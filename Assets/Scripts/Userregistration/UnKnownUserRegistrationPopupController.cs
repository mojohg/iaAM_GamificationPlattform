using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnknownUserRegistrationPopupController : MonoBehaviour, IObserver
{
    [Header("UI References")]
    [SerializeField] private GameObject popupRoot;

    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_InputField passwordConfirmInput;

    [SerializeField] private TMP_Dropdown languageDropdown;

    [SerializeField] private TextMeshProUGUI messageText;

    [SerializeField] private Button submitButton;
    [SerializeField] private Button cancelButton;

    [Header("MQTT Topics")]
    [SerializeField] private string group = "GAMING";
    [SerializeField] private string registerRequiredCategory = "USERDATA/REGISTER_REQUIRED";
    [SerializeField] private string registerSubmitCategory = "USERDATA/REGISTER_SUBMIT";
    [SerializeField] private string registerResultCategory = "USERDATA/REGISTER_RESULT";

    private MsgHandler msgHandler;

    private string registerRequiredTopic;
    private string registerSubmitTopic;
    private string registerResultTopic;

    private string pendingRfidCode;

    private void Awake()
    {
        msgHandler = FindObjectOfType<MsgHandler>();

        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        ConfigurePasswordInputs();
        InitializeLanguageDropdown();
    }

    private void OnEnable()
    {
        if (msgHandler == null)
        {
            Debug.LogError("[UnknownUserRegistrationPopupController] MsgHandler not found in scene.");
            return;
        }

        registerRequiredTopic = TopicNamespace.TopicFor(group, registerRequiredCategory);
        registerSubmitTopic = TopicNamespace.TopicFor(group, registerSubmitCategory);
        registerResultTopic = TopicNamespace.TopicFor(group, registerResultCategory);

        msgHandler.RegisterObserver(registerRequiredTopic, this);
        msgHandler.RegisterObserver(registerResultTopic, this);

        Debug.Log($"[UnknownUserRegistrationPopupController] Registered for topic: {registerRequiredTopic}");
        Debug.Log($"[UnknownUserRegistrationPopupController] Registered for topic: {registerResultTopic}");
    }

    private void OnDisable()
    {
        if (msgHandler == null)
            return;

        if (!string.IsNullOrEmpty(registerRequiredTopic))
            msgHandler.RemoveObserver(registerRequiredTopic, this);

        if (!string.IsNullOrEmpty(registerResultTopic))
            msgHandler.RemoveObserver(registerResultTopic, this);
    }

    public void OnNotify(string topic, object data)
    {
        if (!(data is string jsonString) || string.IsNullOrWhiteSpace(jsonString))
            return;

        try
        {
            if (topic == registerRequiredTopic)
            {
                HandleRegisterRequired(jsonString);
                return;
            }

            if (topic == registerResultTopic)
            {
                HandleRegisterResult(jsonString);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnknownUserRegistrationPopupController] Parse error: {ex.Message}");
        }
    }

    private void HandleRegisterRequired(string jsonString)
    {
        JObject json = JObject.Parse(jsonString);

        pendingRfidCode = json["rfid_code"]?.ToString();

        if (string.IsNullOrWhiteSpace(pendingRfidCode))
        {
            Debug.LogWarning("[UnknownUserRegistrationPopupController] REGISTER_REQUIRED without rfid_code.");
            return;
        }

        ClearForm();

        SetMessage($"Unbekannter RFID-Code erkannt: {pendingRfidCode}");

        if (popupRoot != null)
            popupRoot.SetActive(true);

        Debug.Log($"[UnknownUserRegistrationPopupController] Open popup for RFID: {pendingRfidCode}");
    }

    private void HandleRegisterResult(string jsonString)
    {
        JObject json = JObject.Parse(jsonString);

        bool success = json["success"]?.Value<bool>() ?? false;
        string message = json["message"]?.ToString() ?? "";

        SetMessage(message);

        if (success)
        {
            Debug.Log("[UnknownUserRegistrationPopupController] Registration successful. Closing popup.");

            if (popupRoot != null)
                popupRoot.SetActive(false);

            ClearForm();
        }
        else
        {
            Debug.LogWarning($"[UnknownUserRegistrationPopupController] Registration failed: {message}");
        }
    }

    private void OnSubmitClicked()
    {
        if (!ValidateForm())
            return;

        JObject payload = new JObject
        {
            ["mqtt"] = true,

            ["rfid_code"] = pendingRfidCode,
            ["username"] = usernameInput.text.Trim(),
            ["email"] = emailInput.text.Trim(),
            ["password"] = passwordInput.text,
            ["password_confirm"] = passwordConfirmInput.text,
            ["language_str"] = GetSelectedLanguage(),
            ["color_profile_name"] = "Profile 1"
        };

        msgHandler.NotifyObservers(registerSubmitTopic, payload);

        SetMessage("User wird angelegt...");

        Debug.Log($"[UnknownUserRegistrationPopupController] REGISTER_SUBMIT sent for RFID: {pendingRfidCode}");
    }

    private void OnCancelClicked()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);

        ClearForm();

        Debug.Log("[UnknownUserRegistrationPopupController] Registration popup cancelled.");
    }

    private bool ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(pendingRfidCode))
        {
            SetMessage("RFID-Code fehlt.");
            return false;
        }

        if (usernameInput == null || string.IsNullOrWhiteSpace(usernameInput.text))
        {
            SetMessage("Bitte Username eingeben.");
            return false;
        }

        if (emailInput == null || string.IsNullOrWhiteSpace(emailInput.text))
        {
            SetMessage("Bitte E-Mail eingeben.");
            return false;
        }

        if (!emailInput.text.Contains("@"))
        {
            SetMessage("Bitte gültige E-Mail eingeben.");
            return false;
        }

        if (passwordInput == null || string.IsNullOrWhiteSpace(passwordInput.text))
        {
            SetMessage("Bitte Passwort eingeben.");
            return false;
        }

        if (passwordConfirmInput == null || passwordInput.text != passwordConfirmInput.text)
        {
            SetMessage("Passwörter stimmen nicht überein.");
            return false;
        }

        return true;
    }

    private void SetMessage(string message)
    {
        if (messageText != null)
            messageText.text = message;

        Debug.Log($"[UnknownUserRegistrationPopupController] {message}");
    }

    private void ClearForm()
    {
        if (usernameInput != null)
            usernameInput.text = "";

        if (emailInput != null)
            emailInput.text = "";

        if (passwordInput != null)
            passwordInput.text = "";

        if (passwordConfirmInput != null)
            passwordConfirmInput.text = "";

        if (languageDropdown != null)
        {
            languageDropdown.value = 0;
            languageDropdown.RefreshShownValue();
        }
    }

    private void InitializeLanguageDropdown()
    {
        if (languageDropdown == null)
            return;

        languageDropdown.ClearOptions();

        languageDropdown.AddOptions(new List<string>
        {
            "de",
            "en"
        });

        languageDropdown.value = 0;
        languageDropdown.RefreshShownValue();
    }

    private void ConfigurePasswordInputs()
    {
        if (passwordInput != null)
        {
            passwordInput.contentType = TMP_InputField.ContentType.Password;
            passwordInput.ForceLabelUpdate();
        }

        if (passwordConfirmInput != null)
        {
            passwordConfirmInput.contentType = TMP_InputField.ContentType.Password;
            passwordConfirmInput.ForceLabelUpdate();
        }
    }

    private string GetSelectedLanguage()
    {
        if (languageDropdown == null || languageDropdown.options.Count == 0)
            return "de";

        return languageDropdown.options[languageDropdown.value].text;
    }
}