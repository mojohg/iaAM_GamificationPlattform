using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class RectNudge : MonoBehaviour
{
    [Header("Offset relativ zur Originalposition (Pixel)")]
    public Vector2 offset = new Vector2(12f, 0f);   // +X = rechts, +Y = hoch

    [Header("Steuerung")]
    public bool applyOnEnable = true;   // beim Aktivieren anwenden (animiert)
    public bool simulateApply;          // Inspector-Button (Editor/Play)
    public bool simulateReset;          // Inspector-Button (Editor/Play)

    [Header("Animation")]
    public float inDuration  = 0.25f;   // Zeit für Einfahren (zu Original+Offset)
    public float outDuration = 0.20f;   // Zeit für Zurückfahren (zu Original)
    public AnimationCurve inCurve  = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve outCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool useUnscaledTime = true; // UI meist ohne TimeScale

    RectTransform _rt;
    Vector2 _originalPos;
    bool _saved;
    Coroutine _tween;

    void OnEnable()
    {
        _rt = (RectTransform)transform;
        if (!_saved)
        {
            _originalPos = _rt.anchoredPosition;
            _saved = true;
        }
        if (applyOnEnable) Apply(false); // animiert
    }

    void Update()
    {
        // Inspector-Toggles
        if (simulateApply) { simulateApply = false; Apply(false); }
        if (simulateReset) { simulateReset = false; ResetPosition(false); }
    }

    [ContextMenu("Apply Offset (animated)")]
    public void Apply() => Apply(false);

    [ContextMenu("Reset (animated)")]
    public void ResetPosition() => ResetPosition(false);

    /// <summary>Fährt zur Originalposition + Offset.</summary>
    public void Apply(bool instant)
    {
        EnsureRt();
        if (!_saved) { _originalPos = _rt.anchoredPosition; _saved = true; }
        var target = _originalPos + offset;
        StartTween(_rt.anchoredPosition, target, instant ? 0f : inDuration, inCurve);
    }

    /// <summary>Fährt zurück zur Originalposition.</summary>
    public void ResetPosition(bool instant)
    {
        EnsureRt();
        StartTween(_rt.anchoredPosition, _originalPos, instant ? 0f : outDuration, outCurve);
    }

    // ---- intern ----
    void EnsureRt()
    {
        if (!_rt) _rt = (RectTransform)transform;
    }

    void StartTween(Vector2 from, Vector2 to, float duration, AnimationCurve curve)
    {
        // Im Editor (nicht Play) animieren Coroutines nicht → direkt setzen
        if (!Application.isPlaying || duration <= 0f)
        {
            if (_tween != null) { StopCoroutine(_tween); _tween = null; }
            _rt.anchoredPosition = to;
            return;
        }
        if (_tween != null) StopCoroutine(_tween);
        _tween = StartCoroutine(TweenCo(from, to, duration, curve));
    }

    System.Collections.IEnumerator TweenCo(Vector2 from, Vector2 to, float duration, AnimationCurve curve)
    {
        float t = 0f;
        while (t < 1f)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt / Mathf.Max(0.0001f, duration);
            float e = curve != null ? curve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);
            _rt.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
            yield return null;
        }
        _rt.anchoredPosition = to;
        _tween = null;
    }
}
