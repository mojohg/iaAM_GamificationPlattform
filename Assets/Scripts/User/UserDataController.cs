using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class UserdataController : MonoBehaviour, IObserver
{
    [SerializeField] private Text coinText;      // UI to display coins
    [SerializeField] private Text scoreText;     // UI to display score
    [SerializeField] private Text usernameText;  // UI to display username
    [SerializeField] private Text levelText;  // UI to display username
    [SerializeField] private GameObject adminOverlay;

    private MsgHandler msgHandler;

    private void Start()
    {
        // Den MsgHandler in der Szene finden
        msgHandler = FindObjectOfType<MsgHandler>();

        if (msgHandler != null)
        {
            // Registriere den Controller für die relevanten Topics
            // Die ersten beiden Topics "MVG_ESCH..." sind vermutlich externe Rohdaten im JSON-Format
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/SCORE", this);
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/COINS", this);

            // Die folgenden Topics werden intern aus dem MsgHandler gesendet und liefern bereits fertige Werte (float oder string)
            msgHandler.RegisterObserver("UserDataUpdate/Username", this);
            msgHandler.RegisterObserver("UserDataUpdate/Coins", this);
            msgHandler.RegisterObserver("UserDataUpdate/Score", this);
            msgHandler.RegisterObserver("UserDataUpdate/Level", this);
        }
        else
        {
            Debug.LogError("MsgHandler not found in the scene!");
        }
    }

    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            // Observer sauber entfernen
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/SCORE", this);
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/COINS", this);

            msgHandler.RemoveObserver("UserDataUpdate/Username", this);
            msgHandler.RemoveObserver("UserDataUpdate/Coins", this);
            msgHandler.RemoveObserver("UserDataUpdate/Score", this);
            msgHandler.RemoveObserver("UserDataUpdate/Level", this);
        }
    }

    public void OnNotify(string eventType, object data)
    {
        Debug.Log($"UserdataController: Received event for topic {eventType}");

        // Verarbeite die verschiedenen Topics
        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/SCORE")
        {
            // Hier erwartet man vermutlich ein JSON-Objekt oder String für Score
            HandleExternalScoreData(data);
        }
        else if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/COINS")
        {
            // Hier erwartet man vermutlich ein JSON-Objekt oder String für Coins
            HandleExternalCoinData(data);
        }
        else if (eventType == "UserDataUpdate/Username")
        {
            // Hier ist data bereits ein string (Username)
            HandleUsernameData(data);
        }
        else if (eventType == "UserDataUpdate/Coins")
        {
            // Hier ist data bereits ein float (Coins)
            HandleInternalCoinData(data);
        }
        else if (eventType == "UserDataUpdate/Score")
        {
            // Hier ist data bereits ein float (Score)
            HandleInternalScoreData(data);
        }
          else if (eventType == "UserDataUpdate/Level")
        {
            // Hier ist data bereits ein float (Score)
            Debug.Log("Level Info received");
            HandleInternalLevelData(data);
        }
        else
        {
            Debug.LogWarning($"Unhandled event type: {eventType}");
        }
    }

    private void HandleExternalScoreData(object data)
    {
        // Externe Daten, wahrscheinlich JSON
        Debug.Log($"Handling external score data: {data}");
        if (data is string jsonData)
        {
            JObject parsedData = JObject.Parse(jsonData);

            if (parsedData["score"] != null && float.TryParse(parsedData["score"].ToString(), out float updatedScore))
            {
                scoreText.text = $"Score: {updatedScore:F2}"; // Zwei Dezimalstellen
                Debug.Log($"Updated score from external data: {updatedScore}");
            }
            else
            {
                Debug.LogError($"Invalid or missing 'score' field in JSON: {jsonData}");
            }
        }
        else
        {
            Debug.LogError($"Invalid data type for external score data: {data.GetType()}");
        }
    }

    private void HandleExternalLevelData(object data)
        {
            // Externe Daten, wahrscheinlich JSON
            Debug.Log($"Handling external Level data: {data}");
            if (data is string jsonData)
            {
                JObject parsedData = JObject.Parse(jsonData);

                if (parsedData["level"] != null && float.TryParse(parsedData["level"].ToString(), out float updatedLevel))
                {
                    levelText.text = $"Level: {updatedLevel:F2}";
                    Debug.Log($"Updated Level from external data: {updatedLevel}");
                }
                else
                {
                    Debug.LogError($"Invalid or missing 'level' field in JSON: {jsonData}");
                }
            }
            else
            {
                Debug.LogError($"Invalid data type for external level data: {data.GetType()}");
            }
        }

    private void HandleExternalCoinData(object data)
    {
        // Externe Daten, wahrscheinlich JSON
        Debug.Log($"Handling external coin data: {data}");
        if (data is string jsonData)
        {
            JObject parsedData = JObject.Parse(jsonData);

            if (parsedData["coins"] != null && float.TryParse(parsedData["coins"].ToString(), out float updatedCoins))
            {
                coinText.text = $"Coins: {updatedCoins:F2}";
                Debug.Log($"Updated coins from external data: {updatedCoins}");
            }
            else
            {
                Debug.LogError($"Invalid or missing 'coins' field in JSON: {jsonData}");
            }
        }
        else
        {
            Debug.LogError($"Invalid data type for external coin data: {data.GetType()}");
        }
    }

    private void HandleUsernameData(object data)
    {
        Debug.Log($"Handling username data: {data}");
        if (data is string updatedUsername)
        {
            usernameText.text = $"Username: {updatedUsername}";
            Debug.Log($"Updated username: {updatedUsername}");
            if (updatedUsername == "AdminUser")
            {
                adminOverlay.SetActive(false); // Admin kann Buttons nutzen
                Debug.Log("Admin Mode activated");
            }
            else
            {
                adminOverlay.SetActive(true); // Standard-User: Buttons blockieren
                Debug.Log("Standard Mode: Admin buttons disabled");
            }
        }
        else
        {
            Debug.LogError($"Invalid data type for username: {data.GetType()}");
        }
    }

    private void HandleInternalCoinData(object data)
    {
        // Hier kommt ein float rein, kein JSON
        Debug.Log($"Handling internal coin data: {data}");
        if (data is float updatedCoins)
        {
            coinText.text = $"Coins: {updatedCoins:F2}";
            Debug.Log($"Updated coins (internal): {updatedCoins}");
        }
        else
        {
            Debug.LogError($"Invalid data type for internal coins: {data.GetType()}");
        }
    }

    private void HandleInternalLevelData(object data)
        {
            // Hier kommt ein float rein, kein JSON
            Debug.Log($"Handling internal level data: {data}");
            if (data is float updatedLevel)
            {
                levelText.text = $"Level: {updatedLevel:F2}";
                Debug.Log($"Updated level (internal): {updatedLevel}");
            }
            else
            {
                Debug.LogError($"Invalid data type for internal level: {data.GetType()}");
            }
        }
    private void HandleInternalScoreData(object data)
    {
        // Hier kommt ein float rein, kein JSON
        Debug.Log($"Handling internal score data: {data}");
        if (data is float updatedScore)
        {
            scoreText.text = $"Score: {updatedScore:F2}";
            Debug.Log($"Updated score (internal): {updatedScore}");
        }
        else
        {
            Debug.LogError($"Invalid data type for internal score: {data.GetType()}");
        }
    }
}