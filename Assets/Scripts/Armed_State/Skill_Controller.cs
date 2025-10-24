using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System;
using Rocworks.Mqtt;

public class SkillController : MonoBehaviour, IObserver
{
    public Button armedStateSkillButton;  // Assign your button here in the inspector
    private bool isArmedStateSkillOn = false;
    private bool isArmedStateSkillsOn = false;  // Track skill armed state
    private Coroutine sendDelayCoroutine;  // Store the running coroutine
    private MsgHandler msgHandler;   // Reference to your MQTT message handler

    // Colors for button states
    private Color onColor = Color.green;
    private Color offColor = Color.gray;

    void Start()
    {
        // Assign the button click listener
        armedStateSkillButton.onClick.AddListener(OnArmedStateSkillButtonClick);

        // Find and register the MsgHandler for MQTT communication
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("UserDataUpdate/ArmedStateSkill", this);
             msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/CONFIG/UPDATE", this);
        }

        // Initialize the button color
        UpdateButtonUI(isArmedStateSkillOn);
    }

    // Handle MQTT notification for Skill armed state
    public void OnNotify(string eventType, object data)
    {
        if (eventType == "UserDataUpdate/ArmedStateSkill")
        {
            string status = data as string;
            isArmedStateSkillOn = status == "On";
            UpdateButtonUI(isArmedStateSkillOn);  // Update the UI to reflect the state
        }
        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/CONFIG/UPDATE")
        {

            try
            {
                JObject jsonMessage = JObject.Parse(data.ToString());
                if (jsonMessage["armed_state_skills"] != null)
                {
                    isArmedStateSkillsOn = jsonMessage["armed_state_skills"].ToObject<bool>();
                    UpdateButtonUI(isArmedStateSkillsOn);
                    Debug.Log($"AutoMode updated from MQTT: {isArmedStateSkillsOn}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing MQTT AutoMode update: {ex.Message}");
            }
        }
    }

    // Button click event handler
    private void OnArmedStateSkillButtonClick()
    {
        // Toggle the Skill armed state and update the UI immediately
        isArmedStateSkillOn = !isArmedStateSkillOn;
        UpdateButtonUI(isArmedStateSkillOn);

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
           // Create the message with the "should_publish" flag
        JObject message = new JObject
        {
            { "armed_state", isArmedStateSkillOn ? "On" : "Off" },
            { "mqtt", true }  // This flag indicates the message should be published
        };
        // Send the message via MQTT
        if (msgHandler != null)
        {
            Debug.Log($"Sent MQTT message: {message}");
            msgHandler.NotifyObservers("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/ARMEDSTATE/SKILL/SAVE", message);
        }


    }

    // Update the button color based on Skill armed state
    private void UpdateButtonUI(bool isOn)
    {
        // Get the current ColorBlock of the button
        ColorBlock cb = armedStateSkillButton.colors;

        // Update all relevant color states in the ColorBlock
        cb.normalColor = isOn ? onColor : offColor;
        cb.highlightedColor = isOn ? onColor : offColor;
        cb.pressedColor = isOn ? onColor : offColor * 0.8f;  // Slightly darker when pressed
        cb.selectedColor = isOn ? onColor : offColor;
        cb.disabledColor = Color.gray;  // Optional: a color for when the button is disabled

        // Apply the updated ColorBlock back to the button
        armedStateSkillButton.colors = cb;

        // Optionally, update the button text to show "On" or "Off"
        armedStateSkillButton.GetComponentInChildren<Text>().text = isOn ? "Skill On" : "Skill Off";
    }
}
