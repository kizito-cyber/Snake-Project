using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using ExitGames.Client.Photon;

public class RoomManager : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    [SerializeField] private InputField nicknameInput;   // keep using InputField if that's what your UI uses
    [SerializeField] private GameObject LoadingPanel;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button signupButton;

    [SerializeField] private Button aboutButton;
    [SerializeField] private GameObject aboutPanel;
    [SerializeField] private TextMeshProUGUI aboutText;
    [SerializeField] private Button aboutCloseButton;

    [Header("Play Button (optional)")]
    [SerializeField] private Button playButton;

    // Track auth state so PlayGame won't overwrite a logged-in user's name
    private bool _isAuthenticated = false;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.SendRate = 60;
        PhotonNetwork.SerializationRate = 60;
    }

    private void Start()
    {
        loginButton.gameObject.SetActive(false);
        signupButton.gameObject.SetActive(false);

        if (nicknameInput != null) nicknameInput.gameObject.SetActive(false);

        // 1) Check auth status
        StartCoroutine(DoorzyApi.GetAuthStatus((isAuth, userId, uname) =>
        {
            _isAuthenticated = isAuth;

            if (isAuth)
            {
                Debug.Log($"Welcome back, {uname}");
                // set the Photon nickname to the real username (important: do this BEFORE connecting)
                PhotonNetwork.NickName = !string.IsNullOrEmpty(uname) ? uname : GenerateGuestCode();
                DoorzyApi.CurrentUserId = userId;

                // hide nickname input (we're using the account name)
                if (nicknameInput != null) nicknameInput.gameObject.SetActive(false);
                if (loginButton != null) loginButton.gameObject.SetActive(false);
                if (signupButton != null) signupButton.gameObject.SetActive(false);
            }
            else
            {
                _isAuthenticated = false;
                // not authenticated -> show nickname input and login/signup
                if (nicknameInput != null) nicknameInput.gameObject.SetActive(true);
                if (loginButton != null) loginButton.gameObject.SetActive(true);
                if (signupButton != null) signupButton.gameObject.SetActive(true);

                // set a short guest code as placeholder and on Photon so we always have a non-empty NickName
                string guestCode = GenerateGuestCode();
                PhotonNetwork.NickName = guestCode;
                DoorzyApi.CurrentUserId = null;
                Debug.Log($"Playing as {guestCode}");
            }
        }));

        aboutButton.onClick.AddListener(OnAboutClicked);
        aboutCloseButton.onClick.AddListener(() => aboutPanel.SetActive(false));
        aboutPanel.SetActive(false);

        if (playButton != null)
            playButton.onClick.AddListener(PlayGame);
    }

    public void OnAboutClicked()
    {
        aboutText.text = "Loading…";
        aboutPanel.SetActive(true);

        StartCoroutine(DoorzyApi.GetGameInfo((ok, msg) =>
        {
            if (ok) aboutText.text = msg;
            else aboutText.text = "Could not load game info.\nPlease check your connection.";
        }));
    }

    // Called by Play button or wired from inspector
    public void PlayGame()
    {
        // If the user is authenticated, we MUST NOT overwrite the nickname (the server account name must persist).
        if (_isAuthenticated)
        {
            // PhotonNetwork.NickName already set by auth callback; keep it.
            Debug.Log($"PlayGame: user authenticated, using account name '{PhotonNetwork.NickName}'");
        }
        else
        {
            // For guests, use the input if provided, otherwise generate a guest name.
            string chosen = null;
            if (nicknameInput != null && nicknameInput.gameObject.activeSelf)
            {
                chosen = SanitizeNickname(nicknameInput.text);
            }

            if (!string.IsNullOrEmpty(chosen))
            {
                PhotonNetwork.NickName = chosen;
            }
            else
            {
                // Ensure a short guest code if none provided
                PhotonNetwork.NickName = GenerateGuestCode();
            }

            Debug.Log($"PlayGame: not authenticated, using nickname '{PhotonNetwork.NickName}'");
        }

        LoadingPanel.SetActive(true);
        PhotonNetwork.ConnectUsingSettings();
    }

    public void OnLoginClicked() => Application.OpenURL("https://doorzygames.ca/signInPage");
    public void OnSignupClicked() => Application.OpenURL("https://doorzygames.ca/signUpPage");
    public void OnHomeClicked() => Application.OpenURL("https://doorzygames.ca/index");

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master Server – now loading Game scene…");
        PhotonNetwork.LoadLevel("Game");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError($"Disconnected from Photon: {cause}");
        LoadingPanel.SetActive(false);
    }

    // ---- helpers ----
    private string SanitizeNickname(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        raw = raw.Trim();
        raw = raw.Replace("\n", "").Replace("\r", "").Replace("\t", "");
        int maxLen = 15;
        if (raw.Length > maxLen) raw = raw.Substring(0, maxLen);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var c in raw)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ')
                sb.Append(c);
        }

        string outStr = sb.ToString();
        return string.IsNullOrWhiteSpace(outStr) ? null : outStr;
    }

    private string GenerateGuestCode()
    {
        return "Guest-" + UnityEngine.Random.Range(10000, 99999);
    }
}
