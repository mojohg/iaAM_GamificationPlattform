using System.Collections.Generic;
using UnityEngine;

namespace Rocworks.Mqtt
{
    public class MqttClient : MonoBehaviour
    {
        [Header("Configuration")]
        public string ClientId = "";
        public string Host = "";
        public int Port = 1883;
        public string Username = "";
        public string Password = "";
        public bool Websocket = false;
        public string WebsocketPath = "/mqtt";
        public bool UseTLS = false;
        public bool AllowUntrusted = false;
        public bool CleanSession = true;
        public int ConnectTimeoutInSeconds = 3;
        public int ReconnectInSeconds = 5;
        public bool DebugMessages = true;

        [Header("Birth Message")]
        public string BirthTopic = "";
        public string BirthMessage = "Online";
        public int BirthQualityOfService = 0;
        public bool BirthRetain = true;

        [Header("Last Will Message")]
        public string WillTopic = "";
        public string WillMessage = "Offline";
        public int WillQualityOfService = 0;
        public bool WillRetain = true;
        [Tooltip("Send last will message on a graceful disconnect?")]
        public bool WillOnDisconnect = true;

        [Header("State")]
        public bool ConnectFlag = true;
        public bool ConnectState = false;
        public float ReconnectTimer = 0.0f;

        [Header("Subscribe")]
        public int SubscribeQOS = 0;
        public List<string> SubscribeTopics = new();

        [Header("Events")]
        public MqttConnectedEvent OnConnected;
        public MqttDisconnectedEvent OnDisconnected;
        public MqttDisconnectedEvent BeforeDisconnected;
        public MqttMessageEvent OnMessageArrived;
        [Tooltip("Execute event in the UI/Main thread?")]
        public bool OnMessageArrivedInUiThread = true;

        public IMqttClientUnity Connection { get; private set; }

        private byte[] _birthMessage = null;
        private byte[] _willMessage = null; 

        public bool BirthEnabled { get { return (BirthMessage != null || _birthMessage != null) && BirthTopic != null && BirthTopic.Length > 0; } }

        public bool WillEnabled { get { return (WillMessage != null || _willMessage != null) && WillTopic != null && WillTopic.Length > 0; } }

        public byte[] GetBirthMessage()
        {
            if (_birthMessage == null)
                _birthMessage = System.Text.Encoding.UTF8.GetBytes(BirthMessage);
            return _birthMessage;
        }

        public byte[] GetWillMessage()  
        {
            if (_willMessage == null)
                _willMessage = System.Text.Encoding.UTF8.GetBytes(WillMessage);
            return _willMessage;
        }   

        public void SetBirtMessage(byte[] bytes)
        {
            _birthMessage = bytes;
        }

        public void SetWillMessage(byte[] bytes)
        {
            _willMessage = bytes;
        }

        // Start is called before the first frame update
        void Start()
        {
            Connection = GetConnection();
            if (Connection == null)
            {
                Debug.LogError("Please add MqttClientNet or MqttClientWeb component!");
                this.enabled = false;
            }
        }

        // Update is called once per frame
        void Update()
        {

        }

        public IMqttClientUnity GetConnection()
        {
            if (Connection == null)
            {
                if (TryGetComponent<MqttClientNet>(out var net) && net.isNetRuntime())
                    return net;

                if (TryGetComponent<MqttClientWeb>(out var web) && web.isWebRuntime())
                    return web;
            } 
            return Connection;
        }
    }
}