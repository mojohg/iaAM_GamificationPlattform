using System;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class PrestigeController : MonoBehaviour, IObserver
{
    [SerializeField] private GameObject lockIcon;  // Lock icon to indicate Prestige is locked
    [SerializeField] private Button prestigeButton;    // Prestige button

    private MsgHandler msgHandler;
    private Coroutine buttonHoldCoroutine;

    private void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/PRESTIGE/STATE", this);
        }

        UpdateUI(false);  // Initially disable the button
    }

    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/PRESTIGE/STATE", this);
        }
    }

    public void OnNotify(string eventType, object data)
    {
        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/PRESTIGE/STATE")
        {
            if (data is string jsonString)
            {
                try
                {
                    JObject json = JObject.Parse(jsonString);
                    bool prestigeAvailable = json["Prestige"]?.ToObject<bool>() ?? false;
                    UpdateUI(prestigeAvailable);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing Prestige data: {ex.Message}");
                }
            }
        }
    }

    private void UpdateUI(bool prestigeAvailable)
    {
        if (prestigeAvailable)
        {
            lockIcon.SetActive(false);  // Hide the lock icon
            prestigeButton.interactable = true;  // Enable the button
        }
        else
        {
            lockIcon.SetActive(true);  // Show the lock icon
            prestigeButton.interactable = false;  // Disable the button
        }
    }

    public void OnButtonPress()
    {
        // Start the hold coroutine when the button is pressed
        if (prestigeButton.interactable)
        {
            buttonHoldCoroutine = StartCoroutine(HoldButtonCoroutine());
        }
    }

    public void OnButtonRelease()
    {
        // Stop the coroutine if the button is released early
        if (buttonHoldCoroutine != null)
        {
            StopCoroutine(buttonHoldCoroutine);
            buttonHoldCoroutine = null;
        }
    }

    private System.Collections.IEnumerator HoldButtonCoroutine()
    {
        float holdTime = 3f;  // Time required to hold the button
        float elapsedTime = 0f;

        while (elapsedTime < holdTime)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Button was held for the required time
        SendPrestigeMQTTMessage();
    }

    private void SendPrestigeMQTTMessage()
    {
        // Create the Prestige MQTT message
        JObject message = new JObject
        {
            {"PrestigeActivated", true},
            {"mqtt", true}
        };

        msgHandler.NotifyObservers("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/PRESTIGE/ACTIVATED", message);
        Debug.Log("Prestige activated message sent via MQTT.");
    }
}
