using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkDataManager : MonoBehaviour
{
    public static NetworkDataManager Instance { get; private set; }

    private string gameType;
    private string room;
    private string playerId;
    private string name;
    private string timer;
    private string userToken;
    private string customGameId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void SetData(string gameType, string room, string playerId, string name, string timer, string userToken, string customGameId)
    {
        this.gameType = gameType;
        this.room = room;
        this.playerId = playerId;
        this.name = name;
        this.timer = timer;
        this.userToken = userToken;
        this.customGameId = customGameId;
    }

    private int highScore;

    public void SendScoreToLeaderboard(int score)
    {
        highScore = score;

        SubmitScore();
    }

    public void SubmitScore()
    {
        string url = "https://thryl-prod.com/in-game-score/points";
        string token = userToken; // Replace with your actual token

        StartCoroutine(PostScoreRoutine(url, token));
    }

    private IEnumerator PostScoreRoutine(string url, string token)
    {

        // 1. Create the JSON string to match your -d payload
        string jsonPayload = $"{{\"custom_game_id\": \"{customGameId}\", \"points_ingame\": {highScore}}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        // 2. Set up the generic UnityWebRequest for a POST action
        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            // 3. Attach handlers for sending data and receiving the server response
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            // 4. Set your headers (-H parameters)
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", "Bearer " + token);

            // 5. Send the request and wait for it to complete
            yield return webRequest.SendWebRequest();

            // 6. Handle the network result
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Score Submission Failed: {webRequest.error}");
                Debug.LogError($"Server Response: {webRequest.downloadHandler.text}");
            }
            else
            {
                Debug.Log("Score Submitted Successfully!");
                Debug.Log($"Server Response: {webRequest.downloadHandler.text}");
            }
        }
    }
}