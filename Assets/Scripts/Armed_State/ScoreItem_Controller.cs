using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System;
using Rocworks.Mqtt;

public class ScoreItemController : MonoBehaviour, IObserver
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

        // Find and register the MsgHandler for MQTT communication
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/CONFIG/UPDATE", this);
        }

        // Initialize the button color
        UpdateButtonUI(isarmedStateToolOn);
    }

    // Handle MQTT notification for AutoMode status
    public void OnNotify(string eventType, object data)
    {
        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/CONFIG/UPDATE")
        {

            try
            {
                JObject jsonMessage = JObject.Parse(data.ToString());
                if (jsonMessage["armed_score_items"] != null)
                {
                    isArmedStateToolsOn = jsonMessage["armed_score_items"].ToObject<bool>();
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
