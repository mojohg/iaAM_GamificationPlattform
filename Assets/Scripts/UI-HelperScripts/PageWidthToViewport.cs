using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class PageSizer : MonoBehaviour
{
    private LayoutElement _le;
    private RectTransform _viewport;

    void OnEnable()
    {
        // LayoutElement sicherstellen
        _le = GetComponent<LayoutElement>();
        if (_le == null) _le = gameObject.AddComponent<LayoutElement>();

        // Viewport automatisch aus nächstem ScrollRect holen (fallback: Parent)
        var sr = GetComponentInParent<ScrollRect>();
        _viewport = sr != null ? sr.viewport : transform.parent as RectTransform;

        Canvas.willRenderCanvases += UpdateSize;
        UpdateSize();
    }

    void OnDisable()
    {
        Canvas.willRenderCanvases -= UpdateSize;
    }

    void UpdateSize()
    {
        if (_viewport == null || _le == null) return;

        float w = _viewport.rect.width;
        float h = _viewport.rect.height;

        if (!Mathf.Approximately(_le.preferredWidth, w) ||
            !Mathf.Approximately(_le.preferredHeight, h))
        {
            _le.preferredWidth  = w;
            _le.preferredHeight = h;
            // Content neu aufbauen
            var parent = transform.parent as RectTransform;
            if (parent) LayoutRebuilder.MarkLayoutForRebuild(parent);
        }
    }
}
