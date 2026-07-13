using System;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

[RequireComponent(typeof(ScrollRect))]
public class PageUsageTracker : MonoBehaviour
{
    [Header("Paging (nur Beobachtung)")]
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("Optional: explizite IDs, sonst werden die Child-Namen des Contents verwendet.")]
    [SerializeField] private string[] pageIds;

    [Header("UI-Kategorien (per Button toggelbar)")]
    [SerializeField] private GameObject[] uiSections; // z.B. HUD, Topbar, XP, Debug
    [Header("Conditional Background")]
    [SerializeField] private GameObject progressChallengeBackground;

    [Header("MQTT")]
    [SerializeField] private MsgHandler msgHandler;
    [SerializeField] private string baseTopic = "GAMING/USAGE";

    private string TopicPage => TopicNamespace.TopicFor(baseTopic + "/PAGE");
    private string TopicUI   => TopicNamespace.TopicFor(baseTopic + "/UI");

    // intern
    private int pageCount = 0;
    private int currentPageIndex = -1;

    private void Awake()
    {
        if (!scrollRect)
            scrollRect = GetComponent<ScrollRect>();

        if (!msgHandler)
            msgHandler = FindObjectOfType<MsgHandler>();
    }

    private void Start()
    {
        InitPages();
        // initiale Page ermitteln & einmal senden
        UpdateProgressChallengeBackground();
        UpdateCurrentPage(force: true);
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        UpdateCurrentPage(force: false);
    }

    // ---------------------------------------------------------
    // Seiten beobachten
    // ---------------------------------------------------------
    private void InitPages()
    {
        if (!scrollRect || !scrollRect.content)
        {
            Debug.LogError("[PageUsageTracker] ScrollRect oder Content fehlt.");
            return;
        }

        pageCount = scrollRect.content.childCount;

        if (pageCount <= 0)
        {
            Debug.LogWarning("[PageUsageTracker] Keine Pages im Content gefunden.");
            return;
        }

        // Page-IDs fallback: Child-Namen
        if (pageIds == null || pageIds.Length != pageCount)
        {
            pageIds = new string[pageCount];
            for (int i = 0; i < pageCount; i++)
            {
                var child = scrollRect.content.GetChild(i);
                pageIds[i] = child ? child.name : $"Page_{i}";
            }
        }
    }

    private void UpdateCurrentPage(bool force)
    {
        if (pageCount <= 0 || scrollRect == null)
            return;

        // horizontalNormalizedPosition 0..1 → Pageindex 0..(count-1)
        float pos = scrollRect.horizontalNormalizedPosition;
        int newIndex = Mathf.RoundToInt(pos * (pageCount - 1));
        newIndex = Mathf.Clamp(newIndex, 0, pageCount - 1);

        if (!force && newIndex == currentPageIndex)
            return;

        currentPageIndex = newIndex;
        SendPageEnterEvent();
    }

    private void SendPageEnterEvent()
    {
        if (msgHandler == null || currentPageIndex < 0 || currentPageIndex >= pageCount)
            return;

        string pageId = pageIds[currentPageIndex];

        // UI-State mitgeben (welche Kategorien sind gerade sichtbar?)
        var uiArray = new JArray();
        if (uiSections != null)
        {
            for (int i = 0; i < uiSections.Length; i++)
            {
                var go = uiSections[i];
                if (!go) continue;

                var uiObj = new JObject
                {
                    ["index"] = i,
                    ["name"] = go.name,
                    ["active"] = go.activeSelf
                };
                uiArray.Add(uiObj);
            }
        }

        var payload = new JObject
        {
            ["event"] = "page_enter",
            ["page_index"] = currentPageIndex,
            ["page_id"] = pageId,
            ["ui_state"] = uiArray,
            ["timestamp_utc"] = DateTime.UtcNow.ToString("o"),
            ["mqtt"] = true
        };

        msgHandler.NotifyObservers(TopicPage, payload);
    }

    // ---------------------------------------------------------
    // UI-Kategorien toggeln (Buttons rufen das auf)
    // ---------------------------------------------------------
    public void ToggleSection(int index)
    {
        if (uiSections == null || index < 0 || index >= uiSections.Length)
            return;

        GameObject go = uiSections[index];
        if (!go) return;

        bool newState = !go.activeSelf;
        go.SetActive(newState);

        UpdateProgressChallengeBackground();

        SendUiToggleEvent(index, newState);
    }

    private void SendUiToggleEvent(int index, bool active)
    {
        if (msgHandler == null)
            return;

        string uiName = uiSections[index] ? uiSections[index].name : $"UI_{index}";
        string pageId = (currentPageIndex >= 0 && currentPageIndex < pageIds.Length)
            ? pageIds[currentPageIndex]
            : "unknown";

        var payload = new JObject
        {
            ["event"] = "ui_toggle",
            ["page_index"] = currentPageIndex,
            ["page_id"] = pageId,
            ["ui_index"] = index,
            ["ui_name"] = uiName,
            ["active"] = active,
            ["timestamp_utc"] = DateTime.UtcNow.ToString("o"),
            ["mqtt"] = true
        };

        msgHandler.NotifyObservers(TopicUI, payload);
    }
    private void UpdateProgressChallengeBackground()
    {
        if (progressChallengeBackground == null)
            return;

        bool progressbarActive =
            uiSections != null &&
            uiSections.Length > 1 &&
            uiSections[1] != null &&
            uiSections[1].activeSelf;

        bool challengeActive =
            uiSections != null &&
            uiSections.Length > 2 &&
            uiSections[2] != null &&
            uiSections[2].activeSelf;

        bool shouldShowBackground = progressbarActive || challengeActive;

        progressChallengeBackground.SetActive(shouldShowBackground);
    }
}
