using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System;

namespace Rocworks.Mqtt
{
    /// <summary>
    /// The msgHandler class handles MQTT messages and notifies registered observers about events.
    /// Implements the ISubject interface to manage observers.
    /// </summary>
    public class MsgHandler : MonoBehaviour, ISubject
    {
        [SerializeField] private float userCoins;
        [SerializeField] private float userScore;
        [SerializeField] private float userLevel;
        [SerializeField] private string username;
        private string  userRights;
        private JArray backgrounds;
        private JArray badges;
        private JArray avatars;
        private JArray effects;
        private JArray music;
        private String automodeStatus;
        private JArray scorePresets;

        /// <summary>
        /// The MQTT client for handling communication.
        /// </summary>
        public MqttClient MqttClient;

        /// <summary>
        /// Toggle to display the connection status.
        /// </summary>
        public Toggle IsConnected;

        // Dictionary to manage observers by topic
        private Dictionary<string, List<IObserver>> topicObservers = new Dictionary<string, List<IObserver>>();

        /// <summary>
        /// Indicates whether the MQTT client is connected.
        /// </summary>
        public bool IsMqttConnected { get; private set; }

        /// <summary>
        /// Initializes the msgHandler and subscribes to MQTT events.
        /// </summary>
        void Start()
        {
            if (MqttClient == null)
                {
                    Debug.LogError("MQTT Client is not assigned in the Inspector.");
                    return;
                }
            // Subscribe to MQTT events
            MqttClient.OnConnected.AddListener(OnConnected);
            MqttClient.OnDisconnected.AddListener(OnDisconnected);
            MqttClient.OnMessageArrived.AddListener(OnMessageArrived);
        }

        /// <summary>
        /// Cleans up the subscriptions to MQTT events when the object is destroyed.
        /// </summary>
        void OnDestroy()
        {
            // Unsubscribe from MQTT events
            MqttClient.OnConnected.RemoveListener(OnConnected);
            MqttClient.OnDisconnected.RemoveListener(OnDisconnected);
            MqttClient.OnMessageArrived.RemoveListener(OnMessageArrived);
        }

        /// <summary>
        /// Registers an observer to receive notifications for a specific topic.
        /// </summary>
        /// <param name="topic">The topic to subscribe to.</param>
        /// <param name="observer">The observer to register.</param>
public void RegisterObserver(string topic, IObserver observer)
{
    if (!topicObservers.ContainsKey(topic))
    {
        topicObservers[topic] = new List<IObserver>();
        Debug.Log($"Created new topic list for: {topic}");
    }

    if (!topicObservers[topic].Contains(observer))
    {
        topicObservers[topic].Add(observer);
        Debug.Log($"Observer registered for topic: {topic}");
    }
    else
    {
        Debug.LogWarning($"Observer is already registered for topic: {topic}");
    }
}

        /// <summary>
        /// Removes an observer so it no longer receives notifications for a specific topic.
        /// </summary>
        /// <param name="topic">The topic to unsubscribe from.</param>
        /// <param name="observer">The observer to remove.</param>
        public void RemoveObserver(string topic, IObserver observer)
        {
            if (topicObservers.ContainsKey(topic))
            {
                if (topicObservers[topic].Contains(observer))
                {
                    topicObservers[topic].Remove(observer);
                }
            }
        }

        /// <summary>
        /// Notifies all registered observers of an event for a specific topic.
        /// </summary>
        /// <param name="topic">The topic of the event.</param>
        /// <param name="data">Any data associated with the event.</param>
        public void NotifyObservers(string topic, object data)
{
    if (topicObservers.ContainsKey(topic))
    {
        foreach (var observer in topicObservers[topic])
        {
            // Direkt OnNotify aufrufen, egal welcher Typ
            observer.OnNotify(topic, data);
        }
    }

    if (data is JObject message)
    {
        bool shouldPublish = message["mqtt"] != null && (bool)message["mqtt"];
        if (shouldPublish)
        {
            PublishMessage(topic, message.ToString());
        }
    }
}

        private void PublishMessage(string topic, string payload)
        {
            if (MqttClient != null)
            {
                Debug.Log($"Publishing message to topic {topic}: {payload}");
                MqttClient.Connection.Publish(topic, payload);
            }
            else
            {
                Debug.LogError("MQTT Client is not connected, message cannot be published.");
            }
        }
        
