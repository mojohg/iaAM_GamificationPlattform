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
    [SerializeField] private string category = "PART";
    [SerializeField] private string detail = "RESULT/DATA";

    private MsgHandler msgHandler;
    private string _topic;

    private float totalInterpretationTime = 0f;
    private float totalProcessingTime = 0f;
    private float totalErrorCorrectionTime = 0f;
    private int totalErrorCount = 0;
    private string quality = "N/A";

    private Coroutine animationCoroutine;

    void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        _topic = TopicNamespace.TopicFor(group, category, detail);
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
        if (data is not string s || string.IsNullOrWhiteSpace(s)) return;

        if (!TryParseTotals(
                s,
                out totalInterpretationTime,
                out totalProcessingTime,
                out totalErrorCorrectionTime,
                out totalErrorCount,
                out quality))
        {
            Debug.LogWarning("[TotalRawController] Unrecognized payload for PART RESULT DATA.");
            return;
        }

        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimatePieChart());
        Debug.Log($"[TotalRawController] Updated totals - Interpretation: {totalInterpretationTime}s, Processing: {totalProcessingTime}s, Correction: {totalErrorCorrectionTime}s, Errors: {totalErrorCount}, Quality: {quality}");
        UpdateTextFields();

        if (qualityBar)
        {
            var q = (quality ?? "").Trim().ToLower();
            qualityBar.color = (q == "1" || q == "good") ? Color.green : Color.red;
        }
    }

    bool TryParseTotals(string json, out float interp, out float proc, out float corr, out int errs, out string qual)
    {
        interp = 0f;
        proc = 0f;
        corr = 0f;
        errs = 0;
        qual = "N/A";

        try
        {
            var tok = JToken.Parse(json);

            if (tok["data"] is not JObject d)
                return false;

            // Echte Backend-Feldnamen
            interp = ((float?)d["interpretation_time_sum_ms"] ?? 0f) / 1000f;
            proc   = ((float?)d["operation_time_sum_ms"] ?? 0f) / 1000f;
            corr   = ((float?)d["correction_time_sum_ms"] ?? 0f) / 1000f;

            errs = (int?)d["evaluation_results"]?["summary"]?["error_count"] ?? 0;
            qual = (string?)d["quality"] ?? "N/A";

            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[TotalRawController] Parse error: {ex.Message}");
            return false;
        }
    }

    void ResetUI()
    {
        if (interpretationSegment) interpretationSegment.fillAmount = 0f;
        if (processingSegment) processingSegment.fillAmount = 0f;
        if (errorCorrectionSegment) errorCorrectionSegment.fillAmount = 0f;

        if (errorCountText) errorCountText.text = "Errors: 0";
        if (qualityText) qualityText.text = "Quality: N/A";
    }

    void UpdateTextFields()
    {
        if (errorCountText) errorCountText.text = $"Errors: {totalErrorCount}";
        if (qualityText) qualityText.text = $"Quality: {quality}";
    }

    IEnumerator AnimatePieChart()
    {
        float totalTime = totalInterpretationTime + totalProcessingTime + totalErrorCorrectionTime;
        if (totalTime <= 0f)
            yield break;

        float interpFill = totalInterpretationTime / totalTime;
        float procFill = totalProcessingTime / totalTime;
        float corrFill = totalErrorCorrectionTime / totalTime;

        float duration = 1.2f;
        float t = 0f;

        float sInterp = interpretationSegment ? interpretationSegment.fillAmount : 0f;
        float sProc = processingSegment ? processingSegment.fillAmount : 0f;
        float sCorr = errorCorrectionSegment ? errorCorrectionSegment.fillAmount : 0f;

        float targetProc = interpFill + procFill;
        float targetCorr = targetProc + corrFill;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float e = Mathf.SmoothStep(0f, 1f, t);

            if (interpretationSegment) interpretationSegment.fillAmount = Mathf.Lerp(sInterp, interpFill, e);
            if (processingSegment) processingSegment.fillAmount = Mathf.Lerp(sProc, targetProc, e);
            if (errorCorrectionSegment) errorCorrectionSegment.fillAmount = Mathf.Lerp(sCorr, targetCorr, e);

            yield return null;
        }

        if (interpretationSegment) interpretationSegment.fillAmount = interpFill;
        if (processingSegment) processingSegment.fillAmount = targetProc;
        if (errorCorrectionSegment) errorCorrectionSegment.fillAmount = targetCorr;
    }
}