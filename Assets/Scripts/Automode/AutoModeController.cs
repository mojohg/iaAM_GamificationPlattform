using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System;
using Rocworks.Mqtt;

public class AutoModeController : MonoBehaviour, IObserver
{
   public Button autoModeButton;  // Assign your button here in the inspector
    private bool isAutoModeOn = false;  // Track auto mode state
    private bool isAutoModesOn = false;
    private Coroutine sendDelayCoroutine;  // Store the running coroutine
    private MsgHandler msgHandler;   // Reference to your MQTT message handler

    // Colors for button states
    private Color onColor = Color.green;
    private Color offColor = Color.gray;

    void Start()
    {
        // Assign the button click listener
        autoModeButton.onClick.AddListener(OnAutoModeButtonClick);

        // Find and register the MsgHandler for MQTT communication
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("UserDataUpdate/AutoMode", this);
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/CONFIG/UPDATE", this);

        }

        // Initialize the button color
        UpdateButtonUI(isAutoModeOn);
    }

    // Handle MQTT notification for AutoMode status
    public void OnNotify(string eventType, object data)
    {
                    Debug.Log("AUTOMODE MESSAGE RECIEVED!!!!");
        if (eventType == "UserDataUpdate/AutoMode")
        {
            string status = data as string;
            isAutoModeOn = status == "On";
            UpdateButtonUI(isAutoModeOn);  // Update the UI to reflect the state
        }
        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/CONFIG/UPDATE")
        {

            try
            {
                JObject jsonMessage = JObject.Parse(data.ToString());
                if (jsonMessage["automode"] != null)
                {
                    isAutoModesOn = jsonMessage["automode"].ToObject<bool>();
                    UpdateButtonUI(isAutoModesOn);
                    Debug.Log($"AutoMode updated from MQTT: {isAutoModesOn}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing MQTT AutoMode update: {ex.Message}");
            }
        }
    }

    // Button click event handler
    private void OnAutoModeButtonClick()
    {
        // Toggle the AutoMode state and update the UI immediately
        isAutoModeOn = !isAutoModeOn;
        UpdateButtonUI(isAutoModeOn);

        // If there's already a coroutine running, stop it and start a new one
        if (sendDelayCoroutine != null)
        {
            StopCoroutine(sendDelayCoroutine);
        }

        // Start the coroutine to wait and send the message
        sendDelayCoroutine = StartCoroutine(WaitAndSendMessage());
    }

    // Coroutine to wait for 1 second and then send the MQTT message
    private IEnumerator WaitAndSendMessage()
    {
        // Wait for 1 second
        yield return new WaitForSeconds(1f);

        // Create the message to be sent via MQTT
         JObject message = new JObject
        {
            { "automode", isAutoModeOn ? "On" : "Off" },
            { "mqtt", true }  // This flag indicates the message should be published
        };
        // Send the message via MQTT
        if (msgHandler != null)
        {
            msgHandler.NotifyObservers("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/AUTOMODE", message);
        }

        Debug.Log($"Sent MQTT message: {message}");
    }

    // Update the button color based on AutoMode state
    private void UpdateButtonUI(bool isOn)
    {
        // Get the current ColorBlock of the button
        ColorBlock cb = autoModeButton.colors;

        // Update all relevant color states in the ColorBlock
        cb.normalColor = isOn ? onColor : offColor;
        cb.highlightedColor = isOn ? onColor : offColor;
        cb.pressedColor = isOn ? onColor : offColor * 0.8f;  // Slightly darker when pressed
        cb.selectedColor = isOn ? onColor : offColor;
        cb.disabledColor = Color.gray;  // Optional: a color for when the button is disabled

        // Apply the updated ColorBlock back to the button
        autoModeButton.colors = cb;

        // Optionally, update the button text to show "On" or "Off"
        autoModeButton.GetComponentInChildren<Text>().text = isOn ? "Auto On" : "Auto Off";
    }
}
