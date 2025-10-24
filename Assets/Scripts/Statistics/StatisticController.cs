using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Rocworks.Mqtt;

/// <summary>
/// The StatisticController class manages the display of user and ranking statistics based on MQTT messages.
/// Implements the IObserver interface to update statistics and notify observers when statistics are updated.
/// </summary>
public class StatisticController : MonoBehaviour, IObserver
{
    [SerializeField] private Text performanceUserText;
    [SerializeField] private Text qualityUserText;
    [SerializeField] private Text maxScoreUserText;
    [SerializeField] private Text qualityStreakUserText;
    [SerializeField] private Text badgeCounterText;
    [SerializeField] private Text levelText;

    [SerializeField] private Text performanceRankingText;
    [SerializeField] private Text qualityRankingText;
    [SerializeField] private Text maxScoreRankingText;
    [SerializeField] private Text qualityStreakRankingText;
    [SerializeField] private Text badgeCounterRankingText;
    [SerializeField] private Text levelRankingText;

    private Dictionary<string, Text> userTexts;
    private Dictionary<string, Text> rankingTexts;
    private MsgHandler msgHandler;

    /// <summary>
    /// Initializes the text fields to default values and registers the observer with the MsgHandler.
    /// </summary>
    void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/RANKING", this);
        }

        InitializeTextDictionaries();
        UpdateDashboardToDefault();
    }

    /// <summary>
    /// Unregisters the observer from the MsgHandler when the object is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/RANKING", this);
        }
    }

    /// <summary>
    /// Initializes the dictionaries for user and ranking texts.
    /// </summary>
    private void InitializeTextDictionaries()
    {
        userTexts = new Dictionary<string, Text>
        {
            { "median_performance", performanceUserText },
            { "median_quality", qualityUserText },
            { "max_score", maxScoreUserText },
            { "quality_streak", qualityStreakUserText },
            { "badge_counter", badgeCounterText },
            { "level", levelText}
        };

        rankingTexts = new Dictionary<string, Text>
        {
            { "median_performance", performanceRankingText },
            { "median_quality", qualityRankingText },
            { "max_score", maxScoreRankingText },
            { "quality_streak", qualityStreakRankingText },
            { "badge_counter", badgeCounterRankingText },
            { "level", levelRankingText}
        };
}


    /// <summary>
    /// Handles MQTT messages to update the statistics dashboard.
    /// </summary>
    public void OnNotify(string eventType, object data)
    {
        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/RANKING")
        {
            Debug.Log($"Received data for topic: {eventType}");
            string jsonString = data as string;

            if (jsonString != null)
            {
                JObject json = JObject.Parse(jsonString);

                // Extract KPIs
                JObject kpis = (JObject)json["kpis"];

                UpdateDashboardFromKpis(kpis);

                // Notify observers about updates
                NotifyObservers("StatisticsUpdated", null);
            }
        }
    }

    /// <summary>
    /// Updates the dashboard with the provided KPI data.
    /// </summary>
    /// <param name="kpis">The KPI data as a JObject.</param>
    private void UpdateDashboardFromKpis(JObject kpis)
    {
        // Update user statistics
        foreach (var key in userTexts.Keys)
        {
            string value = kpis[key]?["value"]?.ToString() ?? "N/A";
            userTexts[key].text = value;
        }

        // Update ranking statistics
        foreach (var key in rankingTexts.Keys)
        {
            string ranking = kpis[key]?["ranking"]?.ToString();
            rankingTexts[key].text = ranking ?? "No Ranking"; // Fallback for null rankings
        }

        Debug.Log("Dashboard updated with new statistics.");
    }

    /// <summary>
    /// Resets the dashboard to default values ("N/A").
    /// </summary>
    private void UpdateDashboardToDefault()
    {
        foreach (var key in userTexts.Keys)
        {
            userTexts[key].text = "N/A";
        }

        foreach (var key in rankingTexts.Keys)
        {
            rankingTexts[key].text = "N/A";
        }

        Debug.Log("Dashboard reset to default values.");
    }

    /// <summary>
    /// Notifies all registered observers of an event.
    /// </summary>
    public void NotifyObservers(string eventType, object data)
    {
        Debug.Log($"Notifying observers for event: {eventType}");
        foreach (var observer in FindObjectsOfType<StatisticController>())
        {
            observer.OnNotify(eventType, data);
        }
    }
}
