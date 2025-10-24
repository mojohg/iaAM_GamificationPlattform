#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(ScrollRect))]
public class PagerEditorPreview : MonoBehaviour
{
    public int pageIndex = 0;      // welche Page im Editor zeigen
    public bool liveUpdate = true; // bei Inspector-Änderung sofort updaten

    ScrollRect _sr;

    void OnEnable() { _sr = GetComponent<ScrollRect>(); Apply(); }
    void OnValidate() { Apply(); }
    void Update() { if (!Application.isPlaying && liveUpdate) Apply(); }

    void Apply()
    {
        if (Application.isPlaying || _sr == null || _sr.content == null) return;
        int count = _sr.content.childCount;
        if (count <= 1) { _sr.horizontalNormalizedPosition = 0f; return; }

        pageIndex = Mathf.Clamp(pageIndex, 0, count - 1);
        float step = 1f / (count - 1);
        _sr.horizontalNormalizedPosition = step * pageIndex;
    }
}
#endif
