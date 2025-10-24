using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class TotalRawController : MonoBehaviour, IObserver
{
    [Header("UI")]
    [SerializeField] private Image interpretationSegment;
    [SerializeField] private Image processingSegment;
    [SerializeField] private Image errorCorrectionSegment;
    [SerializeField] private Text errorCountText;
    [SerializeField] private Text qualityText;
    [SerializeField] private Image qualityBar;

    [Header("Topic")]
    [SerializeField] private string group = "EVALUATION";
    [SerializeField] private string category = "TOTALRAW"; // .../EVALUATION/TOTALRAW

    private MsgHandler msgHandler;
    private string _topic;

    // State
    private float totalInterpretationTime = 0f;
    private float totalProcessingTime = 0f;
    private float totalErrorCorrectionTime = 0f;
    private int totalErrorCount = 0;
    private string quality = "N/A";

    private Coroutine animationCoroutine;

    void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        _topic = TopicNamespace.TopicFor(group, category);
        if (msgHandler != null) msgHandler.RegisterObserver(_topic, this);
        ResetUI();
    }

    void OnDestroy()
    {
        if (msgHandler != null) msgHandler.RemoveObserver(_topic, this);
    }

    public void OnNotify(string eventType, object data)
    {
        if (eventType != _topic) return;
        if (data is not string s) return;

        if (!TryParseTotals(s,
                out totalInterpretationTime, out totalProcessingTime, out totalErrorCorrectionTime,
                out totalErrorCount, out quality))
        {
            Debug.LogWarning("[TotalRawController] Unrecognized payload for TOTALRAW.");
            return;
        }

        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimatePieChart());
        UpdateTextFields();
        if (qualityBar) qualityBar.color = (quality == "1" || quality.ToLower() == "good") ? Color.green : Color.red;
    }

    bool TryParseTotals(string json, out float interp, out float proc, out float corr, out int errs, out string qual)
    {
        interp = proc = corr = 0f; errs = 0; qual = "N/A";
        try
        {
            var tok = JToken.Parse(json);

            // Neuer Stil: { "data": { ... } }
            var obj = tok["data"] as JObject ?? tok as JObject;
            if (obj != null)
            {
                interp = (float?)obj["total_interpretation_time"] ?? (float?)obj["interpretation_time"] ?? 0f;
                proc   = (float?)obj["total_processing_time"] ?? (float?)obj["processing_time"] ?? 0f;
                corr   = (float?)obj["total_error_correction_time"] ?? (float?)obj["error_correction_time"] ?? 0f;
                errs   = (int?)obj["total_error_count"] ?? (int?)obj["error_count"] ?? 0;
                qual   = (string)(obj["quality"] ?? "N/A");
                return true;
            }
        }
        catch { }
        return false;
    }

    void ResetUI()
    {
        if (interpretationSegment) interpretationSegment.fillAmount = 0f;
        if (processingSegment)     processingSegment.fillAmount = 0f;
        if (errorCorrectionSegment)errorCorrectionSegment.fillAmount = 0f;
        if (errorCountText) errorCountText.text = "Errors: 0";
        if (qualityText)    qualityText.text = "Quality: N/A";
    }

    void UpdateTextFields()
    {
        if (errorCountText) errorCountText.text = $"Errors: {totalErrorCount}";
        if (qualityText)    qualityText.text = $"Quality: {quality}";
    }

    IEnumerator AnimatePieChart()
    {
        float totalTime = totalInterpretationTime + totalProcessingTime + totalErrorCorrectionTime;
        if (totalTime <= 0f) yield break;

        float interpFill = totalInterpretationTime / totalTime;
        float procFill   = totalProcessingTime / totalTime;
        float corrFill   = totalErrorCorrectionTime / totalTime;

        float duration = 1.2f, t = 0f;
        float sInterp = interpretationSegment ? interpretationSegment.fillAmount : 0f;
        float sProc   = processingSegment ? processingSegment.fillAmount : 0f;
        float sCorr   = errorCorrectionSegment ? errorCorrectionSegment.fillAmount : 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float e = Mathf.Clamp01(t);
            if (interpretationSegment) interpretationSegment.fillAmount = Mathf.Lerp(sInterp, interpFill, e);
            if (processingSegment)     processingSegment.fillAmount     = Mathf.Lerp(sProc,   interpFill + procFill, e);
            if (errorCorrectionSegment)errorCorrectionSegment.fillAmount= Mathf.Lerp(sCorr,   interpFill + procFill + corrFill, e);
            yield return null;
        }

        if (interpretationSegment) interpretationSegment.fillAmount = interpFill;
        if (processingSegment)     processingSegment.fillAmount     = interpFill + procFill;
        if (errorCorrectionSegment)errorCorrectionSegment.fillAmount= interpFill + procFill + corrFill;
    }
}
