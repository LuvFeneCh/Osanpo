using UnityEngine;
using UnityEngine.UI; // UI操作に必要
public class PlayerController : MonoBehaviour
{
    [Header("ジャンプ力")]
    public float jumpForce = 6f;
    [Header("歩き速度")]
    public float moveSpeed = 5.0f;
    [Header("ダッシュ速度")] // 【追加】ダッシュ時の速度設定
    public float dashSpeed = 10.0f;
    [Header("スタミナ設定")]
    public Slider StaminaGauge;      // 作成したStaminaGaugeをInspectorで割り当て
    public Vector3 gaugeOffset = new Vector3(0f, 1.2f, 0f); // プレイヤーの頭上の高さ調整
    public float maxStamina = 100f;   // 最大スタミナ
    public float currentStamina;      // 現在のスタミナ
    public float dashDrainRate = 35f; // 1秒あたりのスタミナ消費量
    public float staminaRegenRate = 20f; // 1秒あたりの回復量
    public Color normalStaminaColor = new Color(0.2f, 0.9f, 0.4f);    // 通常時の色（緑系）
    public Color exhaustedStaminaColor = new Color(1.0f, 0.25f, 0.25f); // スタミナ切れ回復中の色（赤）
    private Image staminaFillImage;   // ゲージの中身（Fill）のImage
    private bool isExhausted = false; // スタミナ切れ（オーバーヒート）状態か

    // 行動分析ログ用（外部から参照可能にする）
    [Header("行動分析ログ用")]
    public int staminaDepletedCount = 0; // スタミナを使い切った回数
    public float totalDashTime = 0f;     // ダッシュした合計時間

    private Rigidbody2D rb;

    // --- ジャンプ制御用の変数 ---
    public int jumpCount = 0;            // 実際にジャンプした回数
    public int totalJumpCount = 0;
    private int maxJumps = 2; // ジャンプできる最大回数（2段ジャンプ）

    void Start()
    {
        // プレイヤーの物理エンジン（重力）を取得
        rb = GetComponent<Rigidbody2D>();

        // 【追加】ゲーム開始時は必ず操作を有効にする
        this.enabled = true;

        currentStamina = maxStamina;
        if (StaminaGauge != null)
        {
            StaminaGauge.maxValue = maxStamina;
            StaminaGauge.value = currentStamina;

            if (StaminaGauge != null && StaminaGauge.fillRect != null)
            {
                staminaFillImage = StaminaGauge.fillRect.GetComponent<Image>();
                if (staminaFillImage != null)
                {
                    staminaFillImage.color = normalStaminaColor;
                }
            }
        }
    }

    void Update()
    {
        void HandleStaminaAndDash(bool wantsToDash)
        {
            if (wantsToDash && !isExhausted && currentStamina > 0f)
            {
                currentStamina -= dashDrainRate * Time.deltaTime;
                totalDashTime += Time.deltaTime;

                if (currentStamina <= 0f)
                {
                    currentStamina = 0f;
                    isExhausted = true; // スタミナ切れ（MAXになるまでダッシュ不可）
                    staminaDepletedCount++;
                }
            }
            else
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                if (currentStamina >= maxStamina)
                {
                    currentStamina = maxStamina;
                    isExhausted = false; // MAXまで回復したら制限解除
                }
            }

            // UIゲージの数値・色・位置を更新
            if (StaminaGauge != null)
            {
                StaminaGauge.value = currentStamina;

                if (staminaFillImage != null)
                {
                    // 0のときは完全に非表示にして「残りかす」を消す
                    staminaFillImage.enabled = (currentStamina > 0.05f);

                    // スタミナ切れ回復中は赤色、通常時は緑色に設定
                    staminaFillImage.color = isExhausted ? exhaustedStaminaColor : normalStaminaColor;
                }

                if (Camera.main != null)
                {
                    StaminaGauge.transform.position = Camera.main.WorldToScreenPoint(transform.position + gaugeOffset);
                }
            }
        }

        // Aキー(-1) / Dキー(1) の入力を受け取る
        float moveInput = Input.GetAxisRaw("Horizontal");

        // ダッシュ入力とスタミナ残量から「ダッシュ可能か」を判定
        bool wantsToDash = Input.GetKey(KeyCode.LeftShift) && Mathf.Abs(moveInput) > 0.1f;
        bool canDash = wantsToDash && !isExhausted && currentStamina > 0f;

        // スタミナの消費・回復処理を実行
        HandleStaminaAndDash(wantsToDash);

        // ダッシュ可能ならダッシュ速度、スタミナ切れなら通常速度にする
        float currentSpeed = canDash ? dashSpeed : moveSpeed;

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
                totalJumpCount++; // 総ジャンプ回数を1増やす
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

        ResetStaminaLogs();
    }

    public void ResetStaminaLogs()
    {
        currentStamina = maxStamina;
        isExhausted = false;
        totalJumpCount = 0;
        staminaDepletedCount = 0;
        totalDashTime = 0f;
    }
}