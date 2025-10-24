using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

namespace GraphWorkaround
{
    public class HistoryController : MonoBehaviour, IObserver
    {
        public UnityEvent<Dictionary<string, List<float>>, List<string>> OnHistoryDataProcessed =
            new UnityEvent<Dictionary<string, List<float>>, List<string>>();

        [Header("History Settings")]
        [SerializeField] private int maxPoints = 100;

        // Technische Keys, die wir aus den Evals ziehen (ohne "timestamp")
        [SerializeField] private string[] categories =
        {
            "quality",
            "total_interpretation_time",
            "total_processing_time",
            "total_error_correction_time",
            "total_error_count",
            "score"
        };

        [Header("Topics")]
        [SerializeField] private string group = "HISTORY";
        [SerializeField] private string dataCategory = "DATA";      // .../HISTORY/DATA
        [SerializeField] private string requestCategory = "REQUEST"; // .../HISTORY/REQUEST

        private MsgHandler msgHandler;
        private string _topicData;
        private string _topicRequest;

        private readonly Dictionary<string, List<float>> categoryHistory = new();
        private readonly List<string> formattedTimestamps = new();

        public Dictionary<string, List<float>> LatestHistoryData => categoryHistory;
        public List<string> FormattedTimestamps => new(formattedTimestamps);

        void Start()
        {
            foreach (var c in categories)
                categoryHistory[c] = new List<float>();

            msgHandler = FindObjectOfType<MsgHandler>();
            _topicData    = TopicNamespace.TopicFor(group, dataCategory);
            _topicRequest = TopicNamespace.TopicFor(group, requestCategory);

            // Subscribe (Wildcard macht zentral der MqttAutoSubscriber; Observer hier registrieren)
            msgHandler?.RegisterObserver(_topicData, this);
        }

        void OnDestroy()
        {
            msgHandler?.RemoveObserver(_topicData, this);
        }

        /// <summary>Anfrage ans Backend: liefere Historie. Optional: userId, limit, since.</summary>
        public void RequestHistory(string userId = null, int? limit = null, string sinceIso = null)
        {
            if (msgHandler == null) return;
            var jo = new JObject { ["mqtt"] = true };
            if (!string.IsNullOrWhiteSpace(userId)) jo["user_id"] = userId;
            if (limit.HasValue) jo["limit"] = Mathf.Max(1, limit.Value);
            if (!string.IsNullOrWhiteSpace(sinceIso)) jo["since"] = sinceIso;
            msgHandler.NotifyObservers(_topicRequest, jo);
        }

        // --- Observer ---
        public void OnNotify(string eventType, object data)
        {
            if (eventType != _topicData) return;
            if (data is not string s || string.IsNullOrWhiteSpace(s)) return;
            ProcessHistoryData(s);
        }

        void ProcessHistoryData(string json)
        {
            // Reset
            foreach (var c in categories) categoryHistory[c].Clear();
            formattedTimestamps.Clear();

            try
            {
                var root = JToken.Parse(json);
                var container = (root["data"] as JToken) ?? root; // new style or old
                var evaluations = container["evaluations"] as JArray;
                if (evaluations == null || evaluations.Count == 0)
                {
                    Debug.LogWarning("[HistoryController] No evaluations in payload.");
                    OnHistoryDataProcessed.Invoke(categoryHistory, new(formattedTimestamps));
                    return;
                }

                foreach (var ev in evaluations)
                {
                    // Timestamp
                    var tsStr = (string)(ev["timestamp"] ?? ev["ts"]);
                    if (DateTime.TryParse(tsStr, out var ts))
                        formattedTimestamps.Add(ts.ToString("HH:mm"));
                    else
                        formattedTimestamps.Add(string.Empty);

                    // Numeric categories
                    foreach (var c in categories)
                    {
                        if (ev[c] == null) continue;
                        if (float.TryParse(ev[c].ToString(), out var v))
                        {
                            var list = categoryHistory[c];
                            list.Add(v);
                            if (list.Count > maxPoints) list.RemoveAt(0); // drop oldest
                        }
                    }
                }

                OnHistoryDataProcessed.Invoke(categoryHistory, new(formattedTimestamps));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HistoryController] Parse error: {e.Message}");
            }
        }
    }
}
