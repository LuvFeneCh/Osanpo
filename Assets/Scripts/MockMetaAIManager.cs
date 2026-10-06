using UnityEngine;
using TMPro;

public class MockMetaAIManager : MonoBehaviour
{
    [Header("GMのUI設定")]
    public TextMeshProUGUI gmText;
    public TextMeshProUGUI debugText;

    [Header("地形ピース（Prefab）の登録")]
    public GameObject[] reliefPieces; // 救済用（安全な足場など）
    public GameObject[] trialPieces;  // 試練用（トゲ床、穴など）

    [Header("プレイヤー連動生成の設定")]
    public Transform player;            // 【重要】プレイヤーを登録
    public float pieceWidth = 5.0f;     // 足場1つ分の横幅
    public float spawnDistance = 15.0f; // プレイヤーの何メートル先に生成するか
    public float spawnY = -3.0f;        // 足場を生成する高さ

    private float nextSpawnX = 0f;      // 次に足場を置くX座標

    // --- メタAIの内部パラメータ ---
    private float trialProbability = 0.5f;
    private float currentSurvivalTime = 0f;
    private int totalMissCount = 0;

    void Start()
    {
        UpdateDebugDisplay();
        // 落下防止のため、開始直後にいくつか生成しておく
        for (int i = 0; i < 3; i++)
        {
            SpawnNextPiece();
        }
    }

    void Update()
    {
        // 生存時間の計測
        currentSurvivalTime += Time.deltaTime;
        UpdateDebugDisplay();

        // プレイヤーの現在位置＋見えている範囲が、次に生成すべき座標を超えたら生成
        if (player != null && (player.position.x + spawnDistance) > nextSpawnX)
        {
            SpawnNextPiece();
        }
    }

    /// <summary>
    /// プレイヤー死亡時に呼び出す関数
    /// </summary>
    public void OnPlayerDeath()
    {
        totalMissCount++;
        SimulateAIAnalysis(currentSurvivalTime, totalMissCount);
        currentSurvivalTime = 0f; // 生存時間をリセット
    }

    private void SimulateAIAnalysis(float survivalTime, int missCount)
    {
        if (missCount >= 3 && survivalTime < 3.0f)
        {
            trialProbability -= 0.3f + Random.Range(0f, 0.1f);
            gmText.text = "大丈夫？少し落ち着いて。次は安全な道を用意するね。";
        }
        else if (survivalTime >= 10.0f)
        {
            trialProbability += 0.2f + Random.Range(-0.05f, 0.1f);
            gmText.text = "惜しい！かなり進めるようになってきたね。少し難易度を上げるよ！";
        }
        else if (missCount < 3 && survivalTime < 4.0f)
        {
            trialProbability += 0.1f;
            gmText.text = "ちょっと焦ってるかな？でもその意気だね！";
        }
        else
        {
            trialProbability -= 0.1f;
            gmText.text = "ドンマイ！その調子で次も頑張ろう！";
        }

        trialProbability = Mathf.Clamp(trialProbability, 0.0f, 1.0f);
    }

    private void SpawnNextPiece()
    {
        GameObject pieceToSpawn = null;
        float roll = Random.Range(0.0f, 1.0f);

        if (roll <= trialProbability)
        {
            pieceToSpawn = trialPieces[Random.Range(0, trialPieces.Length)];
        }
        else
        {
            pieceToSpawn = reliefPieces[Random.Range(0, reliefPieces.Length)];
        }

        if (pieceToSpawn != null)
        {
            // 固定位置ではなく、nextSpawnXを用いて前方に生成
            Vector3 spawnPos = new Vector3(nextSpawnX, spawnY, 0f);
            Instantiate(pieceToSpawn, spawnPos, Quaternion.identity);

            // 次の生成座標を足場1つ分右にずらす
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
}