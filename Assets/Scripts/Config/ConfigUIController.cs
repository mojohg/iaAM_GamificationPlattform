using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rocworks.Mqtt;
[DefaultExecutionOrder(-10)] // läuft vor den meisten Start()-Methoden
public class ConfigUIController : MonoBehaviour
{
    [Header("References")]
    public Rocworks.Mqtt.MqttClient mqttClient;

    [Header("MQTT")]
    public TMP_InputField brokerField;
    public TMP_InputField passwordField;

    [Header("Topic Config")]
    public TMP_InputField companyField;         // company
    public TMP_InputField lineField;            // line
    public TMP_InputField machinegroupField;    // machineGroup
    public TMP_InputField identificationField;  // stationId
    public TMP_InputField agentversionField;    // agentversion (Agent)

    [Header("UI")]
    public Button saveButton;
    public Text previewText;

    [Header("Status (optional)")]
    public Image mqttStatusDot;
    public Color connectedColor = Color.green;
    public Color disconnectedColor = Color.red;

    private string ConfigPath => Path.Combine(Application.persistentDataPath, "topic_config.json");

    private void Start()
    {
        // 1) Lokale Config laden (inkl. entschlüsselter MQTT-Felder)
        bool localLoaded = LoadLocalIfAvailable();

        // 2) TopicSettings → Felder befüllen (falls leer) & Root setzen
        var cfg = TopicSettings.Config;
        // ACHTUNG: konsistent agentversion verwenden
        var root = $"{cfg.company}/{cfg.line}/{cfg.machineGroup}/{cfg.stationId}/{cfg.agentversion}";
        TopicNamespace.SetRoot(root);

        if (previewText != null) previewText.text = root;

        if (string.IsNullOrEmpty(companyField.text))        companyField.text        = cfg.company;
        if (string.IsNullOrEmpty(lineField.text))           lineField.text           = cfg.line;
        if (string.IsNullOrEmpty(machinegroupField.text))   machinegroupField.text   = cfg.machineGroup;
        if (string.IsNullOrEmpty(identificationField.text)) identificationField.text = cfg.stationId;
        if (string.IsNullOrEmpty(agentversionField.text))   agentversionField.text   = cfg.agentversion;

        // 3) MQTT-Felder → zuerst aus JSON (falls geladen), sonst aus mqttClient-Defaults
        if (!localLoaded && mqttClient != null)
        {
            if (brokerField && string.IsNullOrEmpty(brokerField.text))
                brokerField.text = mqttClient.Host;
            if (passwordField && string.IsNullOrEmpty(passwordField.text))
                passwordField.text = mqttClient.Password;
        }

        // 4) Preview aktualisieren
        UpdatePreview();

        // 5) Events
        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);

        TopicSettings.OnChanged += UpdatePreview;

        // 6) MQTT-Status-Indicator (optional)
        if (mqttClient != null && mqttStatusDot != null)
        {
            mqttStatusDot.color = mqttClient.ConnectState ? connectedColor : disconnectedColor;
            mqttClient.OnConnected.AddListener(() => mqttStatusDot.color = connectedColor);
            mqttClient.OnDisconnected.AddListener(() => mqttStatusDot.color = disconnectedColor);
        }

        Debug.Log("[ConfigUI] Start abgeschlossen: Felder initialisiert und ggf. Defaultwerte gesetzt.");
    }

    private void OnDestroy()
    {
        TopicSettings.OnChanged -= UpdatePreview;

        if (saveButton != null)
            saveButton.onClick.RemoveListener(OnSaveClicked);

        if (mqttClient != null && mqttStatusDot != null)
        {
            mqttClient.OnConnected.RemoveAllListeners();
            mqttClient.OnDisconnected.RemoveAllListeners();
        }
    }

    private void OnSaveClicked()
    {
        // 1) TopicSettings zur Laufzeit aktualisieren
        TopicSettings.ApplyRuntimeOverride(
            companyField.text,
            lineField.text,
            machinegroupField.text,
            identificationField.text,
            agentversionField.text
        );

        // 2) Root setzen (triggert RootChanged → MsgHandler remappt + (Un)Subscribe)
        var newRoot = $"{companyField.text}/{lineField.text}/{machinegroupField.text}/{identificationField.text}/{agentversionField.text}";
        TopicNamespace.SetRoot(newRoot);
        if (previewText != null) previewText.text = newRoot;

        // 3) Lokal speichern (inkl. verschlüsselter MQTT-Felder)
        SaveLocal();

        // 4) MQTT: Änderungen anwenden + Reconnect, wenn sich was geändert hat
        if (mqttClient != null)
        {
            string newHost = brokerField ? brokerField.text : "";
            string newPw   = passwordField ? passwordField.text : "";

            bool changed = (mqttClient.Host != newHost) || (mqttClient.Password != newPw);
            if (changed)
            {
                mqttClient.Host = newHost;
                mqttClient.Password = newPw;

                if (mqttClient.Connection != null)
                {
                    try
                    {
                        mqttClient.Connection.Disconnect();
                        mqttClient.Connection.Connect();
                        Debug.Log("[ConfigUI] MQTT reconnected with new settings.");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError("[ConfigUI] MQTT reconnect failed: " + ex.Message);
                    }
                }
            }
        }

        UpdatePreview();
        Debug.Log($"[ConfigUI] Config gespeichert: {TopicSettings.Root}");
    }

    private void UpdatePreview()
    {
        if (!previewText) return;
        string rootFromFields =
            $"{companyField.text}/{lineField.text}/{machinegroupField.text}/{identificationField.text}/{agentversionField.text}";
        previewText.text = rootFromFields;
    }

    // ===== Lokales Speichern & Laden (mit Verschlüsselung für MQTT) =====

    private void SaveLocal()
    {
        var dto = new LocalData
        {
            company       = companyField.text,
            line          = lineField.text,
            machineGroup  = machinegroupField.text,
            stationId     = identificationField.text,
            agentversion  = agentversionField.text,
            mqttBrokerEnc = CryptoUtil.EncryptToB64(brokerField ? brokerField.text : ""),
            mqttPwEnc     = CryptoUtil.EncryptToB64(passwordField ? passwordField.text : "")
        };

        var json = JsonUtility.ToJson(dto, true);
        try
        {
            File.WriteAllText(ConfigPath, json);
            Debug.Log($"[ConfigUI] Lokale Config gespeichert: {ConfigPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ConfigUI] Fehler beim Speichern: {ex.Message}");
        }
    }

    private bool LoadLocalIfAvailable()
    {
        if (!File.Exists(ConfigPath))
            return false;

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var data = JsonUtility.FromJson<LocalData>(json);
            if (data == null) return false;

            // Topic Felder
            companyField.text        = data.company;
            lineField.text           = data.line;
            machinegroupField.text   = data.machineGroup;
            identificationField.text = data.stationId;
            agentversionField.text   = data.agentversion;

            // MQTT Felder (entschlüsseln → Textfelder befüllen)
            string broker = CryptoUtil.DecryptFromB64(data.mqttBrokerEnc);
            string pw     = CryptoUtil.DecryptFromB64(data.mqttPwEnc);
            if (brokerField)   brokerField.text   = broker;
            if (passwordField) passwordField.text = pw;

            // TopicSettings sofort setzen
            TopicSettings.ApplyRuntimeOverride(
                data.company, data.line, data.machineGroup, data.stationId, data.agentversion
            );

            Debug.Log($"[ConfigUI] Lokale Config geladen: {ConfigPath}");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ConfigUI] Konnte lokale Config nicht laden: {ex.Message}");
            return false;
        }
    }

    [System.Serializable]
    private class LocalData
    {
        public string company;
        public string line;
        public string machineGroup;
        public string stationId;
        public string agentversion;

        // verschlüsselte MQTT-Felder
        public string mqttBrokerEnc;
        public string mqttPwEnc;
    }
}
