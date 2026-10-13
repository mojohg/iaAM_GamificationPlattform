using System.Collections;
using System.Collections.Generic;
using Rocworks.Mqtt;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Always-on HUD. Attach to a GameObject that stays enabled while switching pages.
/// The banner is a separate child GameObject and may be toggled by this presenter.
/// </summary>
public class BadgeProgressHudController : MonoBehaviour, IObserver
{
    [SerializeField] private MsgHandler msgHandler;
    [Header("Persistent HUD")]
    [SerializeField] private Text badgeCountText;
    [SerializeField] private Image badgeProgressFill;
    [Header("Badge unlocked toast")]
    [SerializeField] private GameObject unlockBanner;
    [SerializeField] private Text unlockBannerText;
    [SerializeField] private float toastSeconds = 4f;

    private readonly BadgeProgressionState _state = new BadgeProgressionState();
    private readonly Queue<string> _pendingMessages = new Queue<string>();
    private Coroutine _toast;
    private string _badgesTopic;
    private string _unlockTopic;

    private void Awake()
    {
        if (!msgHandler) msgHandler = FindObjectOfType<MsgHandler>();
        if (unlockBanner) unlockBanner.SetActive(false);
        RefreshProgress();
    }

    private void OnEnable()
    {
        _badgesTopic = TopicNamespace.TopicFor("GAMING", "USERDATA/BADGES");
        _unlockTopic = TopicNamespace.TopicFor("GAMING", "USERRESULT/CHALLENGE");
        msgHandler?.RegisterObserver(_badgesTopic, this);
        msgHandler?.RegisterObserver(_unlockTopic, this);
        TopicNamespace.OnRootChanged += HandleRootChanged;
    }

    private void OnDisable()
    {
        TopicNamespace.OnRootChanged -= HandleRootChanged;
        msgHandler?.RemoveObserver(_badgesTopic, this);
        msgHandler?.RemoveObserver(_unlockTopic, this);
        if (_toast != null) StopCoroutine(_toast);
        _toast = null;
        if (unlockBanner) unlockBanner.SetActive(false);
    }

    private void HandleRootChanged(string oldRoot, string newRoot)
    {
        // MsgHandler remaps its registered observer keys. Keep our comparison
        // and cleanup keys in sync with the same namespace event.
        _badgesTopic = TopicNamespace.TopicFor("GAMING", "USERDATA/BADGES");
        _unlockTopic = TopicNamespace.TopicFor("GAMING", "USERRESULT/CHALLENGE");
    }

    public void OnNotify(string eventType, object data)
    {
        if (data is not string json) return;
        if (eventType == _badgesTopic)
        {
            // Snapshot arrives before unlock event: never create a toast here.
            if (_state.TryApplySnapshot(json)) RefreshProgress();
        }
        else if (eventType == _unlockTopic && _state.TryApplyUnlock(json, out var label))
        {
            RefreshProgress();
            _pendingMessages.Enqueue(label);
            if (_toast == null) _toast = StartCoroutine(ShowQueuedNotifications());
        }
    }

    private void RefreshProgress()
    {
        if (badgeCountText) badgeCountText.text = $"Badges: {_state.UnlockedCount} / {_state.TotalCount}";
        if (badgeProgressFill) badgeProgressFill.fillAmount = (float)_state.UnlockedCount / _state.TotalCount;
    }

    private IEnumerator ShowQueuedNotifications()
    {
        while (_pendingMessages.Count > 0)
        {
            string message = _pendingMessages.Dequeue();
            if (unlockBannerText) unlockBannerText.text = message;
            if (unlockBanner) unlockBanner.SetActive(true);
            yield return new WaitForSeconds(Mathf.Max(0.5f, toastSeconds));
            if (unlockBanner) unlockBanner.SetActive(false);
        }
        _toast = null;
    }
}
