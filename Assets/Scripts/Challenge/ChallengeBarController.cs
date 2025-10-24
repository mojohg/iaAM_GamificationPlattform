using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System;
using Rocworks.Mqtt;

public class ChallengeBarController : MonoBehaviour, IObserver
{
    [SerializeField] private Image progressBar;
    [SerializeField] private Button difficultyButton;
    [SerializeField] private Text difficultyText;
    [SerializeField] private Text levelText; // Zeigt das aktuelle Level an

    private readonly string[] difficultyLevels = {
        "Very Easy", "Easy", "Medium", "Hard", "Very Hard", "Extreme", "Insane"
    };
    private readonly float[] difficultyModifiers = {
        1.5f, 1.25f, 1.0f, 0.85f, 0.75f, 0.6f, 0.5f
    };

    private int currentDifficultyIndex = 0; // Standard: "Very easy"
    private int baseBenchmark;
    private int adjustedBenchmark;
    private int currentLevel = 0; // Speichert das aktuelle Level
    private float elapsedTime = 0f;
    private bool isPaused = false;
    private Coroutine progressCoroutine;
    private MsgHandler msgHandler;
    private bool completed = false;
    private bool isActiveChallenge = false; // Neue Challenge darf erst gestartet werden, wenn true


    private void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            // Registriere Observer für Challenge und Level
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/CHALLENGE", this);
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/PROGRESSBAR", this);
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/CHALLENGE/CONTROL", this);
            msgHandler.RegisterObserver("Challenge/Control", this);
        }
        progressBar.fillAmount = 0f;

       // difficultyButton.onClick.AddListener(ChangeDifficulty);
        UpdateDifficultyText();
    }

    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/CHALLENGE", this);
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/PROGRESSBAR", this);
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/CHALLENGE/CONTROL", this);
            msgHandler.RemoveObserver("Challenge/Control", this);
        }
    }

    public void OnNotify(string eventType, object data)
    {
        Debug.Log($"Received notification for event: {eventType} with data: {data} in the ChallengevbarController");
        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/CHALLENGE")
        {
            HandleChallengeData(data as string);
        }
        else if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/PROGRESSBAR")
        {
            Debug.Log("ProgressbarData Recieved");
            HandleProgressBarData(data as string);
        }
        else if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/CHALLENGE/CONTROL")
        {
            HandleChallengeControl(data as string);
        }
        else if (eventType == "Challenge/Control")
        {
            Debug.Log("JJJJJJJJJJJJJJJJJJJJJAAAAAAAA REEEESET");
            HandleChallengeControl(data as string);
        }
    }

    private void HandleChallengeData(string jsonString)
    {
        if (jsonString != null)
        {
            JObject json = JObject.Parse(jsonString);
            string benchmarkString = (string)json["benchmark"];
            baseBenchmark = ConvertBenchmarkToSeconds(benchmarkString);
            AdjustBenchmark();
            elapsedTime = 0f;
             isActiveChallenge = true; // Challenge wird als aktiv gesetzt

            if (progressCoroutine != null)
            {
                StopCoroutine(progressCoroutine);
            }
            progressCoroutine = StartCoroutine(UpdateChallengeBar());
        }
    }

    private void HandleProgressBarData(string jsonString)
{
    if (jsonString != null)
    {
        JObject json = JObject.Parse(jsonString);

        // Level aktualisieren
        if (json["level"] != null)
        {
            currentLevel = json["level"]?.ToObject<int>() ?? 0;
            UpdateLevelText();
        }
         if (!isActiveChallenge) 
        {
            Debug.Log("ProgressBar update ignored, because no active challenge.");
            return;
        }
        // TotalSteps und CurrentStep auswerten
        int totalSteps = json["totalSteps"]?.ToObject<int>() ?? 0;
        int currentStep = json["currentStep"]?.ToObject<int>() ?? 0;

        if (totalSteps > 0 && currentStep > 0 && totalSteps == currentStep)
        {
            Debug.Log($"User completed the challenge at level {currentLevel} with {totalSteps} steps!");

            // Erfolgsmeldung erstellen
            JObject resultData = new JObject
            {
                ["difficulty"] = difficultyLevels[currentDifficultyIndex],
                ["difficulty_raw"] = currentDifficultyIndex,
                ["level"] = currentLevel,
                ["result"] = "success" // Erfolg oder anderes Ergebnis
            };
            Debug.Log($"Difficulty {difficultyLevels[currentDifficultyIndex]}");
            ResetChallenge();
            // Ergebnis an Observer senden
            msgHandler.NotifyObservers("UserResultEvent/Challenge", resultData.ToString());
            // JSON-Daten erstellen
        JObject badgeData = new JObject
        {
            ["level"] = currentLevel,
            ["difficulty"] = currentDifficultyIndex,
            ["status"] = "unlocked",
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["mqtt"] = true // Flag, um die MQTT-Veröffentlichung auszulösen
        };

        // Benachrichtige den MsgHandler
        msgHandler.NotifyObservers("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/BADGE/UNLOCKED", badgeData);
            Debug.Log("UserResultEvent/Challenge published with success!");
        }
    }
}
    private void HandleChallengeControl(string jsonString)
    {
        if (jsonString != null)
        {
            JObject json = JObject.Parse(jsonString);
            string command = (string)json["Cmd"];

            if (command == "Pause")
            {
                PauseChallenge();
            }
            else if (command == "Continue")
            {
                ContinueChallenge();
            }
            else if (command == "Reset")
            {
                ResetChallenge();
            }
        }
    }

    private void ResetChallenge()
    {
        Debug.Log("ChallengeBarController resetting.");
        if (progressCoroutine != null)
        {
            StopCoroutine(progressCoroutine);
            progressCoroutine = null;
        }

        progressBar.fillAmount = 0f;
        elapsedTime = 0f;
        completed = false;

        Debug.Log("ChallengeBarController reset complete.");
    }


    private void UpdateLevelText()
    {
        levelText.text = $"Level: {currentLevel}";
    }

    private int ConvertBenchmarkToSeconds(string benchmarkString)
    {
        try
        {
            string[] parts = benchmarkString.Split(':');
            if (parts.Length == 2)
            {
                int minutes = int.Parse(parts[0]);
                int seconds = int.Parse(parts[1]);
                return (minutes * 60) + seconds;
            }
            else
            {
                Debug.LogError($"Invalid benchmark format: {benchmarkString}. Expected format MM:SS.");
                return 0;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing benchmark: {benchmarkString}. Exception: {e}");
            return 0;
        }
    }

    private void AdjustBenchmark()
    {
        adjustedBenchmark = Mathf.RoundToInt(baseBenchmark * difficultyModifiers[currentDifficultyIndex]);
        Debug.Log($"Benchmark adjusted to {adjustedBenchmark} seconds for difficulty '{difficultyLevels[currentDifficultyIndex]}'");
    }

    public void ChangeDifficulty()
    {
        if (progressCoroutine != null) return;

        currentDifficultyIndex = (currentDifficultyIndex + 1) % difficultyLevels.Length;
        UpdateDifficultyText();
        AdjustBenchmark();
    }

    private void UpdateDifficultyText()
    {
        difficultyText.text = $"{difficultyLevels[currentDifficultyIndex]}";
    }

    private void PauseChallenge()
    {
        isPaused = true;
    }

    private void ContinueChallenge()
    {
        if (isPaused)
        {
            isPaused = false;
            if (progressCoroutine == null)
            {
                progressCoroutine = StartCoroutine(UpdateChallengeBar());
            }
        }
    }

private IEnumerator UpdateChallengeBar()
{
    float duration = adjustedBenchmark;
    while (elapsedTime < duration)
    {
        if (isPaused)
        {
            yield return null;
            continue;
        }

        elapsedTime += Time.deltaTime;
        progressBar.fillAmount = Mathf.Clamp01(elapsedTime / duration);

        if (progressBar.fillAmount >= 1f)
        {
            progressCoroutine = null;
            Debug.Log("Challenge Complete");
            NotifyChallengeFailure();
            yield break;
        }

        yield return null;
    }
}

private void NotifyChallengeFailure()
{
    JObject resultData = new JObject
    {
        ["result"] = "failed",
        ["reason"] = "time_expired"
    };
    isActiveChallenge = false;
    // Sende das Ergebnis an den Observer
    msgHandler.NotifyObservers("UserResultEvent/Challenge", resultData.ToString());
    Debug.Log("Challenge failed notification sent.");
}

}
