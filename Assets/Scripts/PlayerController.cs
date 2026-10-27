using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("ジャンプ力")]
    public float jumpForce = 6f;
    [Header("歩き速度")]
    public float moveSpeed = 5.0f;
    [Header("ダッシュ速度")] // 【追加】ダッシュ時の速度設定
    public float dashSpeed = 10.0f;

    private Rigidbody2D rb;

    // --- ジャンプ制御用の変数 ---
    private int jumpCount = 0;
    private int maxJumps = 2; // ジャンプできる最大回数（2段ジャンプ）

    void Start()
    {
        // プレイヤーの物理エンジン（重力）を取得
        rb = GetComponent<Rigidbody2D>();

        // 【追加】ゲーム開始時は必ず操作を有効にする
        this.enabled = true;
    }

    void Update()
    {
        // ---------------------------------------------------------
        // 【追加】Shiftキーを押している間は dashSpeed を使う
        // ---------------------------------------------------------
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentSpeed = dashSpeed;
        }

        // Aキー(-1) / Dキー(1) の入力を受け取る
        float moveInput = Input.GetAxisRaw("Horizontal");

        // 左右の移動速度を設定（ダッシュ判定を反映させた currentSpeed を使用）
        rb.velocity = new Vector2(moveInput * currentSpeed, rb.velocity.y);

        // --- ジャンプ処理 ---
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // ジャンプ回数が最大回数より少ない場合のみジャンプ実行
            if (jumpCount < maxJumps)
            {
                // Y軸の速度をリセットしてから力を加える
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

    // --- 着地判定 ---
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
        // AIマネージャーに死亡を通知する
        MockMetaAIManager aiManager = FindObjectOfType<MockMetaAIManager>();
        if (aiManager != null)
        {
            aiManager.OnPlayerDeath(); // 生存時間の計測等はAIマネージャー側で行う
        }

        // 落下速度とジャンプ回数をリセット
        rb.velocity = Vector2.zero;
        jumpCount = 0;

        // ※位置を初期位置に戻す処理（startPosition）は、
        //  先ほど追加した MockMetaAIManager の ResetTerrain() 側で地形リセットと同時に行われます。
    }

    // ---------------------------------------------------------
    // 【メモ】ゴールに触れたときの処理 (Trigger判定)
    // ---------------------------------------------------------
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Goal"))
        {
            Debug.Log("クリア！ゴールに到達しました！");

            // 1. プレイヤーの操作を完全停止（速度と移動・ジャンプ入力を無効化）
            rb.velocity = Vector2.zero;
            this.enabled = false; // PlayerControllerスクリプト自体を一時停止して操作を受け付けなくする

            // 2. AIマネージャーにゴールを通知する
            MockMetaAIManager aiManager = FindObjectOfType<MockMetaAIManager>();
            if (aiManager != null)
            {
                aiManager.OnPlayerGoal();
            }
        }
    }

    // ---------------------------------------------------------
    // 【メモ】リスポーン時に外部（AIマネージャー等）から呼ばれて操作を復帰させる関数
    // ---------------------------------------------------------
    public void RespawnReset()
    {
        this.enabled = true; // 操作を再び有効化

        // rbがまだ取得されていない場合の安全装置
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        rb.velocity = Vector2.zero;
        jumpCount = 0;
    }
}