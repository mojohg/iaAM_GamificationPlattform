using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocworks.Mqtt
{
    /// <summary>
    /// Subscribt auf Root-bezogene Wildcards. Jetzt:
    /// - wartet optional auf TopicNamespace.IsReady/OnRootReady
    /// - resubscribt bei Rootänderung
    /// </summary>
    public class MqttAutoSubscriber : MonoBehaviour
    {
        public MqttClient MqttClient;
        [Tooltip("Wildcard relativ zum Root (z. B. 'RECOMMANDATION/#' oder '#')")]
        public List<string> RelativeWildcards = new() { "#" };

        [Tooltip("Erst subscriben, wenn TopicNamespace.IsReady true ist (empfohlen)")]
        public bool WaitForRootReady = true;

        private readonly HashSet<string> _active = new();

        private void OnEnable()
        {
            if (MqttClient == null)
            {
                Debug.LogError("[MqttAutoSubscriber] No MqttClient assigned");
                enabled = false;
                return;
            }

            MqttClient.OnConnected.AddListener(OnConnected);
            MqttClient.OnDisconnected.AddListener(OnDisconnected);

            TopicNamespace.OnRootChanged += HandleRootChanged;
            if (WaitForRootReady && TopicNamespace.IsReady)
            {
                // nichts, OnConnected triggert Subscribe
            }
        }

        private void OnDisable()
        {
            MqttClient.OnConnected.RemoveListener(OnConnected);
            MqttClient.OnDisconnected.RemoveListener(OnDisconnected);
            TopicNamespace.OnRootChanged -= HandleRootChanged;
        }

        private void OnConnected()
        {
            if (WaitForRootReady && !TopicNamespace.IsReady) return;
            SubscribeAllFor(TopicNamespace.CurrentRoot);
        }

        private void OnDisconnected()
        {
            // Verbindung weg — aktive Liste behalten; Broker vergisst Subscriptions,
            // beim nächsten Connect subscriben wir neu.
        }

        private void HandleRootChanged(string oldRoot, string newRoot)
        {
            if (MqttClient?.Connection == null || !MqttClient.ConnectState) return;

            // erst alte abmelden
            if (!string.IsNullOrEmpty(oldRoot))
                UnsubscribeAllFor(oldRoot);

            // dann neue anmelden
            if (!string.IsNullOrEmpty(newRoot))
                SubscribeAllFor(newRoot);
        }

        private void SubscribeAllFor(string root)
        {
            if (MqttClient?.Connection == null) return;
            root = root?.Trim().Trim('/');
            int qos = Mathf.Clamp(MqttClient.SubscribeQOS, 0, 2);

            foreach (var rel in RelativeWildcards)
            {
                var wildcard = string.IsNullOrWhiteSpace(rel)
                    ? $"{root}/#"
                    : $"{root}/{rel.Trim().TrimStart('/')}";

                if (_active.Contains(wildcard)) continue;

                MqttClient.Connection.Subscribe(wildcard, qos);
                _active.Add(wildcard);
                if (MqttClient.DebugMessages)
                    Debug.Log($"[MqttAutoSubscriber] SUB {wildcard} (QoS {qos})");
            }
        }

        private void UnsubscribeAllFor(string root)
        {
            if (MqttClient?.Connection == null) return;
            root = root?.Trim().Trim('/');

            var toRemove = new List<string>();
            foreach (var sub in _active)
            {
                if (sub.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                {
                    MqttClient.Connection.Unsubscribe(sub);
                    toRemove.Add(sub);
                    if (MqttClient.DebugMessages)
                        Debug.Log($"[MqttAutoSubscriber] UNSUB {sub}");
                }
            }
            foreach (var s in toRemove) _active.Remove(s);
        }
    }
}
