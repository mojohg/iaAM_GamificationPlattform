using System;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class SystemStateController : MonoBehaviour, IObserver
{
    public string statusTopic = "MVG_ESCH/PRODUCTION/MW/KST/0001/ACTIVEMODULES/STATUS";
    public Transform contentPanel;  // Im Inspector zuweisen
    public GameObject moduleRowPrefab;  // Im Inspector zuweisen
    public Color activeColor = Color.green;
    public Color inactiveColor = Color.gray;
    private MsgHandler msgHandler;

    void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver(statusTopic, this);
        }
    }

    public void OnNotify(string eventType, object data)
    {
        if (eventType == statusTopic)
        {
            try
            {
                JArray modulesArray = JArray.Parse(data.ToString());
                UpdateScrollView(modulesArray);
            }
            catch (Exception ex)
            {
                Debug.LogError("❌ Fehler beim Parsen des JSON: " + ex.Message);
            }
        }
    }
private void UpdateScrollView(JArray modulesArray)
{
    // Lösche alle bisherigen Einträge
    foreach (Transform child in contentPanel)
    {
        Destroy(child.gameObject);
    }

    // Für jeden Moduleintrag:
    foreach (var module in modulesArray)
    {
        string moduleId = module["module_id"].ToString();
        string description = module["description"].ToString();
        string status = module["status"].ToString().ToLower(); // Kleinbuchstaben für Vergleich

        // Erzeuge eine Zeile aus dem Prefab
        GameObject row = Instantiate(moduleRowPrefab, contentPanel);
        row.transform.SetParent(contentPanel, false); // Damit Skalierung erhalten bleibt

        // Setze den Text – hier wird angenommen, dass dein Prefab einen Text-Komponenten als Kind hat
        Text infoText = row.GetComponentInChildren<Text>();
        if (infoText != null)
        {
            infoText.text = $"{moduleId} - {description} ({status})";
        }

        // Setze die Hintergrundfarbe anhand des Status
        Image bgImage = row.GetComponentInChildren<Image>();
        if (bgImage != null)
        {
            bgImage.color = (status == "active") ? activeColor : inactiveColor;
        }
    }
}
}
