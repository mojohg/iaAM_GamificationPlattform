using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public interface IPageLifecycle { void OnPageShown(); void OnPageHidden(); }

[RequireComponent(typeof(ScrollRect))]
public class ScrollSnapNormalized : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Header("Tuning")]
    public float snapDuration = 0.25f;            // Einrast-Zeit
    public float swipeVelocityThreshold = 500f;   // px/s: starker Wisch erzwingt Seitenwechsel
    public float minStepRatio = 0.15f;            // Anteil eines "Seiten-Schritts" zum Wechseln (~15%)

    [Header("Auto refs")]
    public ScrollRect scrollRect;
    public RectTransform viewport;
    public RectTransform content;

    public int CurrentPage { get; private set; } = 0;
    Coroutine _snapRoutine;
    Vector2 _dragStartPos;
    float _dragStartTime;

    void Reset()
    {
        scrollRect = GetComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        viewport = scrollRect.viewport;
        content  = scrollRect.content;
    }

    void Awake()
    {
        if (!scrollRect) scrollRect = GetComponent<ScrollRect>();
        if (!viewport)   viewport   = scrollRect.viewport;
        if (!content)    content    = scrollRect.content;
    }

    void OnEnable()
    {
        // Start links (Seite 0)
        SetNormalizedImmediate(0f);
        SetPage(0);
    }

    // --- Drag API
    public void OnBeginDrag(PointerEventData e)
    {
        _dragStartPos  = content.anchoredPosition;
        _dragStartTime = Time.unscaledTime;
        if (_snapRoutine != null) StopCoroutine(_snapRoutine);
    }

    public void OnEndDrag(PointerEventData e)
    {
        float dt = Mathf.Max(0.01f, Time.unscaledTime - _dragStartTime);
        float dx = content.anchoredPosition.x - _dragStartPos.x; // px
        float vx = dx / dt; // px/s

        int pageCount = Mathf.Max(1, content.childCount);
        if (pageCount == 1) { GoToPage(0); return; }

        float step = 1f / (pageCount - 1);                 // norm. Strecke pro Seite
        float hn   = scrollRect.horizontalNormalizedPosition;
        float curTarget = CurrentPage * step;
        int target = CurrentPage;

        // 1) schneller Wisch erzwingt Seitenwechsel
        if (Mathf.Abs(vx) > swipeVelocityThreshold)
            target += (vx < 0f) ? 1 : -1;
        else
        {
            // 2) sonst: wenn wir weit genug vom aktuellen Ziel weg sind → nächste Seite,
            //    andernfalls nimm die nächste "nahe" Seite per Rundung
            float delta = hn - curTarget;
            if (Mathf.Abs(delta) > step * minStepRatio)
                target += (delta > 0f) ? 1 : -1;
            else
                target = Mathf.RoundToInt(hn / step);
        }

        target = Mathf.Clamp(target, 0, pageCount - 1);
        GoToPage(target);
    }

    // --- Public
    public void GoToPage(int page)
    {
        int pageCount = Mathf.Max(1, content.childCount);
        page = Mathf.Clamp(page, 0, pageCount - 1);
        float step = (pageCount == 1) ? 0f : 1f / (pageCount - 1);
        float targetNorm = step * page;

        if (_snapRoutine != null) StopCoroutine(_snapRoutine);
        _snapRoutine = StartCoroutine(LerpTo(targetNorm));
    }

    // --- Intern
    IEnumerator LerpTo(float target)
    {
        scrollRect.inertia = false;
        scrollRect.velocity = Vector2.zero;

        float start = scrollRect.horizontalNormalizedPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / snapDuration;
            float v = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            SetNormalizedImmediate(v);
            yield return null;
        }
        scrollRect.inertia = true;

        int pageCount = Mathf.Max(1, content.childCount);
        int newPage = (pageCount == 1) ? 0 : Mathf.RoundToInt(target * (pageCount - 1));
        SetPage(newPage);
    }

    void SetNormalizedImmediate(float v)
    {
        scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(v);
    }

    void SetPage(int newPage)
    {
        if (newPage == CurrentPage) return;
        // Lifecycle optional
        if (CurrentPage >= 0 && CurrentPage < content.childCount)
            content.GetChild(CurrentPage).GetComponent<IPageLifecycle>()?.OnPageHidden();

        CurrentPage = newPage;

        if (CurrentPage >= 0 && CurrentPage < content.childCount)
            content.GetChild(CurrentPage).GetComponent<IPageLifecycle>()?.OnPageShown();
    }
}
