using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Rocworks.Mqtt;
using Newtonsoft.Json.Linq;

public class ScoreController : MonoBehaviour, IObserver
{
    [Header("UI")]
    [SerializeField] private Image interpretationSegment;
    [SerializeField] private Image processingSegment;
    [SerializeField] private Image errorCorrectionSegment;
    [SerializeField] private Image completionBar;
    [SerializeField] private Text errorCountText;
    [SerializeField] private Text issuesText;

    [Header("Topic")]
    [SerializeField] private string group = "EVALUATION";
    [SerializeField] private string category = "WORKSTEP";
    [SerializeField] private string detail = "RESULT/DATA";

    private MsgHandler msgHandler;
    private string _topic;

    private float interpretationTime = 0f;
    private float processingTime = 0f;
    private float errorCorrectionTime = 0f;
    private int errorCount = 0;
    private bool completed = false;
    private List<string> issues = new List<string>();

    private Coroutine animationCoroutine;
    private Coroutine scrollingTextCoroutine;

    void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        _topic = TopicNamespace.TopicFor(group, category, detail);
        if (msgHandler != null) msgHandler.RegisterObserver(_topic, this);
    }

    void OnDestroy()
    {
        if (msgHandler != null) msgHandler.RemoveObserver(_topic, this);
    }

    public void OnNotify(string eventType, object data)
    {
        if (eventType != _topic) return;
        if (data is not string s || string.IsNullOrWhiteSpace(s)) return;

        if (!TryParseWorkstep(s, out var interp, out var proc, out var corr, out var errs, out var isDone, out var list))
        {
            Debug.LogWarning("[ScoreController] Unrecognized payload for WORKSTEP RESULT DATA.");
            return;
        }
        Debug.Log($"[ScoreController] Received data - Interpretation: {interp}s, Processing: {proc}s, Correction: {corr}s, Errors: {errs}, Completed: {isDone}, Issues: {list.Count}");
        interpretationTime = interp;
        processingTime = proc;
        errorCorrectionTime = corr;
        errorCount = errs;
        completed = isDone;
        issues = list ?? new List<string>();

        UpdateUI();
    }

    bool TryParseWorkstep(string json, out float interp, out float proc, out float corr, out int errs, out bool done, out List<string> list)
    {
        interp = proc = corr = 0f;
        errs = 0;
        done = false;
        list = new List<string>();

        try
        {
            var tok = JToken.Parse(json);

            if (tok["data"] is not JObject d)
                return false;

            // echte Backend-Feldnamen
            interp = ((float?)d["interpretation_time_ms"] ?? 0f) / 1000f;
            proc   = ((float?)d["operation_time_ms"] ?? 0f) / 1000f;
            corr   = ((float?)d["correction_time_ms"] ?? 0f) / 1000f;
            done   = (bool?)d["completed"] ?? false;

            errs = (int?)d["evaluation_result"]?["summary"]?["error_count"] ?? 0;

            if (d["evaluation_result"]?["errors"] is JArray errorArray)
            {
                foreach (var err in errorArray)
                {
                    var msg = (string)err["message"];
                    if (!string.IsNullOrWhiteSpace(msg))
                        list.Add(msg);
                }
            }

            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ScoreController] Parse error: {ex.Message}");
            return false;
        }
    }

    void UpdateUI()
    {
        var totalTime = interpretationTime + processingTime + errorCorrectionTime;
        if (totalTime > 0f)
        {
            var interpFill = interpretationTime / totalTime;
            var procFill = processingTime / totalTime;
            var corrFill = errorCorrectionTime / totalTime;

            if (animationCoroutine != null) StopCoroutine(animationCoroutine);
            animationCoroutine = StartCoroutine(AnimatePieChart(interpFill, procFill, corrFill));
        }

        if (completionBar) completionBar.color = completed ? Color.green : Color.red;
        if (errorCountText) errorCountText.text = $"Errors: {errorCount}";

        if (scrollingTextCoroutine != null) StopCoroutine(scrollingTextCoroutine);
        if (issuesText) scrollingTextCoroutine = StartCoroutine(ScrollIssuesText());
    }

    IEnumerator AnimatePieChart(float interpFill, float procFill, float corrFill)
    {
        float duration = 1.0f, t = 0f;
        float sInterp = interpretationSegment ? interpretationSegment.fillAmount : 0f;
        float sProc   = processingSegment ? processingSegment.fillAmount : 0f;
        float sCorr   = errorCorrectionSegment ? errorCorrectionSegment.fillAmount : 0f;

        float tgtProc = interpFill + procFill;
        float tgtCorr = tgtProc + corrFill;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float e = Mathf.SmoothStep(0f, 1f, t);
            if (interpretationSegment) interpretationSegment.fillAmount = Mathf.Lerp(sInterp, interpFill, e);
            if (processingSegment)     processingSegment.fillAmount     = Mathf.Lerp(sProc, tgtProc, e);
            if (errorCorrectionSegment) errorCorrectionSegment.fillAmount = Mathf.Lerp(sCorr, tgtCorr, e);
            yield return null;
        }

        if (interpretationSegment) interpretationSegment.fillAmount = interpFill;
        if (processingSegment) processingSegment.fillAmount = tgtProc;
        if (errorCorrectionSegment) errorCorrectionSegment.fillAmount = tgtCorr;
    }

    IEnumerator ScrollIssuesText()
    {
        if (issues == null || issues.Count == 0)
        {
            issuesText.text = "No issues.";
            yield break;
        }

        string fullText = string.Join(" | ", issues);
        issuesText.text = fullText;

        RectTransform rt = issuesText.rectTransform;
        float scrollSpeed = 100f;
        float textWidth = issuesText.preferredWidth;
        float containerWidth = rt.parent.GetComponent<RectTransform>().rect.width;

        rt.sizeDelta = new Vector2(textWidth, rt.sizeDelta.y);
        Vector2 startPos = new Vector2(containerWidth, rt.anchoredPosition.y);
        Vector2 endPos = new Vector2(-textWidth, rt.anchoredPosition.y);
        rt.anchoredPosition = startPos;

        while (true)
        {
            rt.anchoredPosition += Vector2.left * scrollSpeed * Time.deltaTime;
            if (rt.anchoredPosition.x <= endPos.x) rt.anchoredPosition = startPos;
            yield return null;
        }
    }
}