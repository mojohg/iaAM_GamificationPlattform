// MqttAutoSubscriber.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rocworks.Mqtt;

[DefaultExecutionOrder(-50)]
public class MqttAutoSubscriber : MonoBehaviour
{
    [Tooltip("Optional: MsgHandler wird automatisch gesucht, wenn leer.")]
    public MsgHandler msgHandler;

    [Tooltip("Welche Gruppen sollen automatisch via Wildcard subscribed werden?")]
    public List<string> groups = new() { "RECOMMANDATION" };

    [Tooltip("MQTT QoS für die Auto-Subscriptions.")]
    public int qos = 1;

    private static readonly HashSet<string> Subscribed = new(); // dedupe über Szene
    private bool _started;

    void Awake()
    {
        if (!msgHandler) msgHandler = FindObjectOfType<MsgHandler>();
    }

    void OnEnable()
    {
        if (_started) return;
        _started = true;
        StartCoroutine(SubscribeWhenReady());
    }

    IEnumerator SubscribeWhenReady()
    {
        // warte, bis MQTT verbunden ist
        while (msgHandler == null || msgHandler.MqttClient == null ||
               msgHandler.MqttClient.Connection == null || !msgHandler.MqttClient.ConnectState)
        {
            yield return null;
        }

        foreach (var g in groups)
        {
            var wildcard = TopicNamespace.WildcardFor(g);
            if (Subscribed.Add(wildcard)) // nur einmal je Gruppe
            {
                msgHandler.MqttClient.Connection.Subscribe(wildcard, qos);
                Debug.Log($"[MqttAutoSubscriber] Subscribed: {wildcard} (QoS {qos})");
            }
        }
    }
}
