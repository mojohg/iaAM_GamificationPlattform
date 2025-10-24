using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System;
using Rocworks.Mqtt;

public class ToolController : MonoBehaviour, IObserver
{
    public Button armedStateToolButton;  // Assign your button here in the inspector
    private bool isarmedStateToolOn = false;  // Track auto mode state
    private bool isArmedStateToolsOn = false;
    private Coroutine sendDelayCoroutine;  // Store the running coroutine
    private MsgHandler msgHandler;   // Reference to your MQTT message handler

    // Colors for button states
    private Color onColor = Color.green;
    private Color offColor = Color.gray;

    void Start()
    {
        // Assign the button click listener
        armedStateToolButton.onClick.AddListener(OnArmedStateToolButtonClick);

        // Find and register the MsgHandler for MQTT communication
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("UserDataUpdate/ArmedStateTool", this);
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/CONFIG/UPDATE", this);
        }

        // Initialize the button color
        UpdateButtonUI(isarmedStateToolOn);
    }

    // Handle MQTT notification for AutoMode status
    public void OnNotify(string eventType, object data)
    {
        if (eventType == "UserDataUpdate/ArmedStateTool")
        {
            string status = data as string;
            isarmedStateToolOn = status == "On";
            UpdateButtonUI(isarmedStateToolOn);  // Update the UI to reflect the state
        }
              if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/CONFIG/UPDATE")
        {

            try
            {
                JObject jsonMessage = JObject.Parse(data.ToString());
                if (jsonMessage["armed_state_tools"] != null)
                {
                    isArmedStateToolsOn = jsonMessage["armed_state_tools"].ToObject<bool>();
                    UpdateButtonUI(isArmedStateToolsOn);
                    Debug.Log($"AutoMode updated from MQTT: {isArmedStateToolsOn}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing MQTT AutoMode update: {ex.Message}");
            }
        }
    }

    // Button click event handler
    private void OnArmedStateToolButtonClick()
    {
        // Toggle the AutoMode state and update the UI immediately
        isarmedStateToolOn = !isarmedStateToolOn;
        UpdateButtonUI(isarmedStateToolOn);

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

     // Create the message with the "should_publish" flag
        JObject message = new JObject
        {
            { "armed_state", isarmedStateToolOn ? "On" : "Off" },
            { "mqtt", true }  // This flag indicates the message should be published
        };
        // Send the message via MQTT
        if (msgHandler != null)
        {
            msgHandler.NotifyObservers("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/ARMEDSTATE/TOOL/SAVE", message);
        }

        Debug.Log($"Sent MQTT message: {message}");
    }

    // Update the button color based on AutoMode state
    private void UpdateButtonUI(bool isOn)
    {
        // Get the current ColorBlock of the button
        ColorBlock cb = armedStateToolButton.colors;

        // Update all relevant color states in the ColorBlock
        cb.normalColor = isOn ? onColor : offColor;
        cb.highlightedColor = isOn ? onColor : offColor;
        cb.pressedColor = isOn ? onColor : offColor * 0.8f;  // Slightly darker when pressed
        cb.selectedColor = isOn ? onColor : offColor;
        cb.disabledColor = Color.gray;  // Optional: a color for when the button is disabled

        // Apply the updated ColorBlock back to the button
        armedStateToolButton.colors = cb;

        // Optionally, update the button text to show "On" or "Off"
        armedStateToolButton.GetComponentInChildren<Text>().text = isOn ? "Tool On" : "Tool Off";
    }
}
