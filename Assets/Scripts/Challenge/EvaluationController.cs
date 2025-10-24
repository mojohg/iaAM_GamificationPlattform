using System.Collections;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class EvaluationController : MonoBehaviour, IObserver
{
    private MsgHandler msgHandler;

    private int currentLevel = 0;
    private string currentDifficulty = "Medium";
    private bool challengeCompleted = false;
    private bool progressCompleted = false;

    private int totalSteps = 0;
    private int currentStep = 0;
    private float challengeTimeElapsed = 0f;
    private float progressTimeElapsed = 0f;

    private void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            // Observer registrieren
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/PROGRESSBAR", this);
            msgHandler.RegisterObserver("Challenge/Result", this);
        }
    }

    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            // Observer entfernen
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/PROGRESSBAR", this);
            msgHandler.RemoveObserver("Challenge/Result", this);
        }
    }

    public void OnNotify(string eventType, object data)
    {
        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/PROGRESSBAR")
        {
            HandleProgressBarData(data as string);
        }
        else if (eventType == "Challenge/Result")
        {
            HandleChallengeResult((bool)data);
        }
    }

    private void HandleProgressBarData(string jsonString)
{
    Debug.Log("ProgressBar Evaluation triggered.");
    if (jsonString != null)
    {
        try
        {
            JObject json = JObject.Parse(jsonString);

            totalSteps = json["totalSteps"]?.ToObject<int>() ?? 0;
            currentStep = json["currentStep"]?.ToObject<int>() ?? 0;

            // Check if "level" exists and update `currentLevel` if present
            if (json["level"] != null)
            {
                currentLevel = json["level"]?.ToObject<int>() ?? currentLevel;
                Debug.Log($"Level updated: {currentLevel}");
            }
            else
            {
                Debug.Log($"Level key missing. Using last known level: {currentLevel}");
            }

            if (currentStep >= totalSteps && !progressCompleted)
            {
                progressCompleted = true;
                progressTimeElapsed = Time.time; // Save the time
                Debug.Log("User finished ProgressBar.");
                EvaluateWinner("User");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error parsing progress bar data: {ex.Message}");
        }
    }
}


    private void HandleChallengeResult(bool completed)
    {
        Debug.Log("Challenge Evaluation triggered.");
        if (completed && !challengeCompleted)
        {
            challengeCompleted = true;
            challengeTimeElapsed = Time.time; // Speichert die Zeit
            Debug.Log("Challenge completed.");
            EvaluateWinner("Challenge");
        }
    }

    private void EvaluateWinner(string finishedFirst)
{
    Debug.Log($"Evaluating winner based on {finishedFirst} completion.");

    if (finishedFirst == "User")
    {
        TriggerResultAnimation(true);
        SendEvaluationResults(true, progressTimeElapsed - challengeTimeElapsed, "User");

        // Benachrichtige ChallengeBarController, um sich zurückzusetzen
        msgHandler.NotifyObservers("Challenge/Control", new JObject
        {
            ["Cmd"] = "Reset"
        });
    }
    else if (finishedFirst == "Challenge")
    {
        TriggerResultAnimation(false);
        SendEvaluationResults(false, challengeTimeElapsed - progressTimeElapsed, "Challenge");
    }
}

    private void TriggerResultAnimation(bool success)
    {
        if (success)
        {
            Debug.Log("User successfully beat the challenge! Triggering success animation.");
            // Hier kannst du deine Animationen starten
        }
        else
        {
            Debug.Log("User lost to the challenge! Triggering failure animation.");
            // Hier kannst du deine Animationen starten
        }
    }

    private void SendEvaluationResults(bool isSuccess, float timeDifference, string winner)
    {
        JObject resultData = new JObject
        {
            ["level"] = currentLevel,
            ["difficulty"] = currentDifficulty,
            ["success"] = isSuccess,
            ["timeDifference"] = timeDifference,
            ["winner"] = winner
        };

        string topic = "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/EVALUATION/RESULT";
        string payload = resultData.ToString();

        msgHandler.NotifyObservers("MQTT/Publish", new JObject
        {
            ["topic"] = topic,
            ["payload"] = payload
        });

        Debug.Log($"Sent evaluation result to topic {topic}: {payload}");
    }
}
