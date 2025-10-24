using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using Rocworks.Mqtt;

public class EffektController : MonoBehaviour, IObserver
{
    private MsgHandler msgHandler;

    [SerializeField] private Text costText;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private GameObject shadeOverlay;
    [SerializeField] private GameObject purchaseButton;
    [SerializeField] private Text requiredScoreText;
    [SerializeField] private Text requiredLevelText;

    public GameObject[] effects; // Array mit allen Effekten
    private int currentEffectIndex = 0;
    private List<EffectInfo> effectInfos = new List<EffectInfo>();
    private float userCoins;
    private Coroutine publishCoroutine;

    private class EffectInfo
    {
        public string name;
        public int cost;
        public int requiredScore;
        public int requiredLevel;
        public bool isUnlocked;
        public bool isPurchased;
    }

    private void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("UserDataUpdate/Effects", this);
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/EVALUATION/AUTOTRIGGER", this);
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/EFFECT/PURCHASE/RESPONSE", this);
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/001/GAMING/USERDATA/EFFECTS", this);
        }
                // Zeige den ersten Effekt
        ActivateEffect(currentEffectIndex);
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver("UserDataUpdate/Effects", this);
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/EVALUATION/AUTOTRIGGER", this);
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/EFFECT/PURCHASE/RESPONSE", this);
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/001/GAMING/USERDATA/EFFECTS", this);
        }
    }

    public void OnNotify(string eventType, object data)
    {
        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/EVALUATION/AUTOTRIGGER")
        {
            var jsonData = JObject.Parse(data.ToString());
    // Extrahiere WorkstepID und Automode
            string workstepID = jsonData["WorkstepID"]?.ToString();
            string autoTriggerStatus = jsonData["AutoTrigger"]?.ToString();
            bool automode = jsonData["Automode"]?.ToObject<bool>() ?? false;

            Debug.Log($"Received AutoTrigger event: WorkstepID={workstepID}, AutoTrigger={autoTriggerStatus}, Automode={automode}");

            // Prüfe, ob der AutoTrigger-Status "Completed" ist
            if (autoTriggerStatus == "Completed" && automode)
            {
                TriggerEffect(); // Effekt triggern
            }       
        }
        else if (eventType == "UserDataUpdate/Effects")
        {
            string jsonString = data as string;
            if (jsonString != null)
            {
                LoadEffectStatusFromJson(jsonString);
            }
        }
        else if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/EFFECT/PURCHASE/RESPONSE")
        {
            string jsonString = data as string;
            if (jsonString != null)
            {
                HandlePurchaseResponse(jsonString);
            }
        }
        else if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/001/GAMING/USERDATA/EFFECTS")
        {
            string jsonString = data as string;
            if(jsonString != null)
            {
                UpdateEffectStatusFromJson(jsonString);
            }
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

   private void LoadEffectStatusFromJson(string jsonString)
{
    JArray responseArray = JArray.Parse(jsonString);
    effectInfos.Clear();

    foreach (var item in responseArray)
    {
        string effectName = item["name"]?.ToString();
        if (string.IsNullOrEmpty(effectName))
        {
            Debug.LogWarning("Missing 'name' field in JSON item.");
            continue;
        }

        // Suche nach einem vorhandenen GameObject anhand des Namens
        var effectObject = effects.FirstOrDefault(e => e.name.Equals(effectName, System.StringComparison.Ordinal));
        if (effectObject != null)
        {
            // Nur wenn das GameObject existiert, wird ein Effekt-Info-Eintrag erstellt.
            effectInfos.Add(new EffectInfo
            {
                name = effectName,
                cost = item["required_coins"]?.ToObject<int>() ?? 0,
                requiredScore = item["required_score"]?.ToObject<int>() ?? 0,
                requiredLevel = item["required_level"]?.ToObject<int>() ?? 0,
                isUnlocked = item["is_unlocked"]?.ToObject<bool>() ?? false,
                isPurchased = item["is_purchased"]?.ToObject<bool>() ?? false,
            });
            // Setze den aktiven Status des GameObjects basierend auf dem JSON-Wert
            effectObject.SetActive(item["is_active"]?.ToObject<bool>() ?? false);
        }
        else
        {
            Debug.LogWarning($"Effect '{effectName}' from JSON not found among available GameObjects.");
            // Hier nichts hinzufügen – so werden nur vorhandene Effekte berücksichtigt.
        }
    }

    // Optionale Sortierung: Falls du möchtest, dass die Reihenfolge in effectInfos
    // der Reihenfolge im effects-Array entspricht, kannst du wie folgt sortieren:
    effectInfos = effectInfos.OrderBy(ei =>
    {
        var go = effects.FirstOrDefault(e => e.name.Equals(ei.name, System.StringComparison.Ordinal));
        return go != null ? System.Array.IndexOf(effects, go) : int.MaxValue;
    }).ToList();

    currentEffectIndex = 0;
    UpdateUI();
}

private void UpdateEffectStatusFromJson(string jsonString)
{
    JArray responseArray = JArray.Parse(jsonString);
    // Leere die vorhandenen Infos, wir aktualisieren alles neu
    effectInfos.Clear();

    foreach (var item in responseArray)
    {
        string effectName = item["name"]?.ToString();
        if (string.IsNullOrEmpty(effectName))
        {
            Debug.LogWarning("Missing 'name' field in JSON item.");
            continue;
        }

        // Suche nach dem vorhandenen GameObject
        var effectObject = effects.FirstOrDefault(e => e.name.Equals(effectName, System.StringComparison.Ordinal));
        if (effectObject != null)
        {
            effectInfos.Add(new EffectInfo
            {
                name = effectName,
                cost = item["required_coins"]?.ToObject<int>() ?? 0,
                requiredScore = item["required_score"]?.ToObject<int>() ?? 0,
                requiredLevel = item["required_level"]?.ToObject<int>() ?? 0,
                isUnlocked = item["is_unlocked"]?.ToObject<bool>() ?? false,
                isPurchased = item["is_purchased"]?.ToObject<bool>() ?? false,
            });
            effectObject.SetActive(item["is_active"]?.ToObject<bool>() ?? false);
        }
        else
        {
            Debug.LogWarning($"Effect '{effectName}' from JSON not found among available GameObjects.");
        }
    }

    // Optional: Sortiere die Liste anhand der Reihenfolge im effects-Array
    effectInfos = effectInfos.OrderBy(ei =>
    {
        var go = effects.FirstOrDefault(e => e.name.Equals(ei.name, System.StringComparison.Ordinal));
        return go != null ? System.Array.IndexOf(effects, go) : int.MaxValue;
    }).ToList();
    UpdateUI();
}

    private void UpdateUI()
    {
        if (effectInfos.Count == 0)
        {
            Debug.Log("No effects loaded. Displaying default UI.");
            lockIcon.SetActive(true);
            shadeOverlay.SetActive(true);
            costText.text = "No Effects Available";
            requiredScoreText.text = "";
            requiredLevelText.text = "";
            return;
        }

        EffectInfo currentEffect = effectInfos[currentEffectIndex];

        if (!currentEffect.isUnlocked)
        {
            lockIcon.SetActive(true);
            purchaseButton.SetActive(false);
            shadeOverlay.SetActive(true);
            requiredScoreText.text = $"Required Score: {currentEffect.requiredScore}";
            requiredLevelText.text = $"Required Level: {currentEffect.requiredLevel}";
            costText.text = "Locked";
        }
        else if (!currentEffect.isPurchased)
        {
            lockIcon.SetActive(false);
            purchaseButton.SetActive(true);
            shadeOverlay.SetActive(true);
            costText.text = $"Cost: {currentEffect.cost}";
            requiredScoreText.text = "";
            requiredLevelText.text = "";
        }
        else
        {
            lockIcon.SetActive(false);
            purchaseButton.SetActive(false);
            shadeOverlay.SetActive(false);
            costText.text = "Unlocked";
            requiredScoreText.text = "";
            requiredLevelText.text = "";
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
            // Kauf fehlgeschlagen
            string errorMessage = parsedData["error"]?.ToString() ?? "Unknown error";
            costText.text = "Purchase failed - Not enough coins!";
            Debug.LogWarning($"Purchase failed for background '{effectName}': {errorMessage}");
            return;
        }
        else
        {
            Debug.LogError("Invalid response status.");
            return;
        }

        // UI aktualisieren
        UpdateUI();
    }

    public void NextEffect()
    {
        currentEffectIndex = (currentEffectIndex + 1) % effectInfos.Count;
        ActivateEffect(currentEffectIndex);
        UpdateUI();
        RestartPublishCoroutine();
        TriggerEffect();
        Debug.Log($"Switched to next effect: {effectInfos[currentEffectIndex].name}");
    }

    public void PreviousEffect()
    {
        currentEffectIndex = (currentEffectIndex - 1 + effectInfos.Count) % effectInfos.Count;
        ActivateEffect(currentEffectIndex);
        UpdateUI();
        RestartPublishCoroutine();
        TriggerEffect();
        Debug.Log($"Switched to previous effect: {effectInfos[currentEffectIndex].name}");
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

                // Temporäre Anzeige während des Kaufprozesses
                costText.text = "Processing purchase...";
            }
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

    private void ActivateEffect(int index)
    {
        for (int i = 0; i < effects.Length; i++)
        {
            effects[i].SetActive(i == index);
        }
    }

}
