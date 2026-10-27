using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class NetworkTest : MonoBehaviour
{
    [System.Serializable]
    public class LogData { public float survivalTime; public int missCount; }
    [System.Serializable]
    public class ResponseData { public string color; }

    public Renderer targetPlatform;
    private string serverUrl = "http://127.0.0.1:5000/api/evaluate";

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(SendLogAndChangeColor());
    }

    private IEnumerator SendLogAndChangeColor()
    {
        LogData log = new LogData { survivalTime = 12.5f, missCount = 2 };
        string jsonBody = JsonUtility.ToJson(log);

        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log("Flaskへログを送信中...");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("通信エラー: " + request.error);
            }
            else
            {
                string responseText = request.downloadHandler.text;
                Debug.Log("Flaskからの返答: " + responseText);
                ResponseData response = JsonUtility.FromJson<ResponseData>(responseText);
                ApplyColor(response.color);
            }
        }
    }

    private void ApplyColor(string colorName)
    {
        if (targetPlatform == null) return;
        switch (colorName)
        {
            case "red": targetPlatform.material.color = Color.red; break;
            case "blue": targetPlatform.material.color = Color.blue; break;
            case "yellow": targetPlatform.material.color = Color.yellow; break;
        }
    }
}
