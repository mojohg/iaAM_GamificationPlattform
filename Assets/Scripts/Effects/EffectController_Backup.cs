using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Rocworks.Mqtt;

public class EffektController_Backup : MonoBehaviour, IObserver
{
    private MsgHandler msgHandler;
    public GameObject[] effects; // Array mit allen Effekten
    private int currentEffectIndex = 0; // Aktueller Effekt
    private float userCoins; // Benutzer-Münzen
    private Coroutine publishCoroutine;

    // Effektdaten wie Freischaltung, Kosten etc.
    private class EffectInfo
    {
        public bool isUnlocked;
        public bool isPurchased;
        public int cost;
        public string name;
    }

    private List<EffectInfo> effectInfos = new List<EffectInfo>();

    public Text costText;             // Anzeige der Kosten
    public GameObject lockIcon;       // Schloss-Icon, falls gesperrt
    public GameObject purchaseButton; // Kaufbutton anzeigen, falls gesperrt
    public GameObject shadeOverlay;   // Overlay, falls gesperrt

    void Start()
    {
        // Effekte initialisieren
        InitializeEffects();

        // MQTT-Handler abonnieren
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/EVALUATION/AUTOTRIGGER", this);
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/EFFECT/PURCHASE/RESPONSE", this);
        }

        // Zeige den ersten Effekt
        ActivateEffect(currentEffectIndex);
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/EVALUATION/AUTOTRIGGER", this);
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/EFFECT/PURCHASE/RESPONSE", this);
        }
    }

    private void InitializeEffects()
    {
        foreach (var effect in effects)
        {
            string effectName = effect.name;
            effectInfos.Add(new EffectInfo
            {
                isUnlocked = false,
                isPurchased = false,
                cost = 100, // Standardkosten (kann aus einer Datenquelle geladen werden)
                name = effectName
            });
        }
    }

    // Trigger-Effekt basierend auf MQTT-Nachricht
    public void OnNotify(string eventType, object data)
    {
        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/EVALUATION/AUTOTRIGGER")
        {
            var jsonData = JObject.Parse(data.ToString());
            bool autoMode = jsonData["Automode"].Value<bool>();

            if (autoMode)
            {
                TriggerEffect(); // Effekt triggern
            }
        }
        else if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/EFFECT/PURCHASE/RESPONSE")
        {
            HandlePurchaseResponse(data.ToString());
        }
    }

    // Effekt auslösen
    public void TriggerEffect()
    {
        if (effects[currentEffectIndex] != null)
        {
            var particleSystem = effects[currentEffectIndex].GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                particleSystem.Play();
            }
        }
    }

    // Effekt wechseln (vorwärts)
    public void NextEffect()
    {
        currentEffectIndex = (currentEffectIndex + 1) % effects.Length;
        ActivateEffect(currentEffectIndex);
        UpdateUI();
        RestartPublishCoroutine();
    }

    // Effekt wechseln (rückwärts)
    public void PreviousEffect()
    {
        currentEffectIndex = (currentEffectIndex - 1 + effects.Length) % effects.Length;
        ActivateEffect(currentEffectIndex);
        UpdateUI();
        RestartPublishCoroutine();
    }

    private void ActivateEffect(int index)
    {
        for (int i = 0; i < effects.Length; i++)
        {
            effects[i].SetActive(i == index);
        }
    }

    private void RestartPublishCoroutine()
    {
        if (publishCoroutine != null)
        {
            StopCoroutine(publishCoroutine);
        }

        EffectInfo currentEffect = effectInfos[currentEffectIndex];
        if (currentEffect.isUnlocked && currentEffect.isPurchased)
        {
            publishCoroutine = StartCoroutine(PublishEffectAfterDelay(currentEffect));
        }
    }

    private IEnumerator PublishEffectAfterDelay(EffectInfo effect)
    {
        yield return new WaitForSeconds(3f);

        JObject effectMessage = new JObject
        {
            { "effect", effect.name },
            { "mqtt", true }
        };

        if (msgHandler != null)
        {
            msgHandler.NotifyObservers("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/EFFECT/ACTIVE", effectMessage);
            Debug.Log($"Published active effect: {effect.name}");
        }
    }

    private void UpdateUI()
    {
        EffectInfo currentEffect = effectInfos[currentEffectIndex];

        if (!currentEffect.isUnlocked)
        {
            lockIcon.SetActive(true);
            purchaseButton.SetActive(false);
            shadeOverlay.SetActive(true);
            costText.text = $"Locked - Cost: {currentEffect.cost}";
        }
        else if (currentEffect.isUnlocked && !currentEffect.isPurchased)
        {
            lockIcon.SetActive(false);
            purchaseButton.SetActive(true);
            purchaseButton.GetComponent<Button>().interactable = true;
            shadeOverlay.SetActive(true);
            costText.text = $"Cost: {currentEffect.cost}";
        }
        else
        {
            lockIcon.SetActive(false);
            purchaseButton.SetActive(false);
            shadeOverlay.SetActive(false);
            costText.text = "Unlocked";
        }
    }

    public void PurchaseCurrentEffect()
    {
        EffectInfo currentEffect = effectInfos[currentEffectIndex];
        if (currentEffect.isUnlocked && !currentEffect.isPurchased)
        {
            JObject purchaseRequest = new JObject
            {
                { "effect", currentEffect.name },
                { "coins", currentEffect.cost },
                { "mqtt", true }
            };

            if (msgHandler != null)
            {
                msgHandler.NotifyObservers("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/EFFECT/PURCHASE", purchaseRequest);
                Debug.Log($"Sent purchase request for effect: {currentEffect.name}");

                costText.text = "Processing purchase...";
            }
        }
    }

    private void HandlePurchaseResponse(string jsonData)
    {
        JObject parsedData = JObject.Parse(jsonData);

        string status = parsedData["status"]?.ToString();
        string effectName = parsedData["effect"]?.ToString();

        var effect = effectInfos.FirstOrDefault(e => e.name == effectName);
        if (effect == null)
        {
            Debug.LogError($"Effect '{effectName}' not found.");
            return;
        }

        if (status == "success")
        {
            effect.isPurchased = true;
            costText.text = "Unlocked";

            if (float.TryParse(parsedData["remaining_coins"]?.ToString(), out float remainingCoins))
            {
                userCoins = remainingCoins;
            }
        }
        else if (status == "error")
        {
            costText.text = "Purchase failed - Not enough coins!";
        }

        UpdateUI();
    }
}
