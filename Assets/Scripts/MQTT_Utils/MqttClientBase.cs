// Copyright (C) 2023 Andreas Vogler (andreas.vogler@rocworks.at) - All Rights Reserved 

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace Rocworks.Mqtt
{
    public interface IMqttClientUnity
    {
        void Connect();
        void Disconnect();

        void SetConnectFlag(bool value);
        void SetConnectState(bool value);

        bool GetConnectState();

        void Publish(string topic, byte[] payload, int qos = 0, bool retain = false);
        void Publish(string topic, string payload, int qos = 0, bool retain = false);
        void Subscribe(string topic, int qos);
        void Unsubscribe(string topic);
    }

    public abstract class MqttClientBase : MonoBehaviour, IMqttClientUnity
    {
        public MqttClient Config;        

        private Queue<Action> _mainThreadQueue = new Queue<Action>();
        private bool _connectFlag = false;

        // Start is called before the first frame update
        protected void Init()
        {
            // Get config component
            if (Config == null) 
                TryGetComponent<MqttClient>(out Config);

            if (Config == null)
            {
                Debug.LogError("Please add " + typeof(MqttClient).Name + " component to object!");
                this.enabled = false;
            }
            else
            {
                // Set UUID for Client Id if it is not set
                if (Config.ClientId == null || Config.ClientId == "")
                    Config.ClientId = Guid.NewGuid().ToString();

                // On Connected Event
                Config.OnConnected.AddListener(() =>
                {
                    SetConnectState(true);
                    if (Config.BirthEnabled)
                    {
                        Publish(Config.BirthTopic, Config.GetBirthMessage(), Config.BirthQualityOfService, Config.BirthRetain);
                    }
                    foreach (string topic in Config.SubscribeTopics)
                    {
                        Subscribe(topic, Config.SubscribeQOS);
                    }
                });

                // On Disconnected Event
                Config.OnDisconnected.AddListener(() =>
                {
                    SetConnectState(false);
                });
            }
        }

        // Update is called once per frame
        void Update()
        {
            // Main thread queue
            lock (_mainThreadQueue)
            {
                while (_mainThreadQueue.Count > 0)
                {
                    _mainThreadQueue.Dequeue().Invoke();
                }
            }

            // Connect/Disconnect
            if (Config.ConnectFlag != _connectFlag)
            {
                _connectFlag = Config.ConnectFlag;
                if (Config.ConnectFlag)
                    Connect();
                else
                    Disconnect();
            }

            // Reconnect
            if (Config.ReconnectInSeconds > 0 && Config.ConnectFlag && !GetConnectState())
            {
                Config.ReconnectTimer += Time.deltaTime;
                if (Config.ReconnectTimer >= Config.ReconnectInSeconds)
                {
                    Config.ReconnectTimer = 0;
                    Connect();
                }
            }
        }

        public void Invoke(Action action)
        {
            lock (_mainThreadQueue)
            {
                _mainThreadQueue.Enqueue(action);
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public abstract void Connect();
        public abstract void Disconnect();

        public void SetConnectFlag(bool value)
        {
            this.Config.ConnectFlag = value;
        }

        public void SetConnectState(bool value)
        {
            this.Config.ConnectState = value;
        }

        public abstract bool GetConnectState();

        public abstract void Subscribe(string topic, int qos);
        public abstract void Unsubscribe(string topic);

        public abstract void Publish(string topic, string payload, int qos = 0, bool retain = false);
        public abstract void Publish(string topic, byte[] payload, int qos = 0, bool retain = false);

    }

    [Serializable]
    public class MqttMessage
    {
        private string _topic;
        private byte[] _bytes = null;
        private string _string = null;
        public MqttMessage(string topic, byte[] data)
        {
            this._topic = topic;
            this._bytes = data;
        }
        public MqttMessage(string topic, string data)
        {
            this._topic = topic;
            this._string = data;
        }
        public string GetTopic()
        {
            return _topic;
        }
        public byte[] GetBytes()
        {
            if (_bytes != null) return _bytes;
            else
            {
                Encoding encoding = Encoding.UTF8;
                return encoding.GetBytes(_string);
            }
        }
        public string GetString()
        {
            if (_string != null) return _string;
            else
            {
                Encoding encoding = Encoding.UTF8;
                return encoding.GetString(_bytes);
            }
        }
    }

    [System.Serializable]
    public class MqttConnectedEvent : UnityEvent { }

    [System.Serializable]
    public class MqttDisconnectedEvent : UnityEvent { }

    [System.Serializable]
    public class MqttMessageEvent : UnityEvent<MqttMessage> { }
}
