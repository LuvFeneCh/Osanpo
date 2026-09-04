using UnityEngine;
using TMPro;

public class MockMetaAIManager : MonoBehaviour
{
    [Header("GMのUI設定")]
    public TextMeshProUGUI gmText;

    [Header("デバッグ表示設定")]
    public TextMeshProUGUI debugText; // 左上に確率等を表示するためのUI

    [Header("地形ピース（Prefab）の登録")]
    public GameObject[] reliefPieces; // 救済用テーブル（安全な足場など）
    public GameObject[] trialPieces;  // 試練用テーブル（トゲ床、穴など）

    [Header("プレイヤー連動生成の設定")]
    public Transform player;
    public float pieceWidth = 5.0f;
    public float spawnDistance = 10.0f;
    public float spawnY = -3.0f;

    private float nextSpawnX = 0f;

    // --- メタAIの内部パラメータ ---
    private float trialProbability = 0.5f;

    // --- プレイヤーのプレイログ用変数 ---
    private float currentSurvivalTime = 0f; // 現在の生存時間
    private int totalMissCount = 0;         // 通算のミス回数

    void Start()
    {
        UpdateDebugDisplay();

        // 初期足場の生成
        for (int i = 0; i < 3; i++)
        {
            SpawnNextPiece();
        }
    }

    void Update()
    {
        // 毎フレーム生存時間をカウントアップ
        currentSurvivalTime += Time.deltaTime;

        // デバッグ表示を常に更新（生存時間をリアルタイム表示するため）
        UpdateDebugDisplay();

        // テスト用：Tキーで「すぐ死んだ」扱いにする
        if (Input.GetKeyDown(KeyCode.T))
        {
            currentSurvivalTime = 1.5f;
            OnPlayerDeath();
        }

        // テスト用：Eキーで「長く生き残ってから死んだ」扱いにする
        if (Input.GetKeyDown(KeyCode.E))
        {
            currentSurvivalTime = 12.0f;
            OnPlayerDeath();
        }

        // プレイヤーの位置に基づいた自動生成
        if (player != null && (player.position.x + spawnDistance) > nextSpawnX)
        {
            SpawnNextPiece();
        }
    }

    /// <summary>
    /// プレイヤーが死亡（ミス）した時に呼ばれる関数。
    /// （※実際にプレイヤーが死ぬ処理から、この関数を呼び出してください）
    /// </summary>
    public void OnPlayerDeath()
    {
        totalMissCount++; // ミス回数を加算
        Debug.Log($"【ログ受信】死亡。生存時間: {currentSurvivalTime:F1}秒 / ミス回数: {totalMissCount}回");

        // 1. 生存時間とミス回数を渡してAI分析
        SimulateAIAnalysis(currentSurvivalTime, totalMissCount);

        // 2. 次のプレイに向けて生存時間をリセット
        currentSurvivalTime = 0f;
    }

    /// <summary>
    /// 複数の条件（生存時間・ミス回数）を組み合わせてプレイスタイルを判定する
    /// </summary>
    private void SimulateAIAnalysis(float survivalTime, int missCount)
    {
        // パターンA：ミスが多く、かつすぐ死んでしまう（停滞・挫折の危機）
        if (missCount >= 3 && survivalTime < 3.0f)
        {
            trialProbability -= 0.3f + Random.Range(0f, 0.1f);
            gmText.text = "大丈夫？少し落ち着いて。次は安全な道を用意するね。";
        }
        // パターンB：長く生き残った末のミス（集中・順調）
        else if (survivalTime >= 10.0f)
        {
            trialProbability += 0.2f + Random.Range(-0.05f, 0.1f);
            gmText.text = "惜しい！かなり進めるようになってきたね。少し難易度を上げるよ！";
        }
        // パターンC：ミス回数は少ないが、リトライが早い（強気・焦り）
        else if (missCount < 3 && survivalTime < 4.0f)
        {
            trialProbability += 0.1f;
            gmText.text = "ちょっと焦ってるかな？でもその意気だね！";
        }
        // パターンD：その他（通常）
        else
        {
            trialProbability -= 0.1f;
            gmText.text = "ドンマイ！その調子で次も頑張ろう！";
        }

        // 確率が0〜1の範囲を超えないように制限
        trialProbability = Mathf.Clamp(trialProbability, 0.0f, 1.0f);
    }

    private void SpawnNextPiece()
    {
        GameObject pieceToSpawn = null;
        float roll = Random.Range(0.0f, 1.0f);

        if (roll <= trialProbability)
        {
            int index = Random.Range(0, trialPieces.Length);
            pieceToSpawn = trialPieces[index];
        }
        else
        {
            int index = Random.Range(0, reliefPieces.Length);
            pieceToSpawn = reliefPieces[index];
        }

        if (pieceToSpawn != null)
        {
            Vector3 spawnPos = new Vector3(nextSpawnX, spawnY, 0f);
            Instantiate(pieceToSpawn, spawnPos, Quaternion.identity);
            nextSpawnX += pieceWidth;
        }
    }

    private void UpdateDebugDisplay()
    {
        if (debugText != null)
        {
            float reliefProb = 1.0f - trialProbability;

            // Colorタグを削除し、生存時間とミス回数の情報も追加
            debugText.text = $"試練確率: {trialProbability * 100:F1}%\n" +
                             $"救済確率: {reliefProb * 100:F1}%\n" +
                             $"生存時間: {currentSurvivalTime:F1}秒\n" +
                             $"ミス回数: {totalMissCount}回";
        }
    }
}