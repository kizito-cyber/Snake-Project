using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class DoorzyApi
{
    private static string baseUrl = "https://doorzygames.ca/api";
    public static string CurrentUserId;
    public static string AuthToken; // <-- bearer token (if provided by auth)

    // Public helper to set token manually (useful for testing with Postman value)
    public static void SetAuthToken(string token)
    {
        AuthToken = string.IsNullOrEmpty(token) ? null : token.Trim();
        Debug.Log($"[DoorzyApi] AuthToken set: {(string.IsNullOrEmpty(AuthToken) ? "null" : "present")}");
    }

    // 1) Auth status (now expects token optionally)
    public static IEnumerator GetAuthStatus(Action<bool, string, string> onComplete)
    {
        string url = $"{baseUrl}/auth/status";
        using var www = UnityWebRequest.Get(url);

        // no auth header for this call by default (we're checking if the server recognizes the session)
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"AuthStatus error: {www.error} (code: {www.responseCode})");
            onComplete(false, null, null);
            yield break;
        }

        string txt = www.downloadHandler.text;
        Debug.Log($"[DoorzyApi] AuthStatus response: {txt}");

        // try parse and capture token if present
        try
        {
            var data = JsonUtility.FromJson<AuthStatus>(txt);
            if (data != null)
            {
                CurrentUserId = data.userId;
                // server might name token accessToken OR token � accept either
                if (!string.IsNullOrEmpty(data.accessToken))
                    AuthToken = data.accessToken;
                else if (!string.IsNullOrEmpty(data.token))
                    AuthToken = data.token;

                onComplete(data.isAuthenticated, data.userId, data.username);
                yield break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"AuthStatus parse failed: {ex.Message}");
        }

        // fallback: not authenticated or unexpected response
        onComplete(false, null, null);
    }

    // Helper to apply bearer token header when available
    private static void ApplyAuthHeader(UnityWebRequest www)
    {
        if (!string.IsNullOrEmpty(AuthToken))
            www.SetRequestHeader("Authorization", $"Bearer {AuthToken}");
    }

    // Start game session (POST)
    public static IEnumerator StartGameSession(Action<string> onComplete)
    {
        string url = $"{baseUrl}/game/start";

        // POST with empty body (server dependent). Use UnityWebRequest.Post which properly sets form headers.
        using var www = UnityWebRequest.PostWwwForm(url, "");
        ApplyAuthHeader(www);

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"StartGame error: {www.error} (code: {www.responseCode}) body: {www.downloadHandler.text}");
            onComplete?.Invoke(null);
            yield break;
        }

        try
        {
            var data = JsonUtility.FromJson<StartSessionResponse>(www.downloadHandler.text);
            onComplete?.Invoke(data?.sessionId);
        }
        catch (Exception ex)
        {
            Debug.LogError($"StartGame parse error: {ex.Message}");
            onComplete?.Invoke(null);
        }
    }

    // SubmitResult (applies auth header)
    public static IEnumerator SubmitResult(float survivalTime, int score, Action<bool, PrizeResponse> onComplete)
    {
        var bodyObj = new SubmitResultRequest
        {
            userId = CurrentUserId,
            survivalTime = survivalTime,
            score = score
        };

        string json = JsonUtility.ToJson(bodyObj);
        string url = $"{baseUrl}/game/result";

        using var www = new UnityWebRequest(url, "POST");
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        www.uploadHandler = new UploadHandlerRaw(bytes);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        ApplyAuthHeader(www);

        yield return www.SendWebRequest();

        Debug.Log($"[API] POST {url} -> code {www.responseCode} body: {www.downloadHandler.text}");
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[API] Error: {www.error}\n{www.downloadHandler.text}");
            onComplete(false, null);
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<PrizeResponse>(www.downloadHandler.text);
            onComplete(resp != null && resp.ok, resp);
        }
        catch (Exception ex)
        {
            Debug.LogError($"SubmitResult parse error: {ex.Message}");
            onComplete(false, null);
        }
    }

    // Get vendor/ad names - calls /game/vendors (matches your Postman screenshot)
    public static IEnumerator GetVendorNames(Action<bool, List<string>> onComplete)
    {
        string url = $"{baseUrl}/game/vendors"; // <- matches Postman: /api/game/vendors
        using var www = UnityWebRequest.Get(url);
        ApplyAuthHeader(www);
        yield return www.SendWebRequest();

        Debug.Log($"[DoorzyApi] GetVendorNames response code: {www.responseCode} body: {www.downloadHandler.text}");

        if (www.result != UnityWebRequest.Result.Success)
        {
            // if it's a 401, log a helpful message
            if (www.responseCode == 401)
                Debug.LogError("[DoorzyApi] 401 Unauthorized - ensure Bearer token is set (DoorzyApi.AuthToken).");

            Debug.LogError($"GetVendorNames error: {www.error} resp: {www.downloadHandler.text}");
            onComplete(false, new List<string>());
            yield break;
        }

        string txt = www.downloadHandler.text.Trim();
        var list = new List<string>();

        try
        {
            // shape 1: plain JSON array: ["a","b","c"]
            if (txt.StartsWith("["))
            {
                var arr = JsonHelper.FromJson<string>(txt);
                if (arr != null) list.AddRange(arr);
            }
            else
            {
                // shape 2: wrapper object: { "vendors": ["a","b"] } or { "adNames": [...] }
                var vendorsWrapper = JsonUtility.FromJson<VendorWrapper>(txt);
                if (vendorsWrapper != null && vendorsWrapper.vendors != null && vendorsWrapper.vendors.Length > 0)
                {
                    list.AddRange(vendorsWrapper.vendors);
                }
                else
                {
                    var alt = JsonUtility.FromJson<AltVendorWrapper>(txt);
                    if (alt != null && alt.adNames != null && alt.adNames.Length > 0)
                        list.AddRange(alt.adNames);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"GetVendorNames parse fail: {ex.Message}. raw={txt}");
        }

        onComplete(true, list);
    }

    // Example: GetGameInfo (applies auth header)
    public static IEnumerator GetGameInfo(Action<bool, string> onComplete)
    {
        string url = $"{baseUrl}/game/info";
        using var www = UnityWebRequest.Get(url);
        ApplyAuthHeader(www);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"GetGameInfo error: {www.error} (code {www.responseCode})");
            onComplete(false, null);
            yield break;
        }

        var data = JsonUtility.FromJson<GameInfoResponse>(www.downloadHandler.text);
        onComplete(true, data?.message);
    }

    // --- DATA MODELS ---
    [Serializable]
    private class AuthStatus
    {
        public bool isAuthenticated;
        public string userId;
        public string username;
        public string accessToken; // some servers use this name
        public string token;       // some servers use this name
    }

    [Serializable] private class StartSessionResponse { public string sessionId; }
    [Serializable] private class SubmitResultRequest { public string userId; public float survivalTime; public int score; }
    [Serializable] private class EligibilityResponse { public bool eligible; public float remaining; }
    [Serializable] public class LeaderboardEntry { public string username; public float survivalTime; public int score; }
    [Serializable] public class PrizeResponse { public bool ok; public string gift; public string message; public string redirect_url; public string vendor; }
    [Serializable] private class GameInfoResponse { public string message; }
    [Serializable] private class LeaderboardWrapper { public List<LeaderboardEntry> entries; }

    // helper wrappers used by GetVendorNames
    [Serializable] private class VendorWrapper { public string[] vendors; }
    [Serializable] private class AltVendorWrapper { public string[] adNames; }

    // JsonHelper to parse JSON arrays via JsonUtility
    public static class JsonHelper
    {
        [Serializable] private class Wrapper<T> { public T[] Items; }
        public static T[] FromJson<T>(string json)
        {
            string newJson = "{\"Items\":" + json + "}";
            var wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper.Items;
        }
    }
}
