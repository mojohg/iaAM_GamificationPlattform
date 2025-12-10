using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class ChallengeBarController : MonoBehaviour, IObserver
{
    [Header("UI - ProgressBar")]
    [SerializeField] private Image progressBar;

    [Header("UI - Difficulty")]
    [SerializeField] private Button difficultyButton;
    [SerializeField] private Text difficultyText;

    [Header("Difficulty Settings")]
    [SerializeField] private int startDifficultyIndex = 0;

    private readonly string[] difficultyLevels =
    {
        "Very Easy", "Easy", "Medium", "Hard", "Very Hard", "Extreme", "Insane"
    };

    [Header("UI - Result Overlays")]
    [SerializeField] private GameObject successPanel;
    [SerializeField] private Text successText;

    [SerializeField] private GameObject failPanel;
    [SerializeField] private Text failText;

    [Header("Overlay Animation")]
    [SerializeField] private float overlayDuration = 2f;
    [SerializeField] private float overlayFadeDuration = 0.25f;

    [Header("MQTT")]
    [SerializeField] private MsgHandler msgHandler;

    [Header("Topic Base")]
    [SerializeField] private string baseTopic = "GAMING/CHALLENGE";

    // Derived Topics
    private string TopicState   => TopicNamespace.TopicFor(baseTopic + "/STATE");
    private string TopicResult  => TopicNamespace.TopicFor(baseTopic + "/RESULT");
    private string TopicControl => TopicNamespace.TopicFor(baseTopic + "/CONTROL");

    // Internal
    private float benchmarkSeconds = 0f;
    private bool isRunning = false;
    private Coroutine progressRoutine;
    private Coroutine resultRoutine;
    private int currentDifficultyIndex = 0;

    private void Awake()
    {
        if (!msgHandler)
            msgHandler = FindObjectOfType<MsgHandler>();

        if (progressBar)
            progressBar.fillAmount = 0f;

        if (successPanel) successPanel.SetActive(false);
        if (failPanel) failPanel.SetActive(false);

        currentDifficultyIndex = Mathf.Clamp(startDifficultyIndex, 0, difficultyLevels.Length - 1);
        UpdateDifficultyText();

        if (difficultyButton)
            difficultyButton.onClick.AddListener(OnDifficultyClicked);
    }

    private void OnEnable()
    {
        msgHandler?.RegisterObserver(TopicState, this);
        msgHandler?.RegisterObserver(TopicResult, this);
    }

    private void OnDisable()
    {
        msgHandler?.RemoveObserver(TopicState, this);
        msgHandler?.RemoveObserver(TopicResult, this);

        if (difficultyButton)
            difficultyButton.onClick.RemoveListener(OnDifficultyClicked);
    }

    // ---------------------------------------------------------
    // MQTT Handling
    // ---------------------------------------------------------
    public void OnNotify(string eventType, object data)
    {
        if (data is not string json)
            return;

        if (eventType == TopicState)
            OnStateMessage(json);

        else if (eventType == TopicResult)
            OnResultMessage(json);
    }

    private void OnStateMessage(string json)
    {
        var root = JToken.Parse(json);
        var payload = root["data"] ?? root;

        string cmd = payload["Cmd"]?.ToString();

        switch (cmd)
        {
            case "Start":
                float? bench = payload["benchmark"]?.Value<float>();
                if (bench.HasValue)
                {
                    benchmarkSeconds = bench.Value;
                    StartChallengeBar();
                }
                break;

            case "Reset":
                ResetBar();
                break;

            case "Pause":
            case "Continue":
                // If later needed
                break;
        }
    }

    private void OnResultMessage(string json)
    {
        var root = JToken.Parse(json);
        var payload = root["data"] ?? root;

        string result = payload["result"]?.ToString()?.ToLowerInvariant();
        string message = payload["message"]?.ToString();

        bool success = result == "success" || result == "ok" || result == "passed";

        if (string.IsNullOrEmpty(message))
            message = success ? "Challenge completed!" : "Challenge failed!";

        if (resultRoutine != null) StopCoroutine(resultRoutine);
        resultRoutine = StartCoroutine(ShowResult(success, message));

        ResetBar();
    }

    // ---------------------------------------------------------
    // ChallengeBar Animation
    // ---------------------------------------------------------
    private void StartChallengeBar()
    {
        if (progressRoutine != null)
            StopCoroutine(progressRoutine);

        progressRoutine = StartCoroutine(AnimateProgressBar());
    }

    private IEnumerator AnimateProgressBar()
    {
        progressBar.fillAmount = 0f;
        isRunning = true;

        float t = 0f;

        while (t < benchmarkSeconds)
        {
            t += Time.deltaTime;
            progressBar.fillAmount = Mathf.Clamp01(t / benchmarkSeconds);
            yield return null;
        }

        isRunning = false;
    }

    private void ResetBar()
    {
        if (progressRoutine != null)
            StopCoroutine(progressRoutine);

        progressBar.fillAmount = 0f;

    }

    // ---------------------------------------------------------
    // Result Overlay
    // ---------------------------------------------------------
    private IEnumerator ShowResult(bool success, string message)
    {
        GameObject panel = success ? successPanel : failPanel;
        Text text = success ? successText : failText;

        if (!panel || !text)
            yield break;

        text.text = message;

        var cg = panel.GetComponent<CanvasGroup>();
        if (!cg)
            cg = panel.AddComponent<CanvasGroup>();

        panel.SetActive(true);
        cg.alpha = 0f;

        // Fade-in
        float t = 0f;
        while (t < overlayFadeDuration)
        {
            t += Time.deltaTime;
            cg.alpha = t / overlayFadeDuration;
            yield return null;
        }

        yield return new WaitForSeconds(overlayDuration);

        // Fade-out
        t = 0f;
        while (t < overlayFadeDuration)
        {
            t += Time.deltaTime;
            cg.alpha = 1f - (t / overlayFadeDuration);
            yield return null;
        }

        panel.SetActive(false);
    }

    // ---------------------------------------------------------
    // Difficulty Handling
    // ---------------------------------------------------------
    public void OnDifficultyClicked()
    {
        currentDifficultyIndex =
            (currentDifficultyIndex + 1) % difficultyLevels.Length;

        UpdateDifficultyText();
        SendDifficultyToBackend();
    }

    private void UpdateDifficultyText()
    {
        if (difficultyText)
            difficultyText.text = difficultyLevels[currentDifficultyIndex];
    }

    private void SendDifficultyToBackend()
    {
        var payload = new JObject
        {
            ["Cmd"] = "SetDifficulty",
            ["difficulty_index"] = currentDifficultyIndex,
            ["difficulty_label"] = difficultyLevels[currentDifficultyIndex],
            ["mqtt"] = true
        };
        Debug.Log($"[ChallengeBarController] Sending difficulty to backend: {payload}");
        msgHandler?.NotifyObservers(TopicControl, payload);
    }
}
