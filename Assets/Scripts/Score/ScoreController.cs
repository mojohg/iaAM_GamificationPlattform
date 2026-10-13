using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Workstep display of server evidence, no scientific calculations.</summary>
public class ScoreController : MonoBehaviour, IObserver
{
    [Header("Four-time pie (Radial360 images, cumulative layers)")]
    [SerializeField] private Image interpretationSegment;
    [SerializeField] private Image processingSegment;
    [SerializeField] private Image errorCorrectionSegment;
    [SerializeField] private Image unclassifiedSegment;
    [SerializeField] private Text measurementStatusText;

    [Header("Workstep")]
    [SerializeField] private Image completionBar;
    [SerializeField] private Text errorCountText;
    [SerializeField] private Text issuesText;

    [Header("MQTT")]
    [SerializeField] private string group = "EVALUATION";
    [SerializeField] private string category = "WORKSTEP";
    [SerializeField] private string detail = "RESULT/DATA";

    private MsgHandler _messages;
    private string _topic;
    private Coroutine _issueScroll;

    private void Start()
    {
        _messages = FindObjectOfType<MsgHandler>();
        _topic = TopicNamespace.TopicFor(group, category, detail);
        _messages?.RegisterObserver(_topic, this);
        TopicNamespace.OnRootChanged += HandleRootChanged;
        ClearTimeDiagram("Keine wissenschaftliche Zeitbilanz");
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

        if (EoeeTimeProjection.TryParseWorkstep(json, out var projection))
            ShowTimeDiagram(projection);
        else
            ClearTimeDiagram("Zeitbilanz unvollständig / kein Vier-Zeiten-Nachweis");

        if (completionBar)
            completionBar.color = payload["completed"]?.Value<bool>() == true
                ? Color.green : Color.gray;

        // The workstep MQTT contract does not guarantee full error coverage.
        // Only authoritative scientific nerr is exact; legacy summary is a lower bound.
        var layer = payload["eoee_layer3"];
        var nerr = layer?["nerr"];
        var detected = payload["evaluation_result"]?["summary"]?["error_count"];
        if (errorCountText)
        {
            if (nerr != null && nerr.Type != JTokenType.Null)
                errorCountText.text = $"Fehler: {nerr}";
            else if (detected != null && detected.Type != JTokenType.Null && detected.Value<int>() > 0)
                errorCountText.text = $"Mindestens {detected} Fehler erkannt";
            else
                errorCountText.text = "Fehler: nicht vollständig erfasst";
        }

        var issues = new List<string>();
        if (payload["evaluation_result"]?["errors"] is JArray errors)
        {
            foreach (var item in errors)
            {
                string message = item["message"]?.ToString();
                if (!string.IsNullOrWhiteSpace(message)) issues.Add(message);
            }
        }
        if (_issueScroll != null) StopCoroutine(_issueScroll);
        if (issuesText) _issueScroll = StartCoroutine(ScrollIssues(issues));
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
            measurementStatusText.text = $"{t.MeasurementStatus} · unbekannt: {t.UnclassifiedMs / 1000f:0.##} s";
    }

    private void ClearTimeDiagram(string reason)
    {
        if (interpretationSegment) interpretationSegment.fillAmount = 0;
        if (processingSegment) processingSegment.fillAmount = 0;
        if (errorCorrectionSegment) errorCorrectionSegment.fillAmount = 0;
        if (unclassifiedSegment) unclassifiedSegment.fillAmount = 0;
        if (measurementStatusText) measurementStatusText.text = reason;
    }

    private IEnumerator ScrollIssues(List<string> issues)
    {
        if (!issuesText) yield break;
        issuesText.text = issues.Count > 0 ? string.Join(" | ", issues) : "Keine erfassten Abweichungen";
        if (issues.Count == 0) yield break;
        var rect = issuesText.rectTransform;
        var parent = rect.parent as RectTransform;
        if (!parent) yield break;
        float width = issuesText.preferredWidth;
        float start = parent.rect.width;
        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        rect.anchoredPosition = new Vector2(start, rect.anchoredPosition.y);
        while (true)
        {
            rect.anchoredPosition += Vector2.left * 100f * Time.deltaTime;
            if (rect.anchoredPosition.x < -width)
                rect.anchoredPosition = new Vector2(start, rect.anchoredPosition.y);
            yield return null;
        }
    }
}
