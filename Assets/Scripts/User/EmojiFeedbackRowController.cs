using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Rocworks.Mqtt;
using App.UI;

[RequireComponent(typeof(RectTransform))]
public class EmojiFeedbackRowController : MonoBehaviour
{
    [Header("Topic")]
    public string group = "FEEDBACK";
    public AffectMetric metric = AffectMetric.ENJOYMENT;

    [Header("MQTT")]
    public MsgHandler msgHandler; // auto-find wenn leer

    [Header("Buttons (auto)")]
    public List<Button> buttons = new();
    public bool leftToRightLowToHigh = true;

    [Header("Visual Feedback (relative)")]
    [Tooltip("Faktor relativ zur ursprünglichen Button-Scale")]
    public float selectedMultiplier = 1.15f;   // z.B. 1.15 = +15%
    public float normalMultiplier   = 1.00f;   // immer 1.0 = unverändert
    [Range(0f,1f)] public float dimAlpha = 0.45f;
    [Range(0f,1f)] public float normalAlpha = 1.00f;
    public float tweenDuration = 0.12f;
    public AnimationCurve tweenCurve = AnimationCurve.EaseInOut(0,0,1,1);
    public bool useUnscaledTime = true;
    [Tooltip("Wenn noch nichts gewählt wurde: NICHT dimmen/vergrößern")]
    public bool keepNeutralUntilSelected = true;

    [Header("Optional: sofort AFFECT-Bar spiegeln (UI only)")]
    public bool mirrorToAffectBar = false;

    int _selectedIndex = -1;
    readonly Dictionary<int, Coroutine> _scaleTween = new();
    readonly Dictionary<int, Coroutine> _alphaTween = new();
    Vector3[] _baseScales;  // pro Button gemerkte Ursprungs-Scale

    void Awake()
    {
        if (!msgHandler) msgHandler = FindObjectOfType<MsgHandler>();
        AutoCollectButtonsIfEmpty();
        CacheBaseScales();
        WireClicks();
        RefreshVisuals();
    }

    void AutoCollectButtonsIfEmpty()
    {
        if (buttons.Count > 0) return;
        buttons.AddRange(GetComponentsInChildren<Button>(includeInactive: true));
        buttons.Sort((a,b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
    }

    void CacheBaseScales()
    {
        _baseScales = new Vector3[buttons.Count];
        for (int i = 0; i < buttons.Count; i++)
            _baseScales[i] = (buttons[i].transform as RectTransform).localScale;
    }

    void WireClicks()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            int idx = i;
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].onClick.AddListener(() => OnClicked(idx));
        }
    }

    void OnClicked(int idx)
    {
        _selectedIndex = idx;
        Publish(idx);
        if (mirrorToAffectBar) MirrorToBar(idx);
        RefreshVisuals();
    }

    void Publish(int idx)
    {
        int steps  = Mathf.Max(1, buttons.Count);
        int rating = leftToRightLowToHigh ? (idx + 1) : (steps - idx);

        string topic = TopicNamespace.TopicFor(group, metric.ToString());
        var jo = new JObject {
            ["metric"] = metric.ToString(),
            ["rating"] = rating,
            ["scale"]  = steps,
            ["ts"]     = DateTime.UtcNow.ToString("o"),
            ["mqtt"]   = true
        };
        try { msgHandler?.NotifyObservers(topic, jo); } catch { }
    }

    void MirrorToBar(int idx)
    {
        int steps  = Mathf.Max(1, buttons.Count);
        int rating = leftToRightLowToHigh ? (idx + 1) : (steps - idx);
        float value01 = (steps <= 1) ? 0f : (rating - 1f) / (steps - 1f);

        string topic = TopicNamespace.TopicFor("AFFECT", metric.ToString());
        var jo = new JObject { ["data"] = value01, ["mqtt"] = true };
        try { msgHandler?.NotifyObservers(topic, jo); } catch { }
    }

    public void RefreshVisuals()
    {
        bool neutral = keepNeutralUntilSelected && _selectedIndex < 0;

        for (int i = 0; i < buttons.Count; i++)
        {
            bool sel = !neutral && (i == _selectedIndex);
            float mul = sel ? selectedMultiplier : normalMultiplier;
            float targetAlpha = neutral ? normalAlpha : (sel ? normalAlpha : dimAlpha);

            var rt = buttons[i].transform as RectTransform;
            var baseScale = (i < _baseScales.Length && _baseScales[i] != Vector3.zero)
                            ? _baseScales[i] : Vector3.one;

            StartReplace(_scaleTween, i, TweenScale(rt, baseScale * mul));

            var cg = buttons[i].GetComponent<CanvasGroup>();
            if (cg) StartReplace(_alphaTween, i, TweenCanvasGroupAlpha(cg, targetAlpha));
            else {
                var g = buttons[i].targetGraphic as Graphic;
                if (g) StartReplace(_alphaTween, i, TweenGraphicAlpha(g, targetAlpha));
            }
        }
    }

    // ---- Tweens (relative) ----
    void StartReplace(Dictionary<int, Coroutine> dict, int key, IEnumerator co)
    {
        if (dict.TryGetValue(key, out var running) && running != null) StopCoroutine(running);
        dict[key] = StartCoroutine(co);
    }

    IEnumerator TweenScale(RectTransform rt, Vector3 target)
    {
        if (!Application.isPlaying || tweenDuration <= 0f)
        { rt.localScale = target; yield break; }

        Vector3 from = rt.localScale;
        if ((from - target).sqrMagnitude < 0.000001f) { rt.localScale = target; yield break; }

        float t = 0f;
        while (t < 1f)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t = Mathf.Clamp01(t + dt / tweenDuration);
            float e = tweenCurve?.Evaluate(t) ?? t;
            rt.localScale = Vector3.LerpUnclamped(from, target, e);
            yield return null;
        }
        rt.localScale = target;
    }

    IEnumerator TweenCanvasGroupAlpha(CanvasGroup cg, float target)
    {
        if (!Application.isPlaying || tweenDuration <= 0f)
        { cg.alpha = target; yield break; }

        float from = cg.alpha, t = 0f;
        if (Mathf.Abs(from - target) < 0.0001f) { cg.alpha = target; yield break; }

        while (t < 1f)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t = Mathf.Clamp01(t + dt / tweenDuration);
            float e = tweenCurve?.Evaluate(t) ?? t;
            cg.alpha = Mathf.LerpUnclamped(from, target, e);
            yield return null;
        }
        cg.alpha = target;
    }

    IEnumerator TweenGraphicAlpha(Graphic g, float target)
    {
        if (!Application.isPlaying || tweenDuration <= 0f)
        { SetAlpha(g, target); yield break; }

        float from = g.color.a, t = 0f;
        if (Mathf.Abs(from - target) < 0.0001f) { SetAlpha(g, target); yield break; }

        while (t < 1f)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t = Mathf.Clamp01(t + dt / tweenDuration);
            float e = tweenCurve?.Evaluate(t) ?? t;
            SetAlpha(g, Mathf.LerpUnclamped(from, target, e));
            yield return null;
        }
        SetAlpha(g, target);
    }

    static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

    // Optional programmatisch setzen
    public void SelectRating(int rating1toN)
    {
        int steps = Mathf.Max(1, buttons.Count);
        int idx = leftToRightLowToHigh ? (rating1toN - 1) : (steps - rating1toN);
        _selectedIndex = Mathf.Clamp(idx, 0, steps - 1);
        RefreshVisuals();
    }
}
