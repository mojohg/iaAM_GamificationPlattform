// TopicSlotController.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

[RequireComponent(typeof(RectTransform))]
public class TopicSlotController : MonoBehaviour, IObserver, IPointerClickHandler
{
    [Header("Topic")]
    [Tooltip("Gruppe innerhalb des Namespaces, z. B. RECOMMANDATION")]
    public string group = "RECOMMANDATION";

    public SlotCategory category = SlotCategory.PROGRESSION;

    [Header("Refs")]
    public MsgHandler msgHandler;    // optional auto-find
    public RectNudge rectNudge;      // dein Script
    public MarqueeText marquee;      // optional
    public TMP_Text label;           // optional

    [Header("Behavior")]
    public int queueLimit = 20;

    private readonly Queue<string> _queue = new();
    private string _topic;
    private bool _showing;

    void Awake()
    {
        if (!msgHandler) msgHandler = FindObjectOfType<MsgHandler>();
        if (!rectNudge) rectNudge = GetComponent<RectNudge>();
    }

    void OnEnable()
    {
        _topic = TopicNamespace.TopicFor(group, category.ToString());
        msgHandler?.RegisterObserver(_topic, this);
        // Auto-Subscribe auf Wildcard macht der MqttAutoSubscriber zentral
    }

    void OnDisable()
    {
        msgHandler?.RemoveObserver(_topic, this);
    }

    // ---- IObserver ----
    public void OnNotify(string topic, object data)
    {
        if (topic != _topic) return;

        var text = ExtractText(data as string);
        if (string.IsNullOrWhiteSpace(text)) return;

        _queue.Enqueue(text);
        while (_queue.Count > queueLimit) _queue.Dequeue();

        if (!_showing) ShowNext();
    }

    // ---- UI ----
    public void OnPointerClick(PointerEventData _) => Acknowledge();

    private void ShowNext()
    {
        if (_showing) return;
        if (_queue.Count == 0) { SetText(""); return; }

        var next = _queue.Dequeue();
        SetText(next);
        rectNudge?.Apply();    // nach rechts
        _showing = true;
    }

    private void Acknowledge()
    {
        rectNudge?.ResetPosition();  // wieder einrücken
        SetText("");
        _showing = false;
        ShowNext();
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