        /// <summary>
        /// Handles incoming MQTT messages and notifies observers based on the message topic.
        /// </summary>
        /// <param name="message">The received MQTT message.</param>
        public void OnMessageArrived(MqttMessage message)
        {
            Debug.Log($"Topic: {message.GetTopic()}, Payload: {message.GetString()}");
            NotifyObservers(message.GetTopic(), message.GetString()); 
            if (message.GetTopic() == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA")
            {
                HandleUserData(message.GetString());
            }
            else if (message.GetTopic() == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/RESPONSE")
            {
                Debug.Log("Login information recieved");
                HandleLoginData(message.GetString());
            }
            else if (message.GetTopic() == "MVG_ESCH/PRODUCTION/MW/KST/0001/EVALUATION/RESULT")
            {
                Debug.Log("Result recieved");
                HandleEvaluationData(message.GetString());
            }
            else if (message.GetTopic() == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/SCORE")
            {
                // <-- Änderung: Hier externe Score-Daten verarbeiten
                HandleScoreData(message.GetString());
            }
            else if (message.GetTopic() == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/COINS") // <-- Änderung: Externe Coins-Daten behandeln
            {
                HandleCoinsData(message.GetString()); // <-- Änderung: Neue Methode für Coins
            }
            else 
            {
                Debug.Log("No matching Topic");
            }
        }

    
        /// <summary>
        /// Called when the MQTT client connects to the broker.
        /// </summary>
        private void OnConnected()
        {
            Debug.Log("Connected to MQTT Broker");
            IsMqttConnected = true;
        }

        /// <summary>
        /// Called when the MQTT client disconnects from the broker.
        /// </summary>
        private void OnDisconnected()
        {
            Debug.Log("Disconnected from MQTT Broker");
            IsMqttConnected = false;
        }

        private void HandleUserData(string jsonData)
        {
            // Parse the incoming JSON to extract user data
            JObject parsedData = JObject.Parse(jsonData);
            username = parsedData["username"]?.ToString();
            userCoins = parsedData["coins"] != null ? (int)parsedData["coins"] : 0;
            userScore = parsedData["score"] != null ? (int)parsedData["score"] : 0;
            userLevel = parsedData["level"] != null ? (int)parsedData["level"] : 0;
            // Check for automode status
            automodeStatus = parsedData["automode"] != null ? parsedData["automode"].ToString() : "Off";

            Debug.Log($"Username: {username}, Coins: {userCoins}, Score: {userScore}, Level: {userLevel}");
            NotifyUI();
        }
     private void HandleScoreData(string jsonData)
        {
            try
            {
                // JSON-String in ein JObject parsen
                JObject parsedData = JObject.Parse(jsonData);

                // Überprüfen, ob "score" im JSON enthalten ist
                if (parsedData["score"] != null)
                {
                    // "score" extrahieren und verarbeiten
                    if (float.TryParse(parsedData["score"].ToString(), out float parsedScore))
                    {
                        userScore = parsedScore; // Score setzen
                        Debug.Log($"Received and parsed Score: {userScore}");
                        NotifyObservers("UserDataUpdate/Score", userScore);
                    }
                    else
                    {
                        Debug.LogError($"Failed to parse 'score' as float: {parsedData["score"]}");
                    }
                }
                else
                {
                    Debug.LogError("Score not found in the received JSON data.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing JSON data: {ex}");
            }
        }

        // <-- Änderung: Neue Methode zum Handling der externen Coins-Daten
        private void HandleCoinsData(string jsonData)
        {
            try
            {
                JObject parsedData = JObject.Parse(jsonData);
                if (parsedData["coins"] != null)
                {
                    if (float.TryParse(parsedData["coins"].ToString(), out float parsedCoins))
                    {
                        userCoins = parsedCoins;
                        Debug.Log($"Received and parsed Coins: {userCoins}");
                        NotifyObservers("UserDataUpdate/Coins", userCoins);
                    }
                    else
                    {
                        Debug.LogError($"Failed to parse 'coins' as float: {parsedData["coins"]}");
                    }
                }
                else
                {
                    Debug.LogError("Coins not found in the received JSON data.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing coins JSON data: {ex}");
            }
        }

        private void HandleLoginData(string jsonData)
        {
            JObject parsedData = JObject.Parse(jsonData);
            
            if (parsedData["status"] != null && parsedData["status"].ToString() == "success")
            {
                JObject userData = (JObject)parsedData["user_data"];
                username = userData["username"]?.ToString();
                userCoins = userData["coins"] != null ? (float)userData["coins"] : 0;
                userScore = userData["score"] != null ? (float)userData["score"] : 0;
                userLevel = userData["level"] != null ? (float)userData["level"] : 0;
                userRights = userData["rights"]?.ToString();
                badges = (JArray)userData["badges"];
                backgrounds = (JArray)userData["backgrounds"];
                music = (JArray)userData["unlocked_music"];
                avatars = (JArray)userData["unlocked_characters"];
                effects = (JArray)userData["effects"];
                scorePresets = (JArray)userData["score_presets"];
                NotifyUI();
                
            }
        }

       private void HandleEvaluationData(string jsonData)
{
    try
    {
        // Parse the incoming JSON into a JObject
        JObject parsedData = JObject.Parse(jsonData);

        // Initialize variables with default values
        float interpretationTime = 0f;
        float processingTime = 0f;
        float errorCorrectionTime = 0f;
        int errorCount = 0;
        bool completed = false;
        List<string> issues = new List<string>();

        // Interpretation Time
        if (parsedData["interpretation_time"] != null && 
            float.TryParse(parsedData["interpretation_time"].ToString(), out float interpretation))
        {
            interpretationTime = interpretation;
        }
        else
        {
            Debug.LogWarning("Invalid or missing 'interpretation_time' in evaluation data. Defaulting to 0.");
        }

        // Processing Time
        if (parsedData["processing_time"] != null && 
            float.TryParse(parsedData["processing_time"].ToString(), out float processing))
        {
            processingTime = processing;
        }
        else
        {
            Debug.LogWarning("Invalid or missing 'processing_time' in evaluation data. Defaulting to 0.");
        }

        // Error Correction Time
        if (parsedData["error_correction_time"] != null && 
            float.TryParse(parsedData["error_correction_time"].ToString(), out float errorCorrection))
        {
            errorCorrectionTime = errorCorrection;
        }
        else
        {
            Debug.LogWarning("Invalid or missing 'error_correction_time' in evaluation data. Defaulting to 0.");
        }

        // Error Count
        if (parsedData["error_count"] != null && 
            int.TryParse(parsedData["error_count"].ToString(), out int errors))
        {
            errorCount = errors;
        }
        else
        {
            Debug.LogWarning("Invalid or missing 'error_count' in evaluation data. Defaulting to 0.");
        }

        // Completed
        if (parsedData["completed"] != null && 
            bool.TryParse(parsedData["completed"].ToString(), out bool isCompleted))
        {
            completed = isCompleted;
        }
        else
        {
            Debug.LogWarning("Invalid or missing 'completed' in evaluation data. Defaulting to false.");
        }

        // Issues
        if (parsedData["issues"] != null && parsedData["issues"] is JArray issuesArray)
        {
            issues = issuesArray.ToObject<List<string>>();
        }
        else
        {
            Debug.LogWarning("Invalid or missing 'issues' in evaluation data. Defaulting to empty.");
        }

        // Log the received data
        Debug.Log($"Evaluation Data: InterpretationTime={interpretationTime}, ProcessingTime={processingTime}, " +
                $"ErrorCorrectionTime={errorCorrectionTime}, ErrorCount={errorCount}, Completed={completed}, " +
                $"Issues={string.Join(", ", issues)}");

        // Convert the data to a JArray for notification
        JArray evaluationData = new JArray
        {
            new JObject
            {
                ["key"] = "interpretation_time",
                ["value"] = interpretationTime
            },
            new JObject
            {
                ["key"] = "processing_time",
                ["value"] = processingTime
            },
            new JObject
            {
                ["key"] = "error_correction_time",
                ["value"] = errorCorrectionTime
            },
            new JObject
            {
                ["key"] = "error_count",
                ["value"] = errorCount
            },
            new JObject
            {
                ["key"] = "completed",
                ["value"] = completed
            },
            new JObject
            {
                ["key"] = "issues",
                ["value"] = new JArray(issues)
            }
        };

        // Notify observers
        Debug.Log("Try to notidy ScoreController");
        string evaluationDatajson = evaluationData.ToString();
        NotifyObservers("EvaluationData/Workstep", evaluationDatajson);
    }
    catch (Exception ex)
    {
        Debug.LogError($"Error handling evaluation data: {ex.Message}");
    }
}


        private void NotifyUI()
        {
            // Notify UI elements to update accordingly
            Debug.Log("User data updated");
                // Notify the relevant observers about the update
           NotifyObservers("UserDataUpdate/Coins", userCoins);
           NotifyObservers("UserDataUpdate/Score", userScore);
            NotifyObservers("UserDataUpdate/Username", username);
            NotifyObservers("UserDataUpdate/Level", userLevel);
            string badgesJson = badges.ToString();
            NotifyObservers("UserDataUpdate/Badges", badgesJson);
            string backgroundsJson = backgrounds.ToString();
            NotifyObservers("UserDataUpdate/Backgrounds", backgroundsJson);
            string avatarsJson = avatars.ToString();
            NotifyObservers("UserDataUpdate/Avatars", avatarsJson);
            string musicJson = music.ToString();
            NotifyObservers("UserDataUpdate/Music", musicJson);
            string effectsJson = effects.ToString();
            NotifyObservers("UserDataUpdate/Effects", effectsJson);
            NotifyObservers("UserDataUpdate/AutoMode", automodeStatus);
            NotifyObservers("UserDataUpdate/Rights", userRights);
            string scorePresetsJson = scorePresets.ToString();
            NotifyObservers("UserDataUpdate/ScorePresets", scorePresetsJson);
        }
    }

   

}
