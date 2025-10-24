using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Newtonsoft.Json.Linq;

public class RecommendationSlotController : MonoBehaviour, IObserver, IPointerClickHandler
{
    [Header("Fallback Namespace")]
    [Tooltip("Ohne Config-UI verwenden wir diesen Root.")]
    public string fallbackRoot = "FHAACHEN/LEANLAB/MWS/0001/MULTIAGENT/RECOMMANDATION/";

    public enum SlotCategory { PROGRESSION, COLLECTION, SHOP, ARENA, TEAM, WELLBEING }
    public SlotCategory category;

    [Header("Refs")]
    public Rocworks.Mqtt.MsgHandler msgHandler;  // zieh dein MsgHandler hier rein
    public RectNudge rectNudge;                  // dein Script
    public MarqueeText marquee;                  // optional
    public TMP_Text label;                       // optional

    [Header("Behavior")]
    public int queueLimit = 20;

    private readonly Queue<string> _queue = new();
    private bool _showing;
    private string _topic;

    void Awake()
    {
        if (!msgHandler) msgHandler = FindObjectOfType<Rocworks.Mqtt.MsgHandler>();
        if (!rectNudge) rectNudge = GetComponent<RectNudge>();
    }

    void OnEnable()
    {
        _topic = fallbackRoot + category.ToString(); // z.B. …/PROGRESSION
        // (Optional) direkt subscriben – zusätzlich zum Wildcard ok
        msgHandler?.MqttClient?.Connection?.Subscribe(_topic, 1);
        msgHandler?.RegisterObserver(_topic, this);
    }

    void OnDisable()
    {
        msgHandler?.RemoveObserver(_topic, this);
        // (Optional) Unsubscribe, wenn du wirklich willst:
        // msgHandler?.MqttClient?.Connection?.Unsubscribe(_topic);
    }

    // IObserver
    public void OnNotify(string topic, object data)
    {
        if (topic != _topic) return;
        var text = ExtractText(data as string);
        if (string.IsNullOrWhiteSpace(text)) return;

        _queue.Enqueue(text);
        while (_queue.Count > queueLimit) _queue.Dequeue();

        if (!_showing) ShowNext();
    }

    public void OnPointerClick(PointerEventData _) => Acknowledge();

    private void ShowNext()
    {
        if (_showing) return;
        if (_queue.Count == 0) { SetText(""); return; }

        var next = _queue.Dequeue();
        SetText(next);
        rectNudge?.Apply();    // nach rechts schieben
        _showing = true;
    }

    private void Acknowledge()
    {
        rectNudge?.ResetPosition(); // wieder einrücken
        SetText("");
        _showing = false;
        ShowNext(); // evtl. nächste aus FIFO
    }

    private void SetText(string t)
    {
        if (marquee) marquee.SetText(t);
        if (label)   label.text = t;
    }

    private static string ExtractText(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return null;
        payload = payload.Trim();
        if (payload.StartsWith("{"))
        {
            try
            {
                var jo = JObject.Parse(payload);
                var t = (string)(jo["text"] ?? jo["message"]);
                return string.IsNullOrWhiteSpace(t) ? jo.ToString() : t;
            }
            catch { /* ignore */ }
        }
        return payload; // plain string
    }
}
