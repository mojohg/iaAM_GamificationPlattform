using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Rocworks.Mqtt
{
    public class MsgHandler : MonoBehaviour, ISubject
    {
        [Header("MQTT")]
        public MqttClient MqttClient;
        public Toggle IsConnected;

        [Header("Subscription Options")]
        [Tooltip("Nur aktivieren, wenn KEIN MqttAutoSubscriber in der Szene die Root-Wildcards managed.")]
        public bool SubscribeRootWildcard = false;

        // --- User/Observer ---
        [SerializeField] private float userCoins;
        [SerializeField] private float userScore;
        [SerializeField] private float userLevel;
        [SerializeField] private string username;
        private string userRights;
        private JArray backgrounds, badges, avatars, effects, music, scorePresets;
        private string automodeStatus;

        private readonly Dictionary<string, List<IObserver>> topicObservers = new();
        public bool IsMqttConnected { get; private set; }
        private readonly HashSet<string> _activeSubs = new();

        private void OnEnable()
        {
            if (MqttClient == null)
            {
                Debug.LogError("[MsgHandler] MQTT Client not assigned!");
                enabled = false;
                return;
            }

            MqttClient.OnConnected.AddListener(OnConnected);
            MqttClient.OnDisconnected.AddListener(OnDisconnected);
            MqttClient.OnMessageArrived.AddListener(OnMessageArrived);

            TopicNamespace.OnRootChanged += HandleRootChanged;
        }

        private void OnDisable()
        {
            MqttClient.OnConnected.RemoveListener(OnConnected);
            MqttClient.OnDisconnected.RemoveListener(OnDisconnected);
            MqttClient.OnMessageArrived.RemoveListener(OnMessageArrived);

            TopicNamespace.OnRootChanged -= HandleRootChanged;
        }

        // ---------- Observer Mgmt ----------
        public void RegisterObserver(string topic, IObserver observer)
        {
            if (string.IsNullOrWhiteSpace(topic) || observer == null) return;
            if (!topicObservers.TryGetValue(topic, out var list))
            {
                list = new List<IObserver>();
                topicObservers[topic] = list;
                Debug.Log($"[MsgHandler] Created observer list for: {topic}");
            }
            if (!list.Contains(observer))
            {
                list.Add(observer);
                Debug.Log($"[MsgHandler] Observer registered for topic: {topic}");
            }
        }

        public void RemoveObserver(string topic, IObserver observer)
        {
            if (string.IsNullOrWhiteSpace(topic) || observer == null) return;
            if (topicObservers.TryGetValue(topic, out var list))
            {
                list.Remove(observer);
                if (list.Count == 0) topicObservers.Remove(topic);
            }
        }

        public void NotifyObservers(string topic, object data)
        {
            if (topicObservers.TryGetValue(topic, out var list))
                foreach (var obs in list) obs.OnNotify(topic, data);

            if (data is JObject msg && msg["mqtt"] != null && (bool)msg["mqtt"])
                PublishMessage(topic, msg.ToString());
        }

        private void RemapObserverTopics(string oldRoot, string newRoot)
        {
            if (string.IsNullOrEmpty(oldRoot) || string.IsNullOrEmpty(newRoot)) return;

            var remapped = new Dictionary<string, List<IObserver>>();
            foreach (var kv in topicObservers)
            {
                var topic = kv.Key;
                var list = kv.Value;

                if (topic.StartsWith(oldRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    var suffix = topic.Substring(oldRoot.Length).TrimStart('/');
                    var newTopic = $"{newRoot}/{suffix}";
                    remapped[newTopic] = list;
                    if (MqttClient.DebugMessages)
                        Debug.Log($"[MsgHandler] Remapped {topic} → {newTopic}");
                }
                else remapped[topic] = list;
            }
            topicObservers.Clear();
            foreach (var kv in remapped) topicObservers[kv.Key] = kv.Value;
        }

        // ---------- MQTT ----------
        private void OnConnected()
        {
            IsMqttConnected = true;

            if (SubscribeRootWildcard && TopicNamespace.IsReady)
                SubscribeRootWildcardOnce(TopicNamespace.CurrentRoot);

            if (IsConnected != null) IsConnected.isOn = true;
            if (MqttClient.DebugMessages)
                Debug.Log("[MsgHandler] Connected to MQTT Broker");
        }

        private void OnDisconnected()
        {
            IsMqttConnected = false;
            if (IsConnected != null) IsConnected.isOn = false;
            if (MqttClient.DebugMessages)
                Debug.Log("[MsgHandler] Disconnected from MQTT Broker");
        }

        private void PublishMessage(string topic, string payload)
        {
            if (MqttClient?.Connection != null)
                MqttClient.Connection.Publish(topic, payload);
        }

        public void OnMessageArrived(MqttMessage message)
        {
            string topic = message.GetTopic();
            string payload = message.GetString();

            if (MqttClient.DebugMessages)
                Debug.Log($"[MsgHandler] RX {topic}");

            NotifyObservers(topic, payload);

            string userDataTopic   = TopicNamespace.TopicFor("PRODUCTION", "USERDATA");
            string responseTopic   = TopicNamespace.TopicFor("PRODUCTION", "RESPONSE");
            string evaluationTopic = TopicNamespace.TopicFor("PRODUCTION", "EVALUATION/RESULT");
            string scoreTopic      = TopicNamespace.TopicFor("PRODUCTION", "USERDATA/SCORE");
            string coinsTopic      = TopicNamespace.TopicFor("PRODUCTION", "USERDATA/COINS");

            if (topic == userDataTopic) HandleUserData(payload);
            else if (topic == responseTopic) HandleLoginData(payload);
            else if (topic == evaluationTopic) HandleEvaluationData(payload);
            else if (topic == scoreTopic) HandleScoreData(payload);
            else if (topic == coinsTopic) HandleCoinsData(payload);
        }

        private void HandleRootChanged(string oldRoot, string newRoot)
        {
            RemapObserverTopics(oldRoot, newRoot);

            if (!SubscribeRootWildcard || !IsMqttConnected || MqttClient?.Connection == null)
                return;

            // alte Root-Wildcard abmelden, neue anmelden
            var oldWc = $"{oldRoot?.Trim().Trim('/')}/#";
            var newWc = $"{newRoot?.Trim().Trim('/')}/#";
            SafeUnsubscribe(oldWc);
            SafeSubscribe(newWc);
        }

        private void SubscribeRootWildcardOnce(string root)
        {
            var wc = $"{root?.Trim().Trim('/')}/#";
            SafeSubscribe(wc);
        }

        private void SafeSubscribe(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic)) return;
            if (_activeSubs.Contains(topic)) return;
            try
            {
                int qos = Mathf.Clamp(MqttClient.SubscribeQOS, 0, 2);
                MqttClient.Connection.Subscribe(topic, qos);
                _activeSubs.Add(topic);
                if (MqttClient.DebugMessages) Debug.Log($"[MsgHandler] SUB {topic} (QoS {qos})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MsgHandler] Subscribe failed for '{topic}': {ex.Message}");
            }
        }

        private void SafeUnsubscribe(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic)) return;
            if (!_activeSubs.Contains(topic)) return;
            try
            {
                MqttClient.Connection.Unsubscribe(topic);
                _activeSubs.Remove(topic);
                if (MqttClient.DebugMessages) Debug.Log($"[MsgHandler] UNSUB {topic}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MsgHandler] Unsubscribe failed for '{topic}': {ex.Message}");
            }
        }

        // ---------- Payload-Handler ----------
        private void HandleUserData(string json)
        {
            var data = JObject.Parse(json);
            username   = data["username"]?.ToString();
            userCoins  = data["coins"]?.Value<float>() ?? 0;
            userScore  = data["score"]?.Value<float>() ?? 0;
            userLevel  = data["level"]?.Value<float>() ?? 0;
            automodeStatus = data["automode"]?.ToString() ?? "Off";
            NotifyUI();
        }

        private void HandleLoginData(string json)
        {
            var parsed = JObject.Parse(json);
            if (parsed["status"]?.ToString() == "success")
            {
                var user = (JObject)parsed["user_data"];
                username   = user["username"]?.ToString();
                userCoins  = user["coins"]?.Value<float>() ?? 0;
                userScore  = user["score"]?.Value<float>() ?? 0;
                userLevel  = user["level"]?.Value<float>() ?? 0;
                userRights = user["rights"]?.ToString();
                badges      = (JArray)user["badges"];
                backgrounds = (JArray)user["backgrounds"];
                music       = (JArray)user["unlocked_music"];
                avatars     = (JArray)user["unlocked_characters"];
                effects     = (JArray)user["effects"];
                scorePresets= (JArray)user["score_presets"];
                NotifyUI();
            }
        }

        private void HandleScoreData(string json)
        {
            var data = JObject.Parse(json);
            if (data["score"] != null && float.TryParse(data["score"].ToString(), out var s))
            {
                userScore = s;
                NotifyObservers("UserDataUpdate/Score", userScore);
            }
        }

        private void HandleCoinsData(string json)
        {
            var data = JObject.Parse(json);
            if (data["coins"] != null && float.TryParse(data["coins"].ToString(), out var c))
            {
                userCoins = c;
                NotifyObservers("UserDataUpdate/Coins", userCoins);
            }
        }

        private void HandleEvaluationData(string json)
        {
            try
            {
                var parsed = JObject.Parse(json);
                float interp = parsed["interpretation_time"]?.Value<float>() ?? 0;
                float proc   = parsed["processing_time"]?.Value<float>() ?? 0;
                float corr   = parsed["error_correction_time"]?.Value<float>() ?? 0;
                int err      = parsed["error_count"]?.Value<int>() ?? 0;
                bool done    = parsed["completed"]?.Value<bool>() ?? false;

                var evaluationData = new JArray
                {
                    new JObject { ["key"] = "interpretation_time",  ["value"] = interp },
                    new JObject { ["key"] = "processing_time",      ["value"] = proc },
                    new JObject { ["key"] = "error_correction_time",["value"] = corr },
                    new JObject { ["key"] = "error_count",          ["value"] = err },
                    new JObject { ["key"] = "completed",            ["value"] = done }
                };

                NotifyObservers("EvaluationData/Workstep", evaluationData.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MsgHandler] Error handling evaluation data: {ex.Message}");
            }
        }

        private void NotifyUI()
        {
            if (MqttClient.DebugMessages)
                Debug.Log("[MsgHandler] Notifying UI about updated user data");

            NotifyObservers("UserDataUpdate/Coins",     userCoins);
            NotifyObservers("UserDataUpdate/Score",     userScore);
            NotifyObservers("UserDataUpdate/Username",  username);
            NotifyObservers("UserDataUpdate/Level",     userLevel);
            NotifyObservers("UserDataUpdate/Rights",    userRights);
            NotifyObservers("UserDataUpdate/AutoMode",  automodeStatus);
        }
    }
}
