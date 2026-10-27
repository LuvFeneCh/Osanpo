using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Text;

public class MockMetaAIManager : MonoBehaviour
{
    [System.Serializable]
    public class LogData
    {
        public float survivalTime;
        public int missCount;
        public float currentProbability;
    }

    [System.Serializable]
    public class ResponseData
    {
        public float newProbability;
        public string message;
    }

    [Header("通信設定")]
    public string serverUrl = "http://127.0.0.1:5000/api/evaluate";

    [Header("GMのUI設定")]
    public TextMeshProUGUI gmText;
    public TextMeshProUGUI debugText;
    public TextMeshProUGUI goalText; // ゴール用UI

    [Header("地形ピース（Prefab）の登録")]
    public GameObject startPrefab;    // 【追加】スタート専用のPrefab
    public GameObject[] reliefPieces;
    public GameObject[] trialPieces;
    public GameObject goalPrefab;

    [Header("プレイヤー連動生成の設定")]
    public Transform player;
    public float pieceWidth = 5.0f;
    public float spawnDistance = 15.0f;
    public float spawnY = -3.0f;

    private float nextSpawnX = 0f;

    [Header("ゴールの設定")]
    public int piecesToGoal = 20;

    private int currentPieceCount = 0;
    private bool isGoalSpawned = false;

    private Vector3 playerStartPos;
    private List<GameObject> spawnedPieces = new List<GameObject>();

    // --- 内部パラメータ ---
    private float trialProbability = 0.5f;
    private float currentSurvivalTime = 0f;
    private int totalMissCount = 0;

    // ---------------------------------------------------------
    // 【追加】ゲームが進行中（プレイ中）かどうかを管理するフラグ
    // ---------------------------------------------------------
    private bool isPlaying = false;

    void Start()
    {
        UpdateDebugDisplay();

        if (player != null)
        {
            playerStartPos = player.position;
        }

        ResetTerrain();
    }

    void Update()
    {
        // ---------------------------------------------------------
        // 【追加】プレイ中（isPlayingがtrue）の間だけ生存時間をカウントする
        // ---------------------------------------------------------
        if (isPlaying)
        {
            currentSurvivalTime += Time.deltaTime;
            UpdateDebugDisplay();
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            currentSurvivalTime = 1.5f;
            OnPlayerDeath();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            currentSurvivalTime = 12.0f;
            OnPlayerDeath();
        }

        // プレイ中で、プレイヤーが一定距離進んだら次のピースを生成
        if (isPlaying && player != null && (player.position.x + spawnDistance) > nextSpawnX)
        {
            SpawnNextPiece();
        }
    }

    public void OnPlayerDeath()
    {
        // 死亡した瞬間、タイマーをストップ
        isPlaying = false;

        totalMissCount++;
        Debug.Log($"【ログ送信準備】死亡。生存時間: {currentSurvivalTime:F1}秒 / ミス回数: {totalMissCount}回");

        StartCoroutine(SendLogToAI(currentSurvivalTime, totalMissCount, trialProbability));

        // 地形とプレイヤーをリセット
        ResetTerrain();
    }

    private IEnumerator SendLogToAI(float survivalTime, int missCount, float currentProb)
    {
        LogData log = new LogData { survivalTime = survivalTime, missCount = missCount, currentProbability = currentProb };
        string jsonBody = JsonUtility.ToJson(log);

        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("AIサーバーとの通信エラー: " + request.error);
            }
            else
            {
                string responseText = request.downloadHandler.text;
                ResponseData response = JsonUtility.FromJson<ResponseData>(responseText);

                trialProbability = response.newProbability;
                gmText.text = response.message;
                UpdateDebugDisplay();
            }
        }
    }

    private void ResetTerrain()
    {
        // リセット処理中はタイマーを止める
        isPlaying = false;

        foreach (GameObject piece in spawnedPieces)
        {
            if (piece != null) Destroy(piece);
        }
        spawnedPieces.Clear();

        if (player != null)
        {
            player.position = playerStartPos;
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = Vector2.zero;

            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.enabled = true;
                pc.RespawnReset();
            }
            else
            {
                Debug.LogWarning("PlayerにPlayerControllerスクリプトが見つかりませんでした！");
            }
        }

        nextSpawnX = playerStartPos.x; // スタート位置から開始
        currentPieceCount = 0;
        isGoalSpawned = false;

        // ---------------------------------------------------------
        // 【追加】最初の1個目に「スタート専用Prefab」を生成する
        // ---------------------------------------------------------
        if (startPrefab != null)
        {
            GameObject startPiece = Instantiate(startPrefab, new Vector3(nextSpawnX, spawnY, 0f), Quaternion.identity);
            spawnedPieces.Add(startPiece);
            nextSpawnX += pieceWidth;
        }

        // その後に続く安全な足場を生成
        for (int i = 0; i < 3; i++)
        {
            SpawnNextPiece(forceSafe: true);
        }

        // ---------------------------------------------------------
        // 【追加】初期化がすべて完了したら、タイマーをリセットしてプレイ再開
        // ---------------------------------------------------------
        currentSurvivalTime = 0f;
        isPlaying = true;
    }

    private void SpawnNextPiece(bool forceSafe = false)
    {
        if (isGoalSpawned) return;

        GameObject pieceToSpawn = null;

        if (currentPieceCount >= piecesToGoal && goalPrefab != null)
        {
            pieceToSpawn = goalPrefab;
            isGoalSpawned = true;
        }
        else
        {
            float roll = Random.Range(0.0f, 1.0f);
            if (forceSafe || roll > trialProbability)
            {
                pieceToSpawn = reliefPieces[Random.Range(0, reliefPieces.Length)];
            }
            else
            {
                pieceToSpawn = trialPieces[Random.Range(0, trialPieces.Length)];
            }
            currentPieceCount++;
        }

        if (pieceToSpawn != null)
        {
            Vector3 spawnPos = new Vector3(nextSpawnX, spawnY, 0f);
            GameObject newPiece = Instantiate(pieceToSpawn, spawnPos, Quaternion.identity);
            spawnedPieces.Add(newPiece);
            nextSpawnX += pieceWidth;
        }
    }

    private void UpdateDebugDisplay()
    {
        if (debugText != null)
        {
            float reliefProb = 1.0f - trialProbability;
            debugText.text = $"試練確率: {trialProbability * 100:F1}%\n" +
                             $"救済確率: {reliefProb * 100:F1}%\n" +
                             $"生存時間: {currentSurvivalTime:F1}秒\n" +
                             $"ミス回数: {totalMissCount}回";
        }
    }

    public void OnPlayerGoal()
    {
        // ゴールした瞬間、タイマーをストップ
        isPlaying = false;

        if (goalText != null)
        {
            goalText.gameObject.SetActive(true);
        }

        Debug.Log($"【ゲームクリア】生存時間: {currentSurvivalTime:F1}秒 でクリア！ 5秒後に次のステージへ移動します。");

        // 生存時間を一度クリア
        currentSurvivalTime = 0f;

        StartCoroutine(WaitAndResetForGoal());
    }

    private IEnumerator WaitAndResetForGoal()
    {
        // 5秒間待機（この間は isPlaying = false なのでタイマーは進まない）
        yield return new WaitForSeconds(5.0f);

        if (goalText != null)
        {
            goalText.gameObject.SetActive(false);
        }

        ResetTerrain();
    }
}