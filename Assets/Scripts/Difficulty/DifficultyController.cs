using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class DifficultyController : MonoBehaviour, IObserver
{
    public Button difficultyButton;  // Button to change difficulty
    public Text difficultyText;      // Text to display current difficulty

    // Difficulty levels as an array
    private readonly string[] difficultyLevels = { "Low", "Mid", "High", "Extreme" };
    private int currentDifficultyIndex = 0;  // Track the current difficulty level (index)
    private bool canChangeDifficulty = true;  // Only change difficulty if progress is not running

    private MsgHandler msgHandler;  // Reference to the MsgHandler
    private Coroutine sendDelayCoroutine;  // Store the running coroutine for debouncing

    // Colors for button states
    private Color onColor = Color.green;
    private Color offColor = Color.gray;

    void Start()
    {
        // Assign the button click listener
        difficultyButton.onClick.AddListener(OnDifficultyButtonClick);
        canChangeDifficulty = true;
        // Find and register the MsgHandler for MQTT communication
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("Application/Progressbar/State", this);  // Listen to Progressbar state
        }

        UpdateDifficultyText();  // Update the UI with the initial difficulty
    }

    // Handle notification from ProgressbarController
    public void OnNotify(string eventType, object data)
    {
        if (eventType == "Application/Progressbar/State")
        {
            string progressState = data as string;
            if (progressState == "Started")
            {
                canChangeDifficulty = false;  // Disable difficulty changes when progress has started
                difficultyButton.interactable = false;
                UpdateButtonUI(canChangeDifficulty);  // Gray out the button
            }
            else if (progressState == "Complete")
            {
                canChangeDifficulty = true;  // Re-enable difficulty changes when progress completes
                difficultyButton.interactable = true;
                UpdateButtonUI(canChangeDifficulty);  // Revert button color
            }
        }
    }

    // Button click event handler with debouncing
    private void OnDifficultyButtonClick()
    {
        if (canChangeDifficulty)
        {
            // Toggle the difficulty level immediately
            currentDifficultyIndex = (currentDifficultyIndex + 1) % difficultyLevels.Length;
            UpdateDifficultyText();
            
            // If there's already a coroutine running, stop it and start a new one
            if (sendDelayCoroutine != null)
            {
                StopCoroutine(sendDelayCoroutine);
            }

            // Start the coroutine to wait and send the message after a 1 second delay
            sendDelayCoroutine = StartCoroutine(SendDifficultyMessageWithDelay());
        }
    }

    // Coroutine to wait for 1 second and then send the MQTT message
    private IEnumerator SendDifficultyMessageWithDelay()
    {
        yield return new WaitForSeconds(1f);  // Debouncing delay

        // Create a message to publish the difficulty level
        JObject difficultyMessage = new JObject
        {
            { "difficulty", difficultyLevels[currentDifficultyIndex] },
            { "mqtt", true }  // Flag to indicate this message should be sent via MQTT
        };

        // Send the message via MQTT
        if (msgHandler != null)
        {
            msgHandler.NotifyObservers("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/DIFFICULTY", difficultyMessage);
            Debug.Log($"Published difficulty: {difficultyLevels[currentDifficultyIndex]}");
        }
    }

    // Update the displayed text for the current difficulty
    private void UpdateDifficultyText()
    {
        difficultyText.text = $"{difficultyLevels[currentDifficultyIndex]}";
    }

    // Update the button color based on whether it is active
    private void UpdateButtonUI(bool isActive)
    {
        // Get the current ColorBlock of the button
        ColorBlock cb = difficultyButton.colors;

        // Set the button to gray if inactive or green if active
        cb.normalColor = isActive ? onColor : offColor;
        cb.highlightedColor = isActive ? onColor : offColor;
        cb.pressedColor = isActive ? onColor : offColor * 0.8f;  // Slightly darker when pressed
        cb.selectedColor = isActive ? onColor : offColor;

        // Apply the updated ColorBlock back to the button
        difficultyButton.colors = cb;
    }
}
