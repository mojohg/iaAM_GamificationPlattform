using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System;
using Rocworks.Mqtt;

/// <summary>
/// The ProgressBarController class manages the progress bar display and updates based on MQTT messages.
/// It also notifies observers when the progress is complete.
/// </summary>
public class ProgressBarController : MonoBehaviour, IObserver
{
    /// <summary>
    /// The progress bar image component.
    /// </summary>
    [SerializeField] private Image progressBar;

    /// <summary>
    /// The text component displaying the current level.
    /// </summary>
    [SerializeField] private Text levelText;

    private int totalSteps;
    private int currentStep;
    private int currentLevel;
    private Coroutine progressCoroutine;
    private Coroutine resetCoroutine;
    private MsgHandler msgHandler;

    /// <summary>
    /// Initializes the progress bar to an empty state and registers the observer with the MsgHandler.
    /// </summary>
    private void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/PROGRESSBAR", this);
            msgHandler.RegisterObserver("Application/Progressbar/State", this);
            Debug.Log("ExampleObserver registered for ProgressBar");
        }
        progressBar.fillAmount = 0f;
    }

    /// <summary>
    /// Unregisters the observer from the MsgHandler when the object is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/PROGRESSBAR", this);
            msgHandler.RemoveObserver("Application/Progressbar/State", this);
        }
    }

    /// <summary>
    /// Handles MQTT messages to update the progress bar.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="data">The data associated with the event.</param>
    public void OnNotify(string eventType, object data)
    {
        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DATA/PROGRESSBAR")
        {
            Debug.Log("ObserverReached");
            Debug.Log($"Received data: {data}");

            string jsonString = data as string;
            if (jsonString != null)
            {
                try
                {
                    JObject json = JObject.Parse(jsonString);

                    totalSteps = json["totalSteps"]?.ToObject<int>() ?? 0;
                    currentStep = json["currentStep"]?.ToObject<int>() ?? 0;

                    // Überprüfen, ob "level" existiert
                    if (json["level"] != null)
                    {
                        currentLevel = json["level"]?.ToObject<int>() ?? 0;
                        UpdateLevelText(); // Update Level nur, wenn vorhanden
                    }

                    if (progressCoroutine != null)
                    {
                        StopCoroutine(progressCoroutine);
                    }

                    msgHandler.NotifyObservers("Application/Progressbar/State", "Started");
                    progressCoroutine = StartCoroutine(UpdateProgressBar());
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error parsing progress bar data: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Updates the level text.
    /// </summary>
    private void UpdateLevelText()
    {
        if (levelText != null)
        {
            levelText.text = "Level: " + currentLevel;
        }
    }

    /// <summary>
    /// Updates the progress bar fill amount over a duration.
    /// </summary>
    /// <returns>An IEnumerator used for the coroutine.</returns>
    private IEnumerator UpdateProgressBar()
    {
        float targetFillAmount = (float)currentStep / totalSteps;
        if(targetFillAmount >=1f)
        {
            Debug.Log("ProgressBar full, notifying observers...");
            msgHandler.NotifyObservers("Application/Progressbar/State", "Complete");
        }
        float initialFillAmount = progressBar.fillAmount;
        float elapsedTime = 0f;
        float duration = 2f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            progressBar.fillAmount = Mathf.Lerp(initialFillAmount, targetFillAmount, easedT);
            yield return null;
        }

        progressBar.fillAmount = targetFillAmount;

        if (progressBar.fillAmount >= 1f)
        {
            

            if (resetCoroutine != null)
            {
                StopCoroutine(resetCoroutine);
            }
            resetCoroutine = StartCoroutine(ResetProgressBarAfterDelay(3f)); // Start the reset coroutine
        }
    }

    /// <summary>
    /// Resets the progress bar to empty after a delay.
    /// </summary>
    /// <param name="delay">The delay in seconds before resetting the progress bar.</param>
    /// <returns>An IEnumerator used for the coroutine.</returns>
    private IEnumerator ResetProgressBarAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        progressBar.fillAmount = 0f;
    }

    /// <summary>
    /// Gets the current fill amount of the progress bar.
    /// </summary>
    /// <returns>The fill amount as a float between 0 and 1.</returns>
    public float GetFillAmount()
    {
        return progressBar.fillAmount;
    }

    /// <summary>
    /// Gets the current level.
    /// </summary>
    /// <returns>The current level as an integer.</returns>
    public int GetCurrentLevel()
    {
        return currentLevel;
    }
}
