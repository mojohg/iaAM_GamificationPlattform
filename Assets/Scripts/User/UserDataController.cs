using System;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class UserdataController : MonoBehaviour, IObserver
{
    [Header("UI")]
    [SerializeField] private Text coinText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text usernameText;
    [SerializeField] private Text levelText;

    [Header("Admin Overlay")]
    [SerializeField] private GameObject adminOverlay;

    [Header("Login Overlay")]
    [SerializeField] private GameObject loginOverlay;
    [SerializeField] private bool allowDoubleClickDemoUnlock = true;
    [SerializeField] private float doubleClickThresholdSeconds = 0.35f;

    [Header("MQTT / MsgHandler")]
    [SerializeField] private MsgHandler msgHandler;

    [Header("Topic-Konfiguration")]
    [Tooltip("MQTT-Gruppe, z.B. GAMING")]
    [SerializeField] private string group = "GAMING";

    [Tooltip("Topic-Key relativ zur Gruppe, z.B. USERDATA/OVERVIEW")]
    [SerializeField] private string category = "USERDATA/OVERVIEW";

    private const string LocalUsernameTopic = "UserDataUpdate/Username";
    private const string LocalCoinsTopic = "UserDataUpdate/Coins";
    private const string LocalScoreTopic = "UserDataUpdate/Score";
    private const string LocalLevelTopic = "UserDataUpdate/Level";

    private string UserdataTopic => TopicNamespace.TopicFor(group, category);

    private bool hasActiveUser;
    private bool demoModeUnlocked;
    private float lastClickTime = -1f;

    private void Awake()
    {
        if (!msgHandler)
            msgHandler = FindObjectOfType<MsgHandler>();

        if (!msgHandler)
            Debug.LogError("[UserdataController] MsgHandler not found in scene!");

        InitializeLoginOverlay();
    }

    private void Update()
    {
        HandleLoginOverlayDoubleClick();
    }

    private void OnEnable()
    {
        if (msgHandler == null) return;

        msgHandler.RegisterObserver(UserdataTopic, this);

        msgHandler.RegisterObserver(LocalUsernameTopic, this);
        msgHandler.RegisterObserver(LocalCoinsTopic, this);
        msgHandler.RegisterObserver(LocalScoreTopic, this);
        msgHandler.RegisterObserver(LocalLevelTopic, this);
    }

    private void OnDisable()
    {
        if (msgHandler == null) return;

        msgHandler.RemoveObserver(UserdataTopic, this);

        msgHandler.RemoveObserver(LocalUsernameTopic, this);
        msgHandler.RemoveObserver(LocalCoinsTopic, this);
        msgHandler.RemoveObserver(LocalScoreTopic, this);
        msgHandler.RemoveObserver(LocalLevelTopic, this);
    }

    public void OnNotify(string eventType, object data)
    {
        Debug.Log($"[UserdataController] Received event '{eventType}' with data: {data}");

        if (eventType == UserdataTopic)
        {
            HandleUserdataJson(data);
        }
        else if (eventType == LocalUsernameTopic)
        {
            HandleUsernameData(data);
        }
        else if (eventType == LocalCoinsTopic)
        {
            HandleInternalCoinsData(data);
        }
        else if (eventType == LocalScoreTopic)
        {
            HandleInternalScoreData(data);
        }
        else if (eventType == LocalLevelTopic)
        {
            HandleInternalLevelData(data);
        }
        else
        {
            Debug.LogWarning($"[UserdataController] Unhandled event type: {eventType}");
        }
    }

    private void HandleUserdataJson(object data)
    {
        if (data is not string jsonString)
        {
            Debug.LogError($"[UserdataController] Expected JSON string for userdata, got: {data?.GetType()}");
            return;
        }

        try
        {
            var root = JToken.Parse(jsonString);
            var payload = root["data"] ?? root;

            string username = payload["username"]?.ToString();

            if (!string.IsNullOrWhiteSpace(username))
            {
                SetUsername(username);
                MarkUserAsLoggedIn(username);
            }

            if (TryReadFloat(payload, "coins", out float coins))
            {
                SetCoins(coins);
            }

            if (TryReadFloat(payload, "score", out float score))
            {
                SetScore(score);
            }

            if (TryReadFloat(payload, "level", out float level))
            {
                SetLevel(level);
            }

            if (payload["is_admin"] != null && bool.TryParse(payload["is_admin"]?.ToString(), out bool isAdmin))
            {
                ApplyAdminState(isAdmin);
            }
            else if (!string.IsNullOrWhiteSpace(username))
            {
                ApplyAdminState(username == "AdminUser");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserdataController] Error parsing userdata JSON: {ex}");
        }
    }

    private void HandleUsernameData(object data)
    {
        if (data is not string username)
        {
            Debug.LogError($"[UserdataController] Invalid username data type: {data?.GetType()}");
            return;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            hasActiveUser = false;
            ShowLoginOverlay();
            return;
        }

        SetUsername(username);
        MarkUserAsLoggedIn(username);
        ApplyAdminState(username == "AdminUser");
    }

    private void HandleInternalCoinsData(object data)
    {
        if (data is not float coins)
        {
            Debug.LogError($"[UserdataController] Invalid coins data type: {data?.GetType()}");
            return;
        }

        SetCoins(coins);
    }

    private void HandleInternalScoreData(object data)
    {
        if (data is not float score)
        {
            Debug.LogError($"[UserdataController] Invalid score data type: {data?.GetType()}");
            return;
        }

        SetScore(score);
    }

    private void HandleInternalLevelData(object data)
    {
        if (data is not float level)
        {
            Debug.LogError($"[UserdataController] Invalid level data type: {data?.GetType()}");
            return;
        }

        SetLevel(level);
    }

    private void SetUsername(string username)
    {
        if (usernameText)
            usernameText.text = username;
    }

    private void SetCoins(float coins)
    {
        if (coinText)
            coinText.text = $"Coins: {coins:F2}";
    }

    private void SetScore(float score)
    {
        if (scoreText)
            scoreText.text = $"Score: {score:F2}";
    }

    private void SetLevel(float level)
    {
        if (levelText)
            levelText.text = $"Level: {level:F0}";
    }

    private void ApplyAdminState(bool isAdmin)
    {
        if (!adminOverlay) return;

        adminOverlay.SetActive(!isAdmin);

        Debug.Log($"[UserdataController] Admin state: {(isAdmin ? "Admin" : "Standard User")}");
    }

    private void InitializeLoginOverlay()
    {
        hasActiveUser = false;
        demoModeUnlocked = false;

        ShowLoginOverlay();
    }

    private void MarkUserAsLoggedIn(string username)
    {
        hasActiveUser = true;
        demoModeUnlocked = false;

        HideLoginOverlay();

        Debug.Log($"[UserdataController] User logged in: {username}");
    }

    private void ShowLoginOverlay()
    {
        if (hasActiveUser || demoModeUnlocked)
            return;

        if (loginOverlay)
            loginOverlay.SetActive(true);
    }

    private void HideLoginOverlay()
    {
        if (loginOverlay)
            loginOverlay.SetActive(false);
    }

    private void HandleLoginOverlayDoubleClick()
    {
        if (!allowDoubleClickDemoUnlock)
            return;

        if (hasActiveUser)
            return;

        if (!loginOverlay || !loginOverlay.activeSelf)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        float currentClickTime = Time.unscaledTime;
        bool isDoubleClick = currentClickTime - lastClickTime <= doubleClickThresholdSeconds;

        lastClickTime = currentClickTime;

        if (!isDoubleClick)
            return;

        demoModeUnlocked = true;
        HideLoginOverlay();

        Debug.Log("[UserdataController] Login overlay hidden by double-click demo unlock.");
    }

    private bool TryReadFloat(JToken token, string field, out float value)
    {
        value = 0f;

        var t = token[field];
        if (t == null) return false;

        return float.TryParse(t.ToString(), out value);
    }
}