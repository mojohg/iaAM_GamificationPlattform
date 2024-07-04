using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Rocworks.Mqtt
{
    /// <summary>
    /// The msgHandler class handles MQTT messages and notifies registered observers about events.
    /// Implements the ISubject interface to manage observers.
    /// </summary>
    public class MsgHandler : MonoBehaviour, ISubject
    {
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
            }
            if (!topicObservers[topic].Contains(observer))
            {
                topicObservers[topic].Add(observer);
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
                    observer.OnNotify(topic, data);
                }
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
    }
}
