using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class RecommendationFocus : MonoBehaviour, IPointerClickHandler
{
    [Header("Overlay (auto, wenn leer)")]
    public RectTransform overlay;
    public Color overlayColor = new Color(0, 0, 0, 0.55f);

    [Header("Focus")]
    public float margin = 96f;
    public float focusDuration = 0.28f;
    public float defocusDuration = 0.24f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool clickToDefocus = true;
    public float maxScale = 3.0f;        // Sicherheitslimit

    [Header("Inspector Test (Play Mode)")]
    public bool simulateFocus;
    public bool simulateDefocus;

    RectTransform _rt;
    Transform _origParent; int _origIndex;
    Vector2 _origAnchorMin, _origAnchorMax, _origPivot, _origAnchoredPos, _origSizeDelta;
    Vector3 _origLocalScale;
    GameObject _placeholder;
    Image _overlayImg;
    Canvas _canvas; Camera _cam;
    bool _focused; Coroutine _anim;
    static RecommendationFocus _active;

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
        if (simulateFocus)  { simulateFocus = false; Focus();   }
        if (simulateDefocus){ simulateDefocus = false; Defocus(); }
    }

    public void Focus()
    {
        if (_focused) return;
        if (_active && _active != this) _active.Defocus();
        _active = this;

        CreatePlaceholder();
        CacheOriginal();

        // Welt-Rect der Card vor dem Reparent in Overlay-Lokalraum umrechnen
        var startRectLocal = GetLocalRectInOverlay(_rt);

        // Jetzt in Overlay parenten (KEIN Welt-Pos behalten: false)
        _rt.SetParent(overlay, false);

        // zentrierte Anchors/Pivot
        _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
        _rt.pivot = new Vector2(0.5f, 0.5f);

        // Start-Position exakt dort, wo die Card im Overlay sichtbar wäre
        _rt.anchoredPosition = startRectLocal.center;
        _rt.sizeDelta = _origSizeDelta; // Größe bleibt, Scale übernimmt
        _rt.localScale = _origLocalScale; // 1:1 zum Start

        // Ziel-Skalierung: Weltgrößenvergleich im Overlay-Raum
        var overlaySize = overlay.rect.size;
        float targetW = Mathf.Max(1, overlaySize.x - 2 * margin);
        float targetH = Mathf.Max(1, overlaySize.y - 2 * margin);
        float scaleFactor = Mathf.Min(targetW / Mathf.Max(1, startRectLocal.width),
                                      targetH / Mathf.Max(1, startRectLocal.height));
        float startScale = _rt.localScale.x;
        float targetScale = Mathf.Min(startScale * scaleFactor, maxScale);

        // Ziel-Position: Mitte
        Vector2 startPos = _rt.anchoredPosition;
        Vector2 targetPos = Vector2.zero;

        FadeOverlay(0f, overlayColor.a, focusDuration);

        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Tween(focusDuration, t => {
            float k = ease.Evaluate(t);
            _rt.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, k);
            float s = Mathf.LerpUnclamped(startScale, targetScale, k);
            _rt.localScale = new Vector3(s, s, 1f);
        }, () => _focused = true));
    }

    public void Defocus()
    {
        if (!_placeholder) return;

        // Endzustand: zurück an Originalstelle/Layout
        FadeOverlay(_overlayImg ? _overlayImg.color.a : 0f, 0f, defocusDuration);

        Vector2 curPos = _rt.anchoredPosition;
        float curScale = _rt.localScale.x;

        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Tween(defocusDuration, t => {
            float k = ease.Evaluate(t);
            // während des Tweens belassen wir die Card im Overlay (glatte Animation)
            float s = Mathf.LerpUnclamped(curScale, _origLocalScale.x, k);
            _rt.localScale = new Vector3(s, s, 1f);
        }, () => {
            // nach dem Tween zurück in Original-Hierarchie
            _rt.SetParent(_origParent, false);
            _rt.anchorMin = _origAnchorMin; _rt.anchorMax = _origAnchorMax;
            _rt.pivot = _origPivot; _rt.anchoredPosition = _origAnchoredPos;
            _rt.sizeDelta = _origSizeDelta; _rt.localScale = _origLocalScale;

            RemovePlaceholder();
            _focused = false;
            if (_active == this) _active = null;
        }));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_focused && clickToDefocus) Defocus();
    }

    // ---------------- helpers

    void CacheOriginal()
    {
        _origParent = _rt.parent;
        _origIndex = _rt.GetSiblingIndex();
        _origAnchorMin = _rt.anchorMin; _origAnchorMax = _rt.anchorMax;
        _origPivot = _rt.pivot; _origAnchoredPos = _rt.anchoredPosition;
        _origSizeDelta = _rt.sizeDelta; _origLocalScale = _rt.localScale;
    }

    Rect GetLocalRectInOverlay(RectTransform source)
    {
        var corners = new Vector3[4];
        source.GetWorldCorners(corners);

        Vector2 ToLocal(Vector3 w)
        {
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(_cam, w);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, sp, _cam, out var lp);
            return lp;
        }

        Vector2 a = ToLocal(corners[0]); // bottom-left
        Vector2 b = ToLocal(corners[2]); // top-right
        Vector2 min = Vector2.Min(a, b);
        Vector2 max = Vector2.Max(a, b);
        return new Rect(min, max - min);
    }

    void EnsureOverlay()
    {
        if (overlay) { PrepareOverlayImage(); return; }

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
            var img = go.GetComponent<Image>();
            img.color = new Color(0,0,0,0);
            img.raycastTarget = true;
        }
        overlay = t;
        PrepareOverlayImage();
    }

    void PrepareOverlayImage()
    {
        _overlayImg = overlay.GetComponent<Image>();
        if (_overlayImg) _overlayImg.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 0f);
    }

    void FadeOverlay(float a0, float a1, float dur)
    {
        if (!_overlayImg) return;
        StartCoroutine(Tween(dur, t => {
            float a = Mathf.LerpUnclamped(a0, a1, ease.Evaluate(t));
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
        var size = _rt.rect.size;
        le.minWidth = le.preferredWidth = size.x;
        le.minHeight = le.preferredHeight = size.y;
    }

    void RemovePlaceholder()
    {
        if (_placeholder) Destroy(_placeholder);
        _placeholder = null;
    }
}
