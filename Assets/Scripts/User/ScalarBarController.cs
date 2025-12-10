using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;
using App.UI;

public enum AffectMetric { ENJOYMENT, DIFFICULTY, EFFORT, MOTIVATION }

[RequireComponent(typeof(RectTransform))]
public class ScalarBarController : MonoBehaviour, IObserver
{
    [Header("Topic")]
    public string group = "AFFECT";
    public AffectMetric metric = AffectMetric.ENJOYMENT;

    [Header("Refs")]
    public MsgHandler msgHandler;     // optional auto-find
    public Image fill;                // Image.type = Filled
    public TMP_Text valueLabel;       // optional

    [Header("Animation")]
    public float duration = 0.35f;
    public AnimationCurve curve = AnimationCurve.EaseInOut(0,0,1,1);
    public bool useUnscaledTime = true;

    [Header("Coloring")]
    public bool colorize = true;
    public bool invertForColor = false;      // falls z. B. DIFFICULTY hoch = schlecht
    public Gradient customGradient;          // optional: wenn gesetzt, wird dieser genutzt
    [Range(0f, 1f)] public float hsvSaturation = 1f;
    [Range(0f, 1f)] public float hsvValue = 1f;

    string _topic;
    float _from, _to, _t;
    bool _anim;

    void Awake()
    {
        if (!msgHandler) msgHandler = FindObjectOfType<MsgHandler>();
        if (fill) fill.fillAmount = Mathf.Clamp01(fill.fillAmount);
    }

    void OnEnable()
    {
        _topic = TopicNamespace.TopicFor(group, metric.ToString());
        msgHandler?.RegisterObserver(_topic, this);

        // NEU: Auf Root-Änderungen lauschen
        TopicNamespace.OnRootChanged += HandleRootChanged;

        // sofortige Erstfärbung aus aktuellem Fill (falls schon gesetzt)
        if (fill) UpdateColor(fill.fillAmount);
    }


    void OnDisable()
    {
        msgHandler?.RemoveObserver(_topic, this);
        
        // NEU: Listener wieder abmelden
        TopicNamespace.OnRootChanged -= HandleRootChanged;
    }

    public void OnNotify(string topic, object data)
    {
        if (topic != _topic) return;
        if (!TryReadValue(data as string, out var v)) return;
        SetTarget(v);
    }

    void Update()
    {
        // Animation der Füllhöhe
        if (_anim && duration > 0f && fill != null)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _t = Mathf.Clamp01(_t + dt / duration);
            float e = curve != null ? curve.Evaluate(_t) : _t;
            float val = Mathf.LerpUnclamped(_from, _to, e);
            ApplyValue(val);
            if (_t >= 1f) _anim = false;
        }

        // Färbe auf Basis des aktuellen Fill stets nach
        // => funktioniert auch, wenn der FillAmount extern/Inspector geändert wird
        if (colorize && fill != null) UpdateColor(fill.fillAmount);
    }

    void SetTarget(float v)
    {
        v = Mathf.Clamp01(v);
        _from = fill ? fill.fillAmount : 0f;
        _to = v;
        _t = 0f;
        _anim = duration > 0f;
        if (!_anim) ApplyValue(v);
    }

    void ApplyValue(float v)
    {
        if (fill) fill.fillAmount = Mathf.Clamp01(v);
        if (valueLabel) valueLabel.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    void UpdateColor(float valForFill01)
    {
        if (!colorize || fill == null) return;

        // Nur die FARBE invertieren (nicht den Fillstand!)
        float vColor = invertForColor ? 1f - valForFill01 : valForFill01;

        var c = EvaluateColor(vColor);

        // Alpha des bestehenden Tints erhalten (falls Sprite halbtransparent)
        var cur = fill.color;
        fill.color = new Color(c.r, c.g, c.b, cur.a);
    }

    Color EvaluateColor(float v01)
    {
        v01 = Mathf.Clamp01(v01);
        // 1) Benutzerdefinierter Gradient bevorzugt
        if (customGradient != null && customGradient.colorKeys != null && customGradient.colorKeys.Length > 0)
            return customGradient.Evaluate(v01);

        // 2) Standard: HSV-Gradient (0=rot -> 0.5=gelb -> 1=grün)
        // Hue 0.0 (rot) bis 0.333... (grün). Yellow liegt bei ~0.1666.
        float hue = Mathf.Lerp(0f, 1f / 3f, v01);
        return Color.HSVToRGB(hue, hsvSaturation, hsvValue);
    }

    static bool TryReadValue(string payload, out float value01)
    {
        value01 = 0f;
        if (string.IsNullOrWhiteSpace(payload)) return false;
        var s = payload.Trim();
        Debug.Log($"[ScalarBarController] Received payload: {s}");

        if (s.StartsWith("{"))
        {
            try {
                var jo = JObject.Parse(s);
                var tok = jo["data"];
                if (tok == null) return false;

                if (tok.Type == JTokenType.Float || tok.Type == JTokenType.Integer)
                {
                    var n = tok.Value<float>();
                    value01 = (n <= 1.0f) ? Mathf.Clamp01(n) : Mathf.Clamp01((n - 1f) / 4f); // erlaubt auch 1..5
                    return true;
                }
                if (tok.Type == JTokenType.String && float.TryParse((string)tok, out var f))
                {
                    value01 = Mathf.Clamp01(f);
                    return true;
                }
            } catch { return false; }
        }
        return false;
    }
    /// <summary>
    /// Aktualisiert die lokale Topic-Variable, wenn sich der Root ändert.
    /// </summary>
    private void HandleRootChanged(string oldRoot, string newRoot)
    {
        // WICHTIG: Wir müssen uns NICHT beim MsgHandler an- oder abmelden.
        // Das macht der MsgHandler (mit RemapObserverTopics) bereits für uns.
        // Wir müssen nur unsere LOKALE Variable _topic aktualisieren,
        // damit der Filter in OnNotify() und der Abmelde-Aufruf in OnDisable()
        // den korrekten (neuen) Topic-String verwenden.
        
        _topic = TopicNamespace.TopicFor(group, metric.ToString());
        Debug.Log($"[ScalarBarController] Topic remapped to: {_topic}");
    }
}
