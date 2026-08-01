using UnityEngine;
using System.Runtime.InteropServices;

public class UrlParamReader : MonoBehaviour
{
    // Import the JS function
    [DllImport("__Internal")]
    private static extern System.IntPtr GetURLParameter(string paramName);

    // Helper to convert pointer to string

    private string GetParam(string name)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        System.IntPtr valuePtr = GetURLParameter(name);
        return Marshal.PtrToStringAuto(valuePtr);
#else
        // Fallback for testing in Editor
        return "";
#endif
    }

    void Start()
    {
        // Example usage
        string gameType = GetParam("game_type");
        string room = GetParam("room");
        string playerId = GetParam("playerId");
        string name = GetParam("name");
        string timer = GetParam("timer");
        string userToken = GetParam("usertoken");
        string customGameId = GetParam("custom_game_id");

        Debug.Log($"Game Type: {gameType}");
        Debug.Log($"Room: {room}");
        Debug.Log($"Player ID: {playerId}");
        Debug.Log($"Name: {name}");
        Debug.Log($"Timer: {timer}");
        Debug.Log($"User Token: {userToken}");
        Debug.Log($"Custom Game ID: {customGameId}");

        //Invoke(nameof(CacheData(gameType, room, playerId, name, timer, userToken, customGameId)), 0.5f);

        NetworkDataManager.Instance.SetData(gameType, room, playerId, name, timer, userToken, customGameId);
    }

    private void CacheData(string gameType, string room, string playerId, string name, string timer, string userToken, string customGameId)
    {
        NetworkDataManager.Instance.SetData(gameType, room, playerId, name, timer, userToken, customGameId);
    }
}
