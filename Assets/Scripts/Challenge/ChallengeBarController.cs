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
    private bool difficultyFrozen = false;
    private bool resultHandledForCurrentChallenge;
    private bool challengeStarted;


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


    }

    private void OnDestroy()
    {
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
                    FreezeDifficulty(true);
                    challengeStarted = true;
                    resultHandledForCurrentChallenge = false;
                    StartChallengeBar();
                }
                break;

            case "Reset":
                FreezeDifficulty(false);
                challengeStarted = false;
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
        try
        {
            var root = JToken.Parse(json);
            var payload = root["data"] ?? root;
            string result = payload["result"]?.ToString()?.Trim().ToLowerInvariant();
            if (result != "success" && result != "fail" && result != "ok" && result != "passed")
                return;

            // MWS DTO currently has no challenge/round ID. Do not show duplicate RESULTs
            // until a new Start. Ignore orphan results on initial login/reconnect.
            if (!challengeStarted || resultHandledForCurrentChallenge) return;

            resultHandledForCurrentChallenge = true;
            bool success = result == "success" || result == "ok" || result == "passed";
            string explanation = payload["message"]?.ToString();
            if (string.IsNullOrWhiteSpace(explanation))
                explanation = success ? "Challenge erfolgreich" : "Challenge nicht bestanden";

            float benchmark = payload["benchmark"]?.Value<float>() ?? benchmarkSeconds;
            string benchmarkLine = benchmark > 0f
                ? $"Benchmark: {benchmark:0.#} s"
                : "Benchmark: nicht vorhanden";
            string workstep = payload["workstep_id"] != null
                ? $"\nWorkstep: {payload["workstep_id"]}" : "";
            // An authoritative measured actual time is not present in this MWS DTO.
            string message = $"{explanation}\n{benchmarkLine}{workstep}";

            if (resultRoutine != null) StopCoroutine(resultRoutine);
            resultRoutine = StartCoroutine(ShowResult(success, message));
            ResetBar();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[ChallengeBar] Invalid challenge result: {exception.Message}");
        }
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
        if (progressBar) progressBar.fillAmount = 0f;
        if (benchmarkSeconds <= 0f) yield break;
        isRunning = true;

        float t = 0f;

        while (t < benchmarkSeconds)
        {
            t += Time.deltaTime;
            if (progressBar) progressBar.fillAmount = Mathf.Clamp01(t / benchmarkSeconds);
            yield return null;
        }

        isRunning = false;
    }

    private void ResetBar()
    {
        if (progressRoutine != null)
            StopCoroutine(progressRoutine);

        isRunning = false;
        if (progressBar) progressBar.fillAmount = 0f;

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
        if (difficultyFrozen)
        {
            Debug.Log("[ChallengeBar] Difficulty change blocked (frozen).");
            return;
        }
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
            ["difficulty_index"] = currentDifficultyIndex + 1,
            ["difficulty_label"] = difficultyLevels[currentDifficultyIndex],
            ["mqtt"] = true
        };
        Debug.Log($"[ChallengeBarController] Sending difficulty to backend: {payload}");
        msgHandler?.NotifyObservers(TopicControl, payload);
    }

    private void FreezeDifficulty(bool freeze)
    {
        difficultyFrozen = freeze;

        if (difficultyButton != null)
            difficultyButton.interactable = !freeze;
    }
}
