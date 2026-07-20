using UnityEngine;

public class PieceMovement : MonoBehaviour
{
    public float speed = 5.0f; // 流れるスピード

    void Update()
    {
        // 毎フレーム、左方向へ移動させる
        transform.Translate(Vector3.left * speed * Time.deltaTime);

        // 画面外（左端）に消えたら自動で削除する（メモリ節約のため）
        if (transform.position.x < -15.0f)
        {
            Destroy(gameObject);
        }
    }
}