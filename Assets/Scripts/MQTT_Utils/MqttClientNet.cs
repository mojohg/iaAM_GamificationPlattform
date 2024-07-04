// Copyright (C) 2023 Andreas Vogler (andreas.vogler@rocworks.at) - All Rights Reserved 

using System;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Rocworks.Mqtt
{
    public class MqttClientNet : MqttClientBase, IMqttClientUnity
    {        
        private MqttFactory _mqttFactory;
        private IMqttClient _mqttClient;

        private void Awake()
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            Debug.LogWarning("MqttClientNet got disabled, because it is a WebGL build!");
            this.enabled = false;
#endif
        }

        public bool isNetRuntime()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            return true;
#endif
        }

        // Start is called before the first frame update
        void Start()
        {
            Init();

            _mqttFactory = new MqttFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();
            _mqttClient.ConnectedAsync += OnConnectedCB;
            _mqttClient.DisconnectedAsync += OnDisconnectedCB;
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedCB;
        }
       
        public override void Connect()
        {
            if (Config.DebugMessages) Debug.Log("Connect");
            var options = new MqttClientOptionsBuilder();
            options.WithClientId(Config.ClientId);
            options.WithTimeout(TimeSpan.FromSeconds(Config.ConnectTimeoutInSeconds));
            options.WithCleanSession(Config.CleanSession);

            if (!Config.Websocket)
            {
                options.WithTcpServer(Config.Host, Config.Port);
            }
            else
            {
                var uri = Config.Host + ":" + Config.Port.ToString() + Config.WebsocketPath;
                options.WithWebSocketServer(o =>
                {
                    o.WithUri(uri);
                });
            }

            if (Config.Username != "")
            {
                options.WithCredentials(Config.Username, Config.Password);
            }

            if (Config.UseTLS)
            {
                var tls = new MqttClientTlsOptionsBuilder();
                tls.WithAllowUntrustedCertificates(Config.AllowUntrusted);
                options.WithTlsOptions(tls.Build());
            }

            if (Config.WillEnabled)
            {
                options.WithWillTopic(Config.WillTopic);
                options.WithWillPayload(Config.GetWillMessage());
                options.WithWillQualityOfServiceLevel(GetQoS(Config.WillQualityOfService));
                options.WithWillRetain(Config.WillRetain);
            }

            _mqttClient.ConnectAsync(options.Build(), CancellationToken.None);
        }

        public override void Disconnect()
        {
            if (Config.DebugMessages) Debug.Log("Disconnect");
            if (GetConnectState()) Config.BeforeDisconnected.Invoke();
            var options = new MqttClientDisconnectOptionsBuilder();
            options.WithReason(Config.WillEnabled && Config.WillOnDisconnect ? MqttClientDisconnectOptionsReason.DisconnectWithWillMessage : MqttClientDisconnectOptionsReason.NormalDisconnection);
            _mqttClient.DisconnectAsync(options.Build());
        }

        private static MqttQualityOfServiceLevel GetQoS(int qos)
        {
            return qos == 1 ? MqttQualityOfServiceLevel.AtLeastOnce :
                            qos == 2 ? MqttQualityOfServiceLevel.ExactlyOnce :
                            MqttQualityOfServiceLevel.AtMostOnce;
        }

        public override void Subscribe(string topic, int qos)
        {
            var options = _mqttFactory.CreateSubscribeOptionsBuilder();
            options.WithTopicFilter(topic, GetQoS(qos));
            _mqttClient.SubscribeAsync(options.Build(), CancellationToken.None);
        }

        public override void Unsubscribe(string topic)
        {
            _mqttClient.UnsubscribeAsync(topic, CancellationToken.None);
        }

        public override void Publish(string topic, string payload, int qos = 0, bool retain = false)
        {
            _mqttClient.PublishStringAsync(topic, payload, GetQoS(qos), retain);
        }

        public override void Publish(string topic, byte[] payload, int qos = 0, bool retain = false)
        {
            _mqttClient.PublishBinaryAsync(topic, payload, GetQoS(qos), retain);
        }

        private Task OnConnectedCB(MqttClientConnectedEventArgs args)
        {
            Invoke(() =>
            {
                if (Config.DebugMessages) Debug.Log("OnConnected: " + args.ConnectResult.ResultCode);
                Config.OnConnected.Invoke();
            });
            return Task.CompletedTask;
        }

        private Task OnDisconnectedCB(MqttClientDisconnectedEventArgs args)
        {
            Invoke(() =>
            {
                if (Config.DebugMessages) Debug.Log("OnDisconnected");
                Config.OnDisconnected.Invoke();
            });
            return Task.CompletedTask;
        }

        private Task OnMessageReceivedCB(MqttApplicationMessageReceivedEventArgs args)
        {
            if (Config.DebugMessages) Debug.Log("OnMessageReceived: " + args.ApplicationMessage.ConvertPayloadToString());
            void execute()
            {
                var bytes = args.ApplicationMessage.PayloadSegment.Count>0 ? args.ApplicationMessage.PayloadSegment.ToArray() : new byte[0];
                var msg = new MqttMessage(args.ApplicationMessage.Topic, bytes);
                Config.OnMessageArrived.Invoke(msg);
            }
            if (Config.OnMessageArrivedInUiThread) Invoke(execute);
            else execute();
            return Task.CompletedTask;
        }

        public override bool GetConnectState()
        {
            return _mqttClient != null && _mqttClient.IsConnected;
        }
    }
}