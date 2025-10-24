using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class ScoreRawController : MonoBehaviour, IObserver
{
    [SerializeField] private Text scoreVisualization;

    [Header("Topic")]
    [SerializeField] private string group = "EVALUATION";
    [SerializeField] private string category = "TOTALSCORE"; // .../EVALUATION/TOTALSCORE

    private MsgHandler msgHandler;
    private string _topic;

    private Coroutine animationCoroutine;
    private float currentScore = 0f;
    private float targetScore  = 0f;

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

        if (!TryParseScore(s, out targetScore)) return;

        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimateScore());
    }

    bool TryParseScore(string json, out float score)
    {
        score = 0f;
        try
        {
            var tok = JToken.Parse(json);
            if (tok["data"] != null) // { "data": 12.34 } oder { "data": { "score": 12.34 } }
            {
                var d = tok["data"];
                if (d.Type == JTokenType.Float || d.Type == JTokenType.Integer) { score = d.Value<float>(); return true; }
                if (d["score"] != null) { score = d["score"].Value<float>(); return true; }
            }
            if (tok["score"] != null) { score = tok["score"].Value<float>(); return true; }
        }
        catch { }
        return false;
    }

    void ResetUI() { currentScore = targetScore = 0f; UpdateScoreText(); }
    void UpdateScoreText() { if (scoreVisualization) scoreVisualization.text = $"{currentScore:F2}"; }

    IEnumerator AnimateScore()
    {
        float duration = 1.0f, t = 0f;
        float start = currentScore;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            currentScore = Mathf.Lerp(start, targetScore, t);
            UpdateScoreText();
            yield return null;
        }
        currentScore = targetScore;
        UpdateScoreText();
    }
}
