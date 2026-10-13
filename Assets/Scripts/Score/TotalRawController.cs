using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Part evidence chart; reads occurrences, never averages missing data as zero.</summary>
public class TotalRawController : MonoBehaviour, IObserver
{
    [Header("Four-time pie (Radial360 images, cumulative layers)")]
    [SerializeField] private Image interpretationSegment;
    [SerializeField] private Image processingSegment;
    [SerializeField] private Image errorCorrectionSegment;
    [SerializeField] private Image unclassifiedSegment;
    [SerializeField] private Text measurementStatusText;
    [SerializeField] private Text errorCountText;
    [SerializeField] private Text qualityText;
    [SerializeField] private Image qualityBar;

    [Header("MQTT")]
    [SerializeField] private string group = "EVALUATION";
    [SerializeField] private string category = "PART";
    [SerializeField] private string detail = "RESULT/DATA";

    private MsgHandler _messages;
    private string _topic;

    private void Start()
    {
        _messages = FindObjectOfType<MsgHandler>();
        _topic = TopicNamespace.TopicFor(group, category, detail);
        _messages?.RegisterObserver(_topic, this);
        TopicNamespace.OnRootChanged += HandleRootChanged;
        ClearTimeDiagram("Noch keine wissenschaftliche Part-Zeitbilanz");
        if (qualityText) qualityText.text = "Qualität: unbekannt";
        if (errorCountText) errorCountText.text = "Fehler: unbekannt";
        if (qualityBar) qualityBar.color = Color.gray;
    }

    private void OnDestroy()
    {
        TopicNamespace.OnRootChanged -= HandleRootChanged;
        _messages?.RemoveObserver(_topic, this);
    }

    private void HandleRootChanged(string oldRoot, string newRoot) =>
        _topic = TopicNamespace.TopicFor(group, category, detail);

    public void OnNotify(string eventType, object data)
    {
        if (eventType != _topic || data is not string json) return;
        JObject root;
        try { root = JObject.Parse(json); }
        catch (System.Exception) { return; }
        var payload = root["data"] as JObject;
        if (payload == null) return;

        if (EoeeTimeProjection.TryParsePart(json, out var t))
            ShowTimeDiagram(t);
        else
            ClearTimeDiagram("Part: Zeitbilanz unvollständig / Evidenz fehlt");

        // MWS GAMIFICATION_EVIDENCE_V1 reports detected errors as a lower bound
        // when coverage is incomplete; do not treat legacy summary zero as proof.
        var evidence = payload["evaluation_results"]?["gamification_evidence"];
        var detected = evidence?["known_detected_error_count"];
        bool lowerBound = evidence?["detected_error_count_is_lower_bound"]?.Value<bool>() ?? true;
        if (errorCountText)
        {
            if (detected == null || detected.Type == JTokenType.Null)
                errorCountText.text = "Fehler: Messabdeckung unbekannt";
            else
                errorCountText.text = lowerBound
                    ? $"Mindestens {detected} Fehler erkannt (Teilerfassung)"
                    : $"Fehler: {detected}";
        }

        string quality = payload["quality"]?.ToString().Trim().ToLowerInvariant();
        bool isGood = quality == "1" || quality == "good" || quality == "pass";
        bool isBad = quality == "0" || quality == "bad" || quality == "fail";
        if (qualityText)
            qualityText.text = isGood ? "Qualität: bestanden" : isBad ? "Qualität: nicht bestanden" : "Qualität: unbekannt";
        if (qualityBar) qualityBar.color = isGood ? Color.green : isBad ? Color.red : Color.gray;
    }

    private void ShowTimeDiagram(EoeeTimeProjection t)
    {
        if (!unclassifiedSegment)
        {
            ClearTimeDiagram("Vierte Zeitfläche im Inspector nicht zugewiesen");
            return;
        }
        if (interpretationSegment) interpretationSegment.fillAmount = t.VisualInterpretationFill;
        if (processingSegment) processingSegment.fillAmount = t.VisualExecutionFill;
        if (errorCorrectionSegment) errorCorrectionSegment.fillAmount = t.VisualCorrectionFill;
        unclassifiedSegment.fillAmount = t.UnclassifiedCumulativeFill;
        if (measurementStatusText)
            measurementStatusText.text = $"{t.MeasurementStatus} · nicht differenzierbar: {t.UnclassifiedMs / 1000f:0.##} s";
    }

    private void ClearTimeDiagram(string reason)
    {
        if (interpretationSegment) interpretationSegment.fillAmount = 0f;
        if (processingSegment) processingSegment.fillAmount = 0f;
        if (errorCorrectionSegment) errorCorrectionSegment.fillAmount = 0f;
        if (unclassifiedSegment) unclassifiedSegment.fillAmount = 0f;
        if (measurementStatusText) measurementStatusText.text = reason;
    }
}
