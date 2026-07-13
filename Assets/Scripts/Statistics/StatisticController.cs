using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

/// <summary>
/// Zeigt User- und Ranking-KPIs basierend auf MQTT-Leaderboard-Nachrichten an.
/// Erwartetes Backend-Payload:
/// {
///   "data": {
///     "rows": [
///       { "category": "Performance - Ø", "your_account": "17.2s", "ranking": "#1" }
///     ]
///   }
/// }
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

    [Tooltip("Kategorie innerhalb der Group, z.B. LEADERBOARD/DATA")]
    [SerializeField] private string category = "LEADERBOARD/DATA";

    private readonly Dictionary<string, Text> userTexts = new();
    private readonly Dictionary<string, Text> rankingTexts = new();

    private MsgHandler msgHandler;
    private string _topic;

    void Awake()
    {
        if (!msgHandler)
            msgHandler = FindObjectOfType<MsgHandler>();

        InitializeTextDictionaries();
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

    private void InitializeTextDictionaries()
    {
        userTexts.Clear();
        userTexts.Add("performance", performanceUserText);
        userTexts.Add("quality", qualityUserText);
        userTexts.Add("max_score", maxScoreUserText);
        userTexts.Add("quality_streak", qualityStreakUserText);
        userTexts.Add("badge_counter", badgeCounterText);
        userTexts.Add("max_level", levelText);

        rankingTexts.Clear();
        rankingTexts.Add("performance", performanceRankingText);
        rankingTexts.Add("quality", qualityRankingText);
        rankingTexts.Add("max_score", maxScoreRankingText);
        rankingTexts.Add("quality_streak", qualityStreakRankingText);
        rankingTexts.Add("badge_counter", badgeCounterRankingText);
        rankingTexts.Add("max_level", levelRankingText);
    }

    public void OnNotify(string topic, object data)
    {
        if (topic != _topic)
            return;

        Debug.Log($"[StatisticController] Received data for topic: {topic}");

        if (data is not string jsonString || string.IsNullOrWhiteSpace(jsonString))
        {
            Debug.LogWarning("[StatisticController] Empty or invalid MQTT payload.");
            return;
        }

        try
        {
            JObject json = JObject.Parse(jsonString);
            JArray rows = json["data"]?["rows"] as JArray;

            if (rows == null)
            {
                Debug.LogWarning("[StatisticController] Payload has no data.rows array.");
                return;
            }

            UpdateDashboardFromRows(rows);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[StatisticController] Parse error: {e.Message}");
        }
    }

    private void UpdateDashboardFromRows(JArray rows)
    {
        foreach (JToken row in rows)
        {
            string categoryLabel = row["category"]?.ToString();
            string userValue = row["your_account"]?.ToString() ?? "N/A";
            string rankingValue = row["ranking"]?.ToString() ?? "N/A";

            string key = MapCategoryToKey(categoryLabel);

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning($"[StatisticController] Unknown leaderboard category: {categoryLabel}");
                continue;
            }

            if (userTexts.TryGetValue(key, out Text userText) && userText)
                userText.text = userValue;

            if (rankingTexts.TryGetValue(key, out Text rankingText) && rankingText)
                rankingText.text = rankingValue;
        }

        Debug.Log("[StatisticController] Dashboard updated with leaderboard rows.");
    }

    private string MapCategoryToKey(string categoryLabel)
    {
        if (string.IsNullOrWhiteSpace(categoryLabel))
            return null;

        switch (categoryLabel.Trim())
        {
            case "Performance - Ø":
                return "performance";

            case "Quality - Ø":
                return "quality";

            case "Max Score":
                return "max_score";

            case "Max Level":
                return "max_level";

            case "Quality Streak":
                return "quality_streak";

            case "Badge Counter":
                return "badge_counter";

            default:
                return null;
        }
    }

    private void UpdateDashboardToDefault()
    {
        foreach (var kvp in userTexts)
            if (kvp.Value) kvp.Value.text = "N/A";

        foreach (var kvp in rankingTexts)
            if (kvp.Value) kvp.Value.text = "N/A";

        Debug.Log("[StatisticController] Dashboard reset to default values.");
    }
}