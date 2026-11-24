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
        public float survivalTime;        // 生存時間（またはクリア時間）
        public int missCount;             // 累計ミス回数
        public float currentProbability;  // 現在の試練確率
        public int jumpCount;             // 実際のジャンプ回数
        public float leftInputTime;       // 左キー入力時間
        public float rightInputTime;      // 右キー入力時間
        public int reachedPieceCount;     // 到達したピース数
        public string deathPieceType;     // 敗因地形
        public bool isCleared;            // クリア成否
        public int staminaDepletedCount;  // 【追加】スタミナ切れ回数
        public float totalDashTime;       // 【追加】ダッシュ合計時間
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
    public TextMeshProUGUI goalText;

    [Header("地形ピース（Prefab）の登録")]
    public GameObject startPrefab;
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
    private PlayerController playerController;
    private List<GameObject> spawnedPieces = new List<GameObject>();
    private List<(float xPos, string type)> pieceHistory = new List<(float, string)>();

    // --- 内部パラメータ ---
    private float trialProbability = 0.5f;
    private float currentSurvivalTime = 0f;
    private int totalMissCount = 0;
    private bool isPlaying = false;
    private bool isThinking = false;

    // --- 行動分析用ログ変数 ---
    private float currentLeftTime = 0f;
    private float currentRightTime = 0f;

    void Start()
    {
        UpdateDebugDisplay();
        if (player != null)
        {
            playerStartPos = player.position;
            playerController = player.GetComponent<PlayerController>();
        }
        ResetTerrain();
    }

    void Update()
    {
        if (isPlaying)
        {
            currentSurvivalTime += Time.deltaTime;
            RecordPlayerInputs();
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

        if (isPlaying && player != null && (player.position.x + spawnDistance) > nextSpawnX)
        {
            SpawnNextPiece();
        }
    }

    // 左右入力時間のみを計測（ジャンプ数はPlayerControllerから取得）
    private void RecordPlayerInputs()
    {
        float h = Input.GetAxisRaw("Horizontal");
        if (h < -0.1f)
        {
            currentLeftTime += Time.deltaTime;
        }
        else if (h > 0.1f)
        {
            currentRightTime += Time.deltaTime;
        }
    }

    private (int reachedCount, string pieceType) AnalyzePlayerLocation()
    {
        if (player == null || pieceHistory.Count == 0) return (0, "Unknown");

        float px = player.position.x;
        int reached = 0;
        string lastType = "Start";

        for (int i = 0; i < pieceHistory.Count; i++)
        {
            if (px >= pieceHistory[i].xPos - (pieceWidth * 0.5f))
            {
                reached = i + 1;
                lastType = pieceHistory[i].type;
            }
        }
        return (reached, lastType);
    }

    public void OnPlayerDeath()
    {
        isPlaying = false;
        totalMissCount++;

        var locInfo = AnalyzePlayerLocation();
        int jumps = playerController != null ? playerController.totalJumpCount : 0;
        int depleted = playerController != null ? playerController.staminaDepletedCount : 0;
        float dashTime = playerController != null ? playerController.totalDashTime : 0f;

        Debug.Log($"【死亡ログ】生存:{currentSurvivalTime:F1}s, ジャンプ:{jumps}回, スタミナ切れ:{depleted}回, ダッシュ:{dashTime:F1}s, 到達:{locInfo.reachedCount}個, 敗因:{locInfo.pieceType}");

        if (!isThinking)
        {
            StartCoroutine(SendLogToAI(
                currentSurvivalTime,
                totalMissCount,
                trialProbability,
                jumps,
                currentLeftTime,
                currentRightTime,
                locInfo.reachedCount,
                locInfo.pieceType,
                false,
                depleted,
                dashTime
            ));
        }

        ResetTerrain();
    }

    public void OnPlayerGoal()
    {
        isPlaying = false;
        if (goalText != null) goalText.gameObject.SetActive(true);

        int jumps = playerController != null ? playerController.totalJumpCount : 0;
        int depleted = playerController != null ? playerController.staminaDepletedCount : 0;
        float dashTime = playerController != null ? playerController.totalDashTime : 0f;

        Debug.Log($"【クリアログ】タイム:{currentSurvivalTime:F1}s, ジャンプ:{jumps}回, スタミナ切れ:{depleted}回, ダッシュ:{dashTime:F1}s");

        if (!isThinking)
        {
            StartCoroutine(SendLogToAI(
                currentSurvivalTime,
                totalMissCount,
                trialProbability,
                jumps,
                currentLeftTime,
                currentRightTime,
                piecesToGoal,
                "Clear",
                true,
                depleted,
                dashTime
            ));
        }

        currentSurvivalTime = 0f;
        StartCoroutine(WaitAndResetForGoal());
    }

    private IEnumerator SendLogToAI(float survivalTime, int missCount, float currentProb, int jumps, float leftTime, float rightTime, int reached, string pieceType, bool cleared, int depleted, float dashTime)
    {
        isThinking = true;

        LogData log = new LogData
        {
            survivalTime = survivalTime,
            missCount = missCount,
            currentProbability = currentProb,
            jumpCount = jumps,
            leftInputTime = leftTime,
            rightInputTime = rightTime,
            reachedPieceCount = reached,
            deathPieceType = pieceType,
            isCleared = cleared,
            staminaDepletedCount = depleted,
            totalDashTime = dashTime
        };
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

        isThinking = false;
    }

    private void ResetTerrain()
    {
        isPlaying = false;

        foreach (GameObject piece in spawnedPieces)
        {
            if (piece != null) Destroy(piece);
        }
        spawnedPieces.Clear();
        pieceHistory.Clear();

        if (player != null)
        {
            player.position = playerStartPos;
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = Vector2.zero;

            if (playerController != null)
            {
                playerController.enabled = true;
                playerController.RespawnReset();
            }
        }

        nextSpawnX = playerStartPos.x;
        currentPieceCount = 0;
        isGoalSpawned = false;

        if (startPrefab != null)
        {
            GameObject startPiece = Instantiate(startPrefab, new Vector3(nextSpawnX, spawnY, 0f), Quaternion.identity);
            spawnedPieces.Add(startPiece);
            pieceHistory.Add((nextSpawnX, "Start"));
            nextSpawnX += pieceWidth;
        }

        for (int i = 0; i < 3; i++)
        {
            SpawnNextPiece(forceSafe: true);
        }

        currentSurvivalTime = 0f;
        currentLeftTime = 0f;
        currentRightTime = 0f;
        isPlaying = true;
    }

    private void SpawnNextPiece(bool forceSafe = false)
    {
        if (isGoalSpawned) return;

        GameObject pieceToSpawn = null;
        string spawnedType = "Relief";

        if (currentPieceCount >= piecesToGoal && goalPrefab != null)
        {
            pieceToSpawn = goalPrefab;
            spawnedType = "Goal";
            isGoalSpawned = true;
        }
        else
        {
            float roll = Random.Range(0.0f, 1.0f);
            if (forceSafe || roll > trialProbability)
            {
                pieceToSpawn = reliefPieces[Random.Range(0, reliefPieces.Length)];
                spawnedType = "Relief";
            }
            else
            {
                pieceToSpawn = trialPieces[Random.Range(0, trialPieces.Length)];
                spawnedType = "Trial";
            }
            currentPieceCount++;
        }

        if (pieceToSpawn != null)
        {
            Vector3 spawnPos = new Vector3(nextSpawnX, spawnY, 0f);
            GameObject newPiece = Instantiate(pieceToSpawn, spawnPos, Quaternion.identity);
            spawnedPieces.Add(newPiece);
            pieceHistory.Add((nextSpawnX, spawnedType));
            nextSpawnX += pieceWidth;
        }
    }

    private void UpdateDebugDisplay()
    {
        if (debugText != null)
        {
            float reliefProb = 1.0f - trialProbability;
            int jumps = playerController != null ? playerController.totalJumpCount : 0;
            int depleted = playerController != null ? playerController.staminaDepletedCount : 0;
            debugText.text = $"試練確率: {trialProbability * 100:F1}%\n" +
                             $"救済確率: {reliefProb * 100:F1}%\n" +
                             $"生存時間: {currentSurvivalTime:F1}秒\n" +
                             $"ジャンプ: {jumps}回\n" +
                             $"息切れ: {depleted}回\n" +
                             $"ミス: {totalMissCount}回\n";
        }
    }

    private IEnumerator WaitAndResetForGoal()
    {
        yield return new WaitForSeconds(5.0f);
        if (goalText != null) goalText.gameObject.SetActive(false);
        ResetTerrain();
    }
}