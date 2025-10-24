using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class MarqueeText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Refs")]
    public RectTransform viewport;     // = Ticker (fallback: this)
    public RectTransform content;      // = Content (fallback: parent von templateLabel)
    public TMP_Text templateLabel;     // = Label (TMP)

    [Header("Behavior")]
    public float speed = 90f;          // px/s
    public float gap = 40f;            // Abstand zwischen Wiederholungen
    public bool pauseOnHover = true;
    public bool useUnscaledTime = false;

    TMP_Text _labelA, _labelB;
    float _widthA, _widthB;
    bool _paused, _active = true, _ready = false;

    void OnEnable()
    {
        _active = true;
        Build();
    }

    void OnDisable()
    {
        _active = false;
        _ready = false;
    }

    /// <summary>Text setzen (kannst du jederzeit aufrufen).</summary>
    public void SetText(string t)
    {
        if (!templateLabel) return;
        templateLabel.text = t ?? string.Empty;
        Build();
    }

    void Build()
    {
        _ready = false;

        if (!viewport) viewport = (RectTransform)transform;
        if (!templateLabel) { _labelA = _labelB = null; return; }
        if (!content) content = templateLabel.rectTransform.parent as RectTransform;
        if (!content || !viewport) { _labelA = _labelB = null; return; }

        // alte Clones löschen (PlayMode: Destroy, nicht DestroyImmediate)
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);
            if (child != templateLabel.rectTransform) Destroy(child.gameObject);
        }

        _labelA = templateLabel;
        PrepareLabel(_labelA);
        _labelA.ForceMeshUpdate();
        _widthA = Mathf.Ceil(_labelA.preferredWidth);

        // Kein Text → keine Schleife
        if (_widthA <= 0f)
        {
            CenterSingle(_labelA.rectTransform);
            _labelB = null;
            return;
        }

        // Immer einen Clone für die Endlosschleife erzeugen
        _labelB = Instantiate(_labelA, content);
        _labelB.name = _labelA.name + "_clone";
        PrepareLabel(_labelB);
        _labelB.ForceMeshUpdate();
        _widthB = Mathf.Ceil(_labelB.preferredWidth);

        // Start-Positionen (links ausgerichtet)
        SetLeft(_labelA.rectTransform, 0f);
        SetLeft(_labelB.rectTransform, _widthA + gap);
        _labelB.gameObject.SetActive(true);

        _ready = true;
    }

    void Update()
    {
        if (!_active || !_ready || _paused) return;
        if (_labelA == null || _labelB == null) return; // extra safety

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float dx = -speed * dt;

        var ra = _labelA.rectTransform;
        var rb = _labelB.rectTransform;

        ra.anchoredPosition += new Vector2(dx, 0);
        rb.anchoredPosition += new Vector2(dx, 0);

        // Loop: Wenn Label komplett links raus ist, rechts hinter das andere setzen
        if (ra.anchoredPosition.x + _widthA < 0f)
            SetLeft(ra, rb.anchoredPosition.x + _widthB + gap);
        if (rb.anchoredPosition.x + _widthB < 0f)
            SetLeft(rb, ra.anchoredPosition.x + _widthA + gap);
    }

    void PrepareLabel(TMP_Text lbl)
    {
        var rt = lbl.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        lbl.enableWordWrapping = false;           // Lauftext in einer Zeile
        lbl.overflowMode = TextOverflowModes.Overflow;
    }

    static void SetLeft(RectTransform rt, float x)
    {
        var p = rt.anchoredPosition; p.x = x; rt.anchoredPosition = p;
    }

    static void CenterSingle(RectTransform rt)
    {
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    public void OnPointerEnter(PointerEventData eventData) { if (pauseOnHover) _paused = true; }
    public void OnPointerExit (PointerEventData eventData) { if (pauseOnHover) _paused = false; }
}
