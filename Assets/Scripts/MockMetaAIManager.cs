using UnityEngine;
using TMPro; // GMのセリフ表示用

public class MockMetaAIManager : MonoBehaviour
{
    [Header("GMのUI設定")]
    public TextMeshProUGUI gmText;

    [Header("デバッグ表示設定")]
    public TextMeshProUGUI debugText; // 【新規】左上に確率を表示するためのUI

    [Header("地形ピース（Prefab）の登録")]
    public GameObject[] reliefPieces; // 救済用テーブル（安全な足場など）
    public GameObject[] trialPieces;  // 試練用テーブル（トゲ床、敵など）

    [Header("生成位置")]
    public Transform spawnPoint; // 画面右端など、ピースを生成する場所

    [Header("自動生成の設定")]
    public float spawnInterval = 1.0f; // 生成する間隔（秒）
    private float spawnTimer = 0f;

    // メタAIの内部パラメータ：試練テーブルが選ばれる確率（0.0〜1.0）
    private float trialProbability = 0.5f;

    void Start()
    {
        // 起動時に一度デバッグ表示を更新しておく
        UpdateDebugDisplay();
    }

    void Update()
    {
        // Tキー：即リトライ（1秒）＝ 試練の確率UP
        if (Input.GetKeyDown(KeyCode.T))
        {
            OnPlayerDeath(1.0f);
        }

        // Eキー：ゆっくりリトライ（5秒）＝ 救済の確率UP
        if (Input.GetKeyDown(KeyCode.E))
        {
            OnPlayerDeath(5.0f);
        }

        // --- ここから追加：定期的な自動生成タイマー ---
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f; // タイマーをリセット
            SpawnNextPiece(); // 確率に基づいて次のピース（足場or穴）を生成
        }
    }

    /// <summary>
    /// プレイヤーが死亡（ミス）した時に呼ばれる関数。
    /// ここで操作ログ（今回はリトライまでの時間）を受け取る想定。
    /// </summary>
    public void OnPlayerDeath(float retryTime)
    {
        Debug.Log($"【ログ受信】プレイヤー死亡。リトライ時間: {retryTime}秒");

        // 1. 乱数を用いてAIの文脈分析をシミュレート
        SimulateAIAnalysis(retryTime);

        // ※注意：ここにあった SpawnNextPiece(); は削除してください！

        // 3. 画面上のデバッグ表示を更新
        UpdateDebugDisplay();
    }

    /// <summary>
    /// 本来は外部Pythonサーバーが行う「分析と指示」のモックアップ
    /// </summary>
    private void SimulateAIAnalysis(float retryTime)
    {
        // リトライ速度からプレイスタイルを判定（疑似）
        if (retryTime < 2.0f)
        {
            // 即リトライ ＝ 自力クリア派（熱中状態）と仮定
            // 乱数を混ぜてAIの「粗（揺らぎ）」を表現しつつ、試練の確率を上げる
            trialProbability += 0.2f + Random.Range(-0.05f, 0.1f);
            gmText.text = "やる気満々だね！次は少し難しくするよ！";
        }
        else
        {
            // リトライが遅い ＝ 救済派（停滞状態）と仮定
            // 救済の確率を上げる（試練の確率を下げる）
            trialProbability -= 0.2f + Random.Range(-0.05f, 0.1f);
            gmText.text = "休憩しよう。次は安全な道を用意したよ。";
        }

        // 確率が0〜1の範囲を超えないように制限
        trialProbability = Mathf.Clamp(trialProbability, 0.0f, 1.0f);
    }

    /// <summary>
    /// 確率テーブルに基づいてピースを選択し、生成する
    /// </summary>
    private void SpawnNextPiece()
    {
        GameObject pieceToSpawn = null;

        // 0.0〜1.0の乱数を振り、試練か救済かを決定
        float roll = Random.Range(0.0f, 1.0f);

        if (roll <= trialProbability)
        {
            // 試練テーブルからランダムに1つ選択
            int index = Random.Range(0, trialPieces.Length);
            pieceToSpawn = trialPieces[index];
        }
        else
        {
            // 救済テーブルからランダムに1つ選択
            int index = Random.Range(0, reliefPieces.Length);
            pieceToSpawn = reliefPieces[index];
        }

        if (pieceToSpawn != null && spawnPoint != null)
        {
            Instantiate(pieceToSpawn, spawnPoint.position, Quaternion.identity);
        }
    }

    /// <summary>
    /// 画面左上に確率情報を表示する（デバッグ用）
    /// </summary>
    private void UpdateDebugDisplay()
    {
        if (debugText != null)
        {
            float reliefProb = 1.0f - trialProbability;

            // UIに情報を表示。漢字が含まれるのでフォントアセットの設定が必要です。
            debugText.text = $"<color=#ff4d4d>試練確率: {trialProbability * 100:F1}%</color>\n" +
                             $"<color=#4dff4d>救済確率: {reliefProb * 100:F1}%</color>";
        }
    }
}