using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("ジャンプ力")]
    public float jumpForce = 6f;

    private Rigidbody2D rb;
    private Vector3 startPosition; // リトライ時の復活位置
    private float aliveTime = 0f;  // 生きていた（プレイしていた）時間

    // --- ジャンプ制御用の変数 ---
    private int jumpCount = 0;
    private int maxJumps = 2; // ジャンプできる最大回数（2段ジャンプ）

    void Start()
    {
        // プレイヤーの物理エンジン（重力）を取得
        rb = GetComponent<Rigidbody2D>();
        // 最初の位置を記憶しておく
        startPosition = transform.position;
    }

    void Update()
    {
        // 毎フレーム、プレイ時間を計測する
        aliveTime += Time.deltaTime;

        // --- ジャンプ処理 ---
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // ジャンプ回数が最大回数より少ない場合のみジャンプ実行
            if (jumpCount < maxJumps)
            {
                // Y軸の速度をリセットしてから力を加える（2段目がしっかり跳ねるようにするため）
                rb.velocity = new Vector2(rb.velocity.x, 0f);
                rb.velocity = new Vector2(rb.velocity.x, jumpForce);

                jumpCount++; // ジャンプ回数を1増やす
            }
        }

        // もし画面の下（Y座標が -10 より下）に落ちたら死亡判定
        if (transform.position.y < -10.0f)
        {
            Die();
        }
    }
        // --- 着地判定を追加 ---
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // ぶつかった相手のタグが "Ground" だった場合
        if (collision.gameObject.CompareTag("Ground"))
        {
            // ジャンプ回数をリセット
            jumpCount = 0;
        }
    }

    private void Die()
    {
        // 1. AIマネージャーを探して、実際のプレイ時間（aliveTime）を送信！
        MockMetaAIManager aiManager = FindObjectOfType<MockMetaAIManager>();
        if (aiManager != null)
        {
            aiManager.OnPlayerDeath(aliveTime);
        }

        // 2. プレイヤーの位置を初期位置に戻す（リトライ）
        transform.position = startPosition;
        rb.velocity = Vector2.zero; // 落下速度をリセット

        // 3. プレイ時間を0にリセットして再スタート
        aliveTime = 0f;

        // リトライ時も念のためジャンプ回数をリセット
        jumpCount = 0;
    }
}
