using System;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class UserdataController : MonoBehaviour, IObserver
{
    [Header("UI")]
    [SerializeField] private Text coinText;      // UI to display coins
    [SerializeField] private Text scoreText;     // UI to display score
    [SerializeField] private Text usernameText;  // UI to display username
    [SerializeField] private Text levelText;     // UI to display level
    [SerializeField] private GameObject adminOverlay;

    [Header("MQTT / MsgHandler")]
    [SerializeField] private MsgHandler msgHandler;

    [Header("Topic-Konfiguration")]
    [Tooltip("MQTT-Gruppe, z.B. GAMING")]
    [SerializeField] private string group = "GAMING";

    [Tooltip("Topic-Key relativ zur Gruppe, z.B. USERDATA/OVERVIEW")]
    [SerializeField] private string category = "USERDATA/OVERVIEW";

    // Optional: interne Events, falls du irgendwo im Code direkt Userdaten setzen willst
    private const string LocalUsernameTopic = "UserDataUpdate/Username";
    private const string LocalCoinsTopic    = "UserDataUpdate/Coins";
    private const string LocalScoreTopic    = "UserDataUpdate/Score";
    private const string LocalLevelTopic    = "UserDataUpdate/Level";

    private string UserdataTopic => TopicNamespace.TopicFor(group, category);

    private void Awake()
    {
        if (!msgHandler)
            msgHandler = FindObjectOfType<MsgHandler>();

        if (!msgHandler)
            Debug.LogError("[UserdataController] MsgHandler not found in scene!");
    }

    private void OnEnable()
    {
        if (msgHandler == null) return;

        // Haupt-Topic aus dem Backend
        msgHandler.RegisterObserver(UserdataTopic, this);

        // Optionale lokale Events (falls irgendwo im Code genutzt)
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

    // ---------------------------------------------------------
    // IObserver
    // ---------------------------------------------------------
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

    // ---------------------------------------------------------
    // Backend-JSON: alle Daten in einem Payload
    // ---------------------------------------------------------
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

            // username
            string username = payload["username"]?.ToString();
            if (!string.IsNullOrEmpty(username))
            {
                SetUsername(username);
            }

            // coins
            if (TryReadFloat(payload, "coins", out float coins))
            {
                SetCoins(coins);
            }

            // score
            if (TryReadFloat(payload, "score", out float score))
            {
                SetScore(score);
            }

            // level
            if (TryReadFloat(payload, "level", out float level))
            {
                SetLevel(level);
            }

            // optionales Admin-Flag
            if (payload["is_admin"] != null && bool.TryParse(payload["is_admin"]?.ToString(), out bool isAdmin))
            {
                ApplyAdminState(isAdmin);
            }
            else if (!string.IsNullOrEmpty(username))
            {
                // Fallback: bestimmter Username schaltet Admin-Mode
                ApplyAdminState(username == "AdminUser");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserdataController] Error parsing userdata JSON: {ex}");
        }
    }

    // ---------------------------------------------------------
    // Lokale / interne Updates (falls genutzt)
    // ---------------------------------------------------------
    private void HandleUsernameData(object data)
    {
        if (data is not string username)
        {
            Debug.LogError($"[UserdataController] Invalid username data type: {data?.GetType()}");
            return;
        }

        SetUsername(username);
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

    // ---------------------------------------------------------
    // UI-Setter (zentral, damit alles konsistent ist)
    // ---------------------------------------------------------
    private void SetUsername(string username)
    {
        if (usernameText)
            usernameText.text = $"Username: {username}";
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

        // Overlay blockiert normale User → Admin = Overlay aus
        adminOverlay.SetActive(!isAdmin);

        Debug.Log($"[UserdataController] Admin state: {(isAdmin ? "Admin" : "Standard User")}");
    }

    // ---------------------------------------------------------
    // Helper
    // ---------------------------------------------------------
    private bool TryReadFloat(JToken token, string field, out float value)
    {
        value = 0f;
        var t = token[field];
        if (t == null) return false;
        return float.TryParse(t.ToString(), out value);
    }
}
