using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class RecommendationSpotlight : MonoBehaviour, IPointerClickHandler
{
    [Header("Overlay (auto, wenn leer)")]
    public RectTransform overlay;
    public Color overlayColor = new Color(0,0,0,0.55f);
    public bool clickToDefocus = true;

    [Header("Focus Tuning")]
    public float margin = 96f;            // Rand um die fokussierte Card
    public float focusDuration = 0.28f;
    public float defocusDuration = 0.24f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);

    [Header("Inspector Test (Play Mode)")]
    public bool simulateFocus;
    public bool simulateDefocus;

    // intern
    RectTransform _rt;
    Transform _origParent; int _origIndex;
    Vector2 _origAnchorMin, _origAnchorMax, _origPivot, _origAnchoredPos, _origSizeDelta;
    GameObject _placeholder;
    Canvas _canvas; Camera _cam; Image _overlayImg;
    Coroutine _anim; bool _focused;

    static RecommendationSpotlight _active; // nur eine gleichzeitig

    void Awake()
    {
        _rt = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
        _cam = _canvas && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        EnsureOverlay();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (simulateFocus)  { simulateFocus  = false; Focus();   }
        if (simulateDefocus){ simulateDefocus= false; Defocus(); }
    }

    public void Focus()
    {
        if (_focused) return;
        if (_active && _active != this) _active.Defocus();
        _active = this;

        // Platz im Layout halten
        CreatePlaceholder();

        // Originalzustand
        _origParent = _rt.parent; _origIndex = _rt.GetSiblingIndex();
        _origAnchorMin = _rt.anchorMin; _origAnchorMax = _rt.anchorMax;
        _origPivot = _rt.pivot; _origAnchoredPos = _rt.anchoredPosition;
        _origSizeDelta = _rt.sizeDelta;

        // Start-Rect in Overlay-Koordinaten
        Rect startRectLocal = WorldRectToOverlayLocal(_rt);

        // an Overlay hängen (kein Weltpos behalten)
        _rt.SetParent(overlay, false);
        _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
        _rt.pivot = new Vector2(0.5f, 0.5f);

        // Startwerte setzen (Position/Größe im Overlay)
        _rt.anchoredPosition = startRectLocal.center;
        _rt.sizeDelta = startRectLocal.size;

        // Zielgröße berechnen (mit margin) – Seitenverhältnis beibehalten
        Vector2 ov = overlay.rect.size;
        float maxW = Mathf.Max(1, ov.x - 2f * margin);
        float maxH = Mathf.Max(1, ov.y - 2f * margin);
        float s = Mathf.Min(maxW / Mathf.Max(1, startRectLocal.width),
                            maxH / Mathf.Max(1, startRectLocal.height));
        Vector2 targetSize = startRectLocal.size * s;
        Vector2 targetPos  = Vector2.zero;

        // Overlay einblenden
        FadeOverlay(0f, overlayColor.a, focusDuration);

        // Tween der Größe + Position
        if (_anim != null) StopCoroutine(_anim);
        Vector2 startSize = _rt.sizeDelta;
        Vector2 startPos  = _rt.anchoredPosition;
        _anim = StartCoroutine(Tween(focusDuration, k =>
        {
            float t = ease.Evaluate(k);
            _rt.sizeDelta       = Vector2.LerpUnclamped(startSize, targetSize, t);
            _rt.anchoredPosition= Vector2.LerpUnclamped(startPos, targetPos, t);
        }, () => _focused = true));
    }

    public void Defocus()
    {
        if (!_placeholder) return;

        // sanft Overlay ausblenden
        FadeOverlay(_overlayImg ? _overlayImg.color.a : 0f, 0f, defocusDuration);

        // zurück in Original-Hierarchie/Größe
        if (_anim != null) StopCoroutine(_anim);
        Vector2 curSize = _rt.sizeDelta;

        _anim = StartCoroutine(Tween(defocusDuration, k =>
        {
            float t = ease.Evaluate(k);
            _rt.sizeDelta = Vector2.LerpUnclamped(curSize, _origSizeDelta, t);
        },
        () =>
        {
            _rt.SetParent(_origParent, false);
            _rt.anchorMin = _origAnchorMin; _rt.anchorMax = _origAnchorMax;
            _rt.pivot = _origPivot; _rt.anchoredPosition = _origAnchoredPos;
            _rt.sizeDelta = _origSizeDelta;
            RemovePlaceholder();
            _focused = false;
            if (_active == this) _active = null;
        }));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_focused && clickToDefocus) Defocus();
    }

    // --------- helpers

    Rect WorldRectToOverlayLocal(RectTransform source)
    {
        Vector3[] wc = new Vector3[4];
        source.GetWorldCorners(wc);
        Vector2 ToLocal(Vector3 w)
        {
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(_cam, w);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, sp, _cam, out var lp);
            return lp;
        }
        Vector2 bl = ToLocal(wc[0]); // bottom-left
        Vector2 tr = ToLocal(wc[2]); // top-right
        Vector2 min = Vector2.Min(bl, tr);
        Vector2 size = Vector2.Max(tr - bl, Vector2.one);
        return new Rect(min, size);
    }

    void EnsureOverlay()
    {
        if (overlay) { _overlayImg = overlay.GetComponent<Image>(); if (_overlayImg) _overlayImg.color = new Color(0,0,0,0); return; }

        var canvas = _canvas ? _canvas : GetComponentInParent<Canvas>();
        if (!canvas) return;

        var t = canvas.transform.Find("FocusOverlay") as RectTransform;
        if (!t)
        {
            var go = new GameObject("FocusOverlay", typeof(RectTransform), typeof(Image));
            t = go.GetComponent<RectTransform>();
            t.SetParent(canvas.transform, false);
            t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one;
            t.offsetMin = t.offsetMax = Vector2.zero;
            t.SetAsLastSibling();
            var img = go.GetComponent<Image>(); img.color = new Color(0,0,0,0); img.raycastTarget = true;
        }
        overlay = t; _overlayImg = overlay.GetComponent<Image>();
    }

    void FadeOverlay(float a0, float a1, float dur)
    {
        if (!_overlayImg) return;
        StartCoroutine(Tween(dur, k =>
        {
            float a = Mathf.LerpUnclamped(a0, a1, ease.Evaluate(k));
            var c = overlayColor; c.a = a; _overlayImg.color = c;
        }));
    }

    IEnumerator Tween(float dur, System.Action<float> step, System.Action done = null)
    {
        if (dur <= 0f) { step?.Invoke(1f); done?.Invoke(); yield break; }
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            step?.Invoke(Mathf.Clamp01(t));
            yield return null;
        }
        done?.Invoke();
    }

    void CreatePlaceholder()
    {
        if (_placeholder) return;
        _placeholder = new GameObject(name + "_placeholder", typeof(LayoutElement));
        var ph = _placeholder.GetComponent<RectTransform>();
        ph.SetParent(_rt.parent, false);
        ph.SetSiblingIndex(_rt.GetSiblingIndex());
        var le = _placeholder.GetComponent<LayoutElement>();
        var sz = _rt.rect.size;
        le.minWidth = le.preferredWidth = sz.x;
        le.minHeight= le.preferredHeight= sz.y;
    }

    void RemovePlaceholder()
    {
        if (_placeholder) Destroy(_placeholder);
        _placeholder = null;
    }
}
