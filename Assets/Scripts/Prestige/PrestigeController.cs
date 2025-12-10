using System;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class PrestigeController : MonoBehaviour, IObserver
{
    [Header("UI")]
    [SerializeField] private GameObject lockIcon;      // zeigt: Prestige gesperrt
    [SerializeField] private Button prestigeButton;    // Button zum Auslösen

    [Header("MQTT Topics")]
    [Tooltip("Group, z.B. GAMING")]
    [SerializeField] private string group = "GAMING";

    [Tooltip("Kategorie für den Prestige-Status (Backend -> Unity)")]
    [SerializeField] private string stateCategory = "PRESTIGE_STATE";

    [Tooltip("Kategorie für Aktivierungs-Events (Unity -> Backend)")]
    [SerializeField] private string activatedCategory = "PRESTIGE_ACTIVATED";

    [Header("Behavior")]
    [Tooltip("Haltezeit in Sekunden, um Prestige zu aktivieren")]
    [SerializeField] private float holdTimeSeconds = 3f;

    private MsgHandler msgHandler;
    private Coroutine buttonHoldCoroutine;

    private string _topicState;
    private string _topicActivated;

    private void Awake()
    {
        if (!prestigeButton)
            prestigeButton = GetComponent<Button>();

        if (!msgHandler)
            msgHandler = FindObjectOfType<MsgHandler>();

        // UI initial: gesperrt
        UpdateUI(false);
    }

    private void OnEnable()
    {
        if (msgHandler == null)
        {
            Debug.LogError("[PrestigeController] MsgHandler not found in scene.");
            return;
        }

        _topicState     = TopicNamespace.TopicFor(group, stateCategory);
        _topicActivated = TopicNamespace.TopicFor(group, activatedCategory);

        msgHandler.RegisterObserver(_topicState, this);
        TopicNamespace.OnRootChanged += HandleRootChanged;

        Debug.Log($"[PrestigeController] Listening on state topic: {_topicState}");
    }

    private void OnDisable()
    {
        if (msgHandler != null && !string.IsNullOrEmpty(_topicState))
            msgHandler.RemoveObserver(_topicState, this);

        TopicNamespace.OnRootChanged -= HandleRootChanged;

        CancelHold();
    }

    // --- IObserver ---
    public void OnNotify(string eventType, object data)
    {
        if (eventType != _topicState) return;
        if (data is not string jsonString || string.IsNullOrWhiteSpace(jsonString)) return;

        try
        {
            var json = JObject.Parse(jsonString);

            // Wir sind tolerant bei den Feldnamen
            bool prestigeAvailable =
                json["Prestige"]?.ToObject<bool?>()
                ?? json["prestige"]?.ToObject<bool?>()
                ?? json["available"]?.ToObject<bool?>()
                ?? json["prestige_available"]?.ToObject<bool?>()
                ?? false;

            UpdateUI(prestigeAvailable);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PrestigeController] Error parsing prestige state: {ex.Message}");
        }
    }

    private void UpdateUI(bool prestigeAvailable)
    {
        if (lockIcon) lockIcon.SetActive(!prestigeAvailable);

        if (prestigeButton)
            prestigeButton.interactable = prestigeAvailable;

        // falls während des Haltens wieder gelockt wird -> abbrechen
        if (!prestigeAvailable)
            CancelHold();
    }

    // --- Button-Ereignisse (per Inspector an Pointer Down / Up hängen) ---
    public void OnButtonPress()
    {
        if (prestigeButton == null || !prestigeButton.interactable) return;
        CancelHold(); // Sicherheit
        buttonHoldCoroutine = StartCoroutine(HoldButtonCoroutine());
    }

    public void OnButtonRelease()
    {
        CancelHold();
    }

    private void CancelHold()
    {
        if (buttonHoldCoroutine != null)
        {
            StopCoroutine(buttonHoldCoroutine);
            buttonHoldCoroutine = null;
        }
    }

    private System.Collections.IEnumerator HoldButtonCoroutine()
    {
        float elapsedTime = 0f;

        while (elapsedTime < holdTimeSeconds)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Button wurde lange genug gehalten -> Event ans Backend
        SendPrestigeActivatedMessage();
    }

    private void SendPrestigeActivatedMessage()
    {
        if (msgHandler == null)
        {
            Debug.LogWarning("[PrestigeController] MsgHandler is null, cannot send MQTT message.");
            return;
        }

        var message = new JObject
        {
            ["PrestigeActivated"] = true,
            ["mqtt"] = true
        };

        msgHandler.NotifyObservers(_topicActivated, message);
        Debug.Log($"[PrestigeController] Prestige activated message sent on topic: {_topicActivated}");
    }

    // Root-Wechsel (z.B. anderes Gerät / Profil)
    private void HandleRootChanged(string oldRoot, string newRoot)
    {
        _topicState     = TopicNamespace.TopicFor(group, stateCategory);
        _topicActivated = TopicNamespace.TopicFor(group, activatedCategory);
        Debug.Log($"[PrestigeController] Topics remapped to: state={_topicState}, activated={_topicActivated}");
    }
}
