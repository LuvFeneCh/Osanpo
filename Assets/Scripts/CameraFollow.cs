using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // プレイヤーを登録する
    public float offsetX = 3.0f; // プレイヤーより少し右側を映すためのズレ

    void LateUpdate()
    {
        if (target != null)
        {
            // カメラのX座標だけをプレイヤーに合わせて更新（YとZはそのまま）
            transform.position = new Vector3(target.position.x + offsetX, transform.position.y, transform.position.z);
        }
    }
}