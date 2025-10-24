using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System;
using Rocworks.Mqtt;

public class ItemController : MonoBehaviour, IObserver
{
    public Button armedStateItemButton;  // Assign your button here in the inspector
    private bool isArmedStateItemOn = false;
    private bool isArmedStateItemsOn = false;
    private Coroutine sendDelayCoroutine;  // Store the running coroutine
    private MsgHandler msgHandler;   // Reference to your MQTT message handler

    // Colors for button states
    private Color onColor = Color.green;
    private Color offColor = Color.gray;

    void Start()
    {
        // Assign the button click listener
        armedStateItemButton.onClick.AddListener(OnArmedStateItemButtonClick);

        // Find and register the MsgHandler for MQTT communication
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("UserDataUpdate/ArmedStateItem", this);
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/CONFIG/UPDATE", this);
        }

        // Initialize the button color
        UpdateButtonUI(isArmedStateItemOn);
    }

    // Handle MQTT notification for Item armed state
    public void OnNotify(string eventType, object data)
    {
        if (eventType == "UserDataUpdate/ArmedStateItem")
        {
            string status = data as string;
            isArmedStateItemOn = status == "On";
            UpdateButtonUI(isArmedStateItemOn);  // Update the UI to reflect the state
        }
        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/CONFIG/UPDATE")
        {

            try
            {
                JObject jsonMessage = JObject.Parse(data.ToString());
                if (jsonMessage["armed_state_items"] != null)
                {
                    isArmedStateItemsOn = jsonMessage["armed_state_items"].ToObject<bool>();
                    UpdateButtonUI(isArmedStateItemsOn);
                    Debug.Log($"AutoMode updated from MQTT: {isArmedStateItemsOn}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing MQTT armed_state_items update: {ex.Message}");
            }
        }
    }

    // Button click event handler
    private void OnArmedStateItemButtonClick()
    {
        // Toggle the Item armed state and update the UI immediately
        isArmedStateItemOn = !isArmedStateItemOn;
        UpdateButtonUI(isArmedStateItemOn);

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
        { "armed_state", isArmedStateItemOn ? "On" : "Off" },
        { "mqtt", true }  // This flag indicates the message should be published
    };
        // Send the message via MQTT
        if (msgHandler != null)
        {
            msgHandler.NotifyObservers("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/ARMEDSTATE/ITEM/SAVE", message);
        }

        Debug.Log($"Sent MQTT message: {message}");
    }

    // Update the button color based on Item armed state
    private void UpdateButtonUI(bool isOn)
    {
        // Get the current ColorBlock of the button
        ColorBlock cb = armedStateItemButton.colors;

        // Update all relevant color states in the ColorBlock
        cb.normalColor = isOn ? onColor : offColor;
        cb.highlightedColor = isOn ? onColor : offColor;
        cb.pressedColor = isOn ? onColor : offColor * 0.8f;  // Slightly darker when pressed
        cb.selectedColor = isOn ? onColor : offColor;
        cb.disabledColor = Color.gray;  // Optional: a color for when the button is disabled

        // Apply the updated ColorBlock back to the button
        armedStateItemButton.colors = cb;

        // Optionally, update the button text to show "On" or "Off"
        armedStateItemButton.GetComponentInChildren<Text>().text = isOn ? "Item On" : "Item Off";
    }
}
