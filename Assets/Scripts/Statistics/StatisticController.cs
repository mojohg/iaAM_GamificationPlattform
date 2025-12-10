using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

/// <summary>
/// Zeigt User- und Ranking-KPIs basierend auf MQTT-Nachrichten an.
/// Hängt sich über MsgHandler als IObserver an ein Topic.
/// </summary>
public class StatisticController : MonoBehaviour, IObserver
{
    [Header("User KPIs")]
    [SerializeField] private Text performanceUserText;
    [SerializeField] private Text qualityUserText;
    [SerializeField] private Text maxScoreUserText;
    [SerializeField] private Text qualityStreakUserText;
    [SerializeField] private Text badgeCounterText;
    [SerializeField] private Text levelText;

    [Header("Ranking KPIs")]
    [SerializeField] private Text performanceRankingText;
    [SerializeField] private Text qualityRankingText;
    [SerializeField] private Text maxScoreRankingText;
    [SerializeField] private Text qualityStreakRankingText;
    [SerializeField] private Text badgeCounterRankingText;
    [SerializeField] private Text levelRankingText;

    [Header("MQTT Topic")]
    [Tooltip("Group innerhalb des TopicNamespace, z.B. GAMING")]
    [SerializeField] private string group = "GAMING";

    [Tooltip("Kategorie innerhalb der Group, z.B. RANKING")]
    [SerializeField] private string category = "RANKING";

    private readonly Dictionary<string, Text> userTexts   = new();
    private readonly Dictionary<string, Text> rankingTexts = new();

    private MsgHandler msgHandler;
    private string _topic;

    void Awake()
    {
        // MsgHandler suchen
        if (!msgHandler) msgHandler = FindObjectOfType<MsgHandler>();

        // Dictionaries einmalig aufbauen
        InitializeTextDictionaries();

        // Default-Werte setzen
        UpdateDashboardToDefault();
    }

    void OnEnable()
    {
        if (!msgHandler)
        {
            Debug.LogError("[StatisticController] MsgHandler not found in scene.");
            return;
        }

        _topic = TopicNamespace.TopicFor(group, category);
        msgHandler.RegisterObserver(_topic, this);

        Debug.Log($"[StatisticController] Registered for topic: {_topic}");
    }

    void OnDisable()
    {
        if (msgHandler != null && !string.IsNullOrEmpty(_topic))
        {
            msgHandler.RemoveObserver(_topic, this);
            Debug.Log($"[StatisticController] Unregistered from topic: {_topic}");
        }
    }

    /// <summary>
    /// Initialisiert die Dictionaries für User- und Ranking-Texte.
    /// </summary>
    private void InitializeTextDictionaries()
    {
        userTexts.Clear();
        userTexts.Add("median_performance", performanceUserText);
        userTexts.Add("median_quality",     qualityUserText);
        userTexts.Add("max_score",          maxScoreUserText);
        userTexts.Add("quality_streak",     qualityStreakUserText);
        userTexts.Add("badge_counter",      badgeCounterText);
        userTexts.Add("level",              levelText);

        rankingTexts.Clear();
        rankingTexts.Add("median_performance", performanceRankingText);
        rankingTexts.Add("median_quality",     qualityRankingText);
        rankingTexts.Add("max_score",          maxScoreRankingText);
        rankingTexts.Add("quality_streak",     qualityStreakRankingText);
        rankingTexts.Add("badge_counter",      badgeCounterRankingText);
        rankingTexts.Add("level",              levelRankingText);
    }

    // ---- IObserver ----
    public void OnNotify(string topic, object data)
    {
        if (topic != _topic) return;

        Debug.Log($"[StatisticController] Received data for topic: {topic}");

        if (data is not string jsonString || string.IsNullOrWhiteSpace(jsonString))
            return;

        try
        {
            JObject json = JObject.Parse(jsonString);
            JObject kpis = (JObject)json["kpis"];

            if (kpis == null)
            {
                Debug.LogWarning("[StatisticController] Payload has no 'kpis' object.");
                return;
            }

            UpdateDashboardFromKpis(kpis);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[StatisticController] Parse error: {e.Message}");
        }
    }

    /// <summary>
    /// Aktualisiert das Dashboard mit den übergebenen KPI-Daten.
    /// </summary>
    private void UpdateDashboardFromKpis(JObject kpis)
    {
        // User-Statistiken
        foreach (var key in userTexts.Keys)
        {
            if (!userTexts[key]) continue;

            string value = kpis[key]?["value"]?.ToString() ?? "N/A";
            userTexts[key].text = value;
        }

        // Ranking-Statistiken
        foreach (var key in rankingTexts.Keys)
        {
            if (!rankingTexts[key]) continue;

            string ranking = kpis[key]?["ranking"]?.ToString();
            rankingTexts[key].text = ranking ?? "No Ranking";
        }

        Debug.Log("[StatisticController] Dashboard updated with new statistics.");
    }

    /// <summary>
    /// Setzt alle Texte auf Default ("N/A").
    /// </summary>
    private void UpdateDashboardToDefault()
    {
        foreach (var kvp in userTexts)
            if (kvp.Value) kvp.Value.text = "N/A";

        foreach (var kvp in rankingTexts)
            if (kvp.Value) kvp.Value.text = "N/A";

        Debug.Log("[StatisticController] Dashboard reset to default values.");
    }
}
