using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class CardVisualSpotlight : MonoBehaviour, IPointerClickHandler
{
    [Header("Overlay (wird auto erstellt, wenn leer)")]
    public RectTransform overlay;
    public Color overlayColor = new Color(0, 0, 0, 0.55f);

    [Header("Focus-Effekt")]
    public float margin = 96f;                 // Rand um die fokussierte Karte
    public float focusDuration = 0.28f;
    public float defocusDuration = 0.24f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool clickToDefocus = true;

    [Header("Inspector-Test (Play Mode)")]
    public bool simulateFocus;
    public bool simulateDefocus;

    // intern
    RectTransform _rt;
    RectTransform _origParent; int _origIndex;
    Image _overlayImg;
    Canvas _canvas; Camera _cam;
    bool _focused; Coroutine _anim;

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
        _focused = true;

        // Original-Parent (CardRoot) + Index merken
        _origParent = (RectTransform)_rt.parent;
        _origIndex  = _rt.GetSiblingIndex();

        // Welt-Rect der CardVisual in Overlay-Lokalkoordinaten umrechnen
        Rect startRect = WorldRectToOverlayLocal(_rt);

        // In Overlay hängen (ohne Weltpos übernehmen)
        _rt.SetParent(overlay, false);
        _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
        _rt.pivot = new Vector2(0.5f, 0.5f);

        // Startwerte im Overlay
        _rt.anchoredPosition = startRect.center;
        _rt.sizeDelta = startRect.size;

        // Zielgröße (mit margin), Seitenverhältnis beibehalten
        Vector2 ov = overlay.rect.size;
        float maxW = Mathf.Max(1, ov.x - 2 * margin);
        float maxH = Mathf.Max(1, ov.y - 2 * margin);
        float scale = Mathf.Min(maxW / Mathf.Max(1, startRect.width),
                                maxH / Mathf.Max(1, startRect.height));
        Vector2 targetSize = startRect.size * scale;
        Vector2 targetPos  = Vector2.zero;

        FadeOverlay(0f, overlayColor.a, focusDuration);

        if (_anim != null) StopCoroutine(_anim);
        Vector2 s0 = _rt.sizeDelta, p0 = _rt.anchoredPosition;
        _anim = StartCoroutine(Tween(focusDuration, t =>
        {
            float k = ease.Evaluate(t);
            _rt.sizeDelta        = Vector2.LerpUnclamped(s0, targetSize, k);
            _rt.anchoredPosition = Vector2.LerpUnclamped(p0, targetPos, k);
        }));
    }

    public void Defocus()
    {
        if (!_focused) return;
        _focused = false;

        FadeOverlay(_overlayImg ? _overlayImg.color.a : 0f, 0f, defocusDuration);

        if (_anim != null) StopCoroutine(_anim);
        // Zurück in den Card-Slot: Stretch füllt den Slot automatisch
        StartCoroutine(Tween(defocusDuration, null, () =>
        {
            _rt.SetParent(_origParent, false);
            _rt.anchorMin = Vector2.zero; _rt.anchorMax = Vector2.one;
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.anchoredPosition = Vector2.zero;
            _rt.sizeDelta = Vector2.zero;
            _rt.SetSiblingIndex(_origIndex);
        }));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_focused && clickToDefocus) Defocus();
    }

    // ---------- helpers

    Rect WorldRectToOverlayLocal(RectTransform src)
    {
        var wc = new Vector3[4];
        src.GetWorldCorners(wc);

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
        if (overlay)
        {
            _overlayImg = overlay.GetComponent<Image>();
            if (_overlayImg) _overlayImg.color = new Color(0,0,0,0);
            return;
        }

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
            img.raycastTarget = true; // blockt Hintergrund
        }
        overlay = t;
        _overlayImg = overlay.GetComponent<Image>();
    }

    void FadeOverlay(float a0, float a1, float dur)
    {
        if (!_overlayImg) return;
        StartCoroutine(Tween(dur, x =>
        {
            float a = Mathf.LerpUnclamped(a0, a1, ease.Evaluate(x));
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
}
