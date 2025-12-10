using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

/// <summary>
/// Zeigt Charakter-/RPG-Stats (Strength, Health, K/D etc.) inklusive Ranking
/// auf Basis einer MQTT- Nachricht mit KPI-Objekt.
/// </summary>
public class CharacterStatisticController : MonoBehaviour, IObserver
{
    [Header("User Stats (Your Account)")]
    [SerializeField] private Text strengthUserText;
    [SerializeField] private Text healthUserText;
    [SerializeField] private Text enemiesDefeatedUserText;
    [SerializeField] private Text weaponUserText;
    [SerializeField] private Text kdRateUserText;
    [SerializeField] private Text inventoryUserText;

    [Header("Ranking Stats")]
    [SerializeField] private Text strengthRankingText;
    [SerializeField] private Text healthRankingText;
    [SerializeField] private Text enemiesDefeatedRankingText;
    [SerializeField] private Text weaponRankingText;
    [SerializeField] private Text kdRateRankingText;
    [SerializeField] private Text inventoryRankingText;

    [Header("MQTT Topic")]
    [Tooltip("Group innerhalb des TopicNamespace, z.B. GAMING")]
    [SerializeField] private string group = "GAMING";

    [Tooltip("Kategorie innerhalb der Group, z.B. CHARACTER")]
    [SerializeField] private string category = "CHARACTER";

    private readonly Dictionary<string, Text> userTexts   = new();
    private readonly Dictionary<string, Text> rankingTexts = new();

    private MsgHandler msgHandler;
    private string _topic;

    void Awake()
    {
        if (!msgHandler) msgHandler = FindObjectOfType<MsgHandler>();

        InitializeTextDictionaries();
        UpdateDashboardToDefault();
    }

    void OnEnable()
    {
        if (!msgHandler)
        {
            Debug.LogError("[CharacterStatisticController] MsgHandler not found in scene.");
            return;
        }

        _topic = TopicNamespace.TopicFor(group, category);
        msgHandler.RegisterObserver(_topic, this);

        Debug.Log($"[CharacterStatisticController] Registered for topic: {_topic}");
    }

    void OnDisable()
    {
        if (msgHandler != null && !string.IsNullOrEmpty(_topic))
        {
            msgHandler.RemoveObserver(_topic, this);
            Debug.Log($"[CharacterStatisticController] Unregistered from topic: {_topic}");
        }
    }

    /// <summary>
    /// Dictionaries für User-/Ranking-Texte aufbauen.
    /// Keys sind gleichzeitig die JSON-KPI-Namen.
    /// </summary>
    private void InitializeTextDictionaries()
    {
        userTexts.Clear();
        userTexts.Add("strength",          strengthUserText);
        userTexts.Add("health",            healthUserText);
        userTexts.Add("enemies_defeated",  enemiesDefeatedUserText);
        userTexts.Add("weapon",            weaponUserText);
        userTexts.Add("kd_rate",           kdRateUserText);
        userTexts.Add("inventory",         inventoryUserText);

        rankingTexts.Clear();
        rankingTexts.Add("strength",          strengthRankingText);
        rankingTexts.Add("health",            healthRankingText);
        rankingTexts.Add("enemies_defeated",  enemiesDefeatedRankingText);
        rankingTexts.Add("weapon",            weaponRankingText);
        rankingTexts.Add("kd_rate",           kdRateRankingText);
        rankingTexts.Add("inventory",         inventoryRankingText);
    }

    // ---- IObserver ----
    public void OnNotify(string topic, object data)
    {
        if (topic != _topic) return;

        Debug.Log($"[CharacterStatisticController] Received data for topic: {topic}");

        if (data is not string jsonString || string.IsNullOrWhiteSpace(jsonString))
            return;

        try
        {
            JObject json = JObject.Parse(jsonString);
            JObject kpis = (JObject)json["kpis"];

            if (kpis == null)
            {
                Debug.LogWarning("[CharacterStatisticController] Payload has no 'kpis' object.");
                return;
            }

            UpdateDashboardFromKpis(kpis);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CharacterStatisticController] Parse error: {e.Message}");
        }
    }

    /// <summary>
    /// Dashboard mit KPI-Daten aktualisieren.
    /// </summary>
    private void UpdateDashboardFromKpis(JObject kpis)
    {
        // User-Werte
        foreach (var key in userTexts.Keys)
        {
            var text = userTexts[key];
            if (!text) continue;

            string value = kpis[key]?["value"]?.ToString() ?? "N/A";
            text.text = value;
        }

        // Rankings
        foreach (var key in rankingTexts.Keys)
        {
            var text = rankingTexts[key];
            if (!text) continue;

            string ranking = kpis[key]?["ranking"]?.ToString();
            text.text = ranking ?? "No Ranking";
        }

        Debug.Log("[CharacterStatisticController] Character stats updated.");
    }

    /// <summary>
    /// Setzt alle Felder auf "N/A".
    /// </summary>
    private void UpdateDashboardToDefault()
    {
        foreach (var kvp in userTexts)
            if (kvp.Value) kvp.Value.text = "N/A";

        foreach (var kvp in rankingTexts)
            if (kvp.Value) kvp.Value.text = "N/A";

        Debug.Log("[CharacterStatisticController] Dashboard reset to default values.");
    }
}
