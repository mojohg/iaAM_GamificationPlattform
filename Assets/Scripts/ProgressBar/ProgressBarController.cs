using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System;
using Rocworks.Mqtt;

/// <summary>
/// Manages the progress bar display and updates based on MQTT messages.
/// Notifies observers when the progress is complete.
/// </summary>
public class ProgressBarController : MonoBehaviour, IObserver
{
    [Header("UI")]
    [SerializeField] private Image progressBar;
    [SerializeField] private Text levelText;

    [Header("MQTT / MsgHandler")]
    [SerializeField] private MsgHandler msgHandler;

    [Header("Topic-Konfiguration")]
    [Tooltip("Topic-Gruppe, z.B. GAMING")]
    [SerializeField] private string group = "GAMING";

    [Tooltip("Topic-Key relativ zur Gruppe, z.B. DATA/PROGRESSBAR")]
    [SerializeField] private string category = "DATA/PROGRESSBAR";

    [Header("Animation")]
    [Tooltip("Dauer für die Progressbar-Animation in Sekunden.")]
    [SerializeField] private float animationDuration = 2f;

    [Tooltip("Verzögerung, bevor die Progressbar nach 100 % wieder auf 0 gesetzt wird.")]
    [SerializeField] private float resetDelay = 3f;

    private int totalSteps;
    private int currentStep;
    private int currentLevel;

    private Coroutine progressCoroutine;
    private Coroutine resetCoroutine;

    private const string LocalStateTopic = "Application/Progressbar/State";

    private string Topic() => TopicNamespace.TopicFor(group, category);

    private void Awake()
    {
        if (!msgHandler)
            msgHandler = FindObjectOfType<MsgHandler>();

        if (!progressBar)
            Debug.LogError("[ProgressBarController] ProgressBar Image is not assigned!");

        if (levelText)
            levelText.text = string.Empty;

        if (progressBar)
            progressBar.fillAmount = 0f;
    }

    private void OnEnable()
    {
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver(Topic(), this);
            Debug.Log($"[ProgressBarController] Registered for topic '{Topic()}'.");
        }
        else
        {
            Debug.LogError("[ProgressBarController] MsgHandler not found in scene!");
        }
    }

    private void OnDisable()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver(Topic(), this);
            Debug.Log($"[ProgressBarController] Unregistered from topic '{Topic()}'.");
        }
    }

    /// <summary>
    /// Handles MQTT messages to update the progress bar.
    /// </summary>
    public void OnNotify(string eventType, object data)
    {
        if (eventType != Topic())
            return;

        if (data is not string jsonString)
        {
            Debug.LogError("[ProgressBarController] Received data is not a JSON string!");
            return;
        }

        try
        {
            var root = JToken.Parse(jsonString);

            // Unterstützt sowohl { "data": { ... } } als auch flaches Objekt
            var payload = root["data"] ?? root;

            totalSteps  = payload["totalSteps"]?.Value<int>()
                          ?? payload["total_steps"]?.Value<int>() ?? 0;

            currentStep = payload["currentStep"]?.Value<int>()
                          ?? payload["current_step"]?.Value<int>() ?? 0;

            var levelToken = payload["level"] ?? payload["current_level"];
            if (levelToken != null)
            {
                currentLevel = levelToken.Value<int>();
                UpdateLevelText();
            }

            if (totalSteps <= 0)
            {
                Debug.LogWarning("[ProgressBarController] totalSteps <= 0 – ignoring update.");
                return;
            }

            if (progressCoroutine != null)
                StopCoroutine(progressCoroutine);

            // interner Status für andere Observer
            msgHandler?.NotifyObservers(LocalStateTopic, "Started");
            progressCoroutine = StartCoroutine(UpdateProgressBar());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ProgressBarController] Error parsing progress bar data: {ex.Message}");
        }
    }

    /// <summary>Updates the level text.</summary>
    private void UpdateLevelText()
    {
        if (levelText != null)
        {
            levelText.text = "Level: " + currentLevel;
        }
    }

    /// <summary>Updates the progress bar fill amount over a duration.</summary>
    private IEnumerator UpdateProgressBar()
    {
        if (!progressBar)
            yield break;

        float targetFillAmount = Mathf.Clamp01((float)currentStep / Mathf.Max(1, totalSteps));

        if (targetFillAmount >= 1f)
        {
            Debug.Log("[ProgressBarController] ProgressBar full, notifying observers...");
            msgHandler?.NotifyObservers(LocalStateTopic, "Complete");
        }

        float initialFillAmount = progressBar.fillAmount;
        float elapsedTime = 0f;
        float duration = Mathf.Max(0.01f, animationDuration);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f); // easeOutCubic
            progressBar.fillAmount = Mathf.Lerp(initialFillAmount, targetFillAmount, easedT);
            yield return null;
        }

        progressBar.fillAmount = targetFillAmount;

        if (Mathf.Approximately(progressBar.fillAmount, 1f))
        {
            if (resetCoroutine != null)
                StopCoroutine(resetCoroutine);

            resetCoroutine = StartCoroutine(ResetProgressBarAfterDelay(resetDelay));
        }
    }

    /// <summary>Resets the progress bar to empty after a delay.</summary>
    private IEnumerator ResetProgressBarAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (progressBar)
            progressBar.fillAmount = 0f;
    }

    public float GetFillAmount() => progressBar ? progressBar.fillAmount : 0f;

    public int GetCurrentLevel() => currentLevel;
}
