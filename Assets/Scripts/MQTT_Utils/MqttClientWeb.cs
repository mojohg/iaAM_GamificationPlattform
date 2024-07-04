using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AOT;
using UnityEngine;

namespace Rocworks.Mqtt
{
    public class MqttClientWeb : MqttClientBase, IMqttClientUnity
    {
        private int _instanceId;

        private void Awake()
        {
#if UNITY_EDITOR || !UNITY_WEBGL
            Debug.LogWarning("MqttClientWeb got disabled, because it is not a WebGL build!");
            this.enabled = false;
#endif
        }

        public bool isWebRuntime()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        // Start is called before the first frame update
        void Start()
        {
            Init();
        }

        public override void Connect()
        {
#if UNITY_WEBGL
            if (this.enabled)
            {
                string url = (Config.UseTLS ? "wss" : "ws") + "://" + Config.Host + ":" + Config.Port + Config.WebsocketPath;
                _instanceId = MqttJsNative.CreateInstance(
                    Config.ClientId, 
                    url, 
                    Config.UseTLS, 
                    Config.Username, 
                    Config.Password, 
                    Config.CleanSession,
                    Config.DebugMessages, 
                    this
                );
                if (_instanceId > 0)
                {
                    if (Config.WillEnabled)
                    {
                        MqttJsNative.MqttSetLastWillMessage(
                            _instanceId, 
                            Config.WillTopic,
                            Encoding.UTF8.GetString(Config.GetWillMessage()), // TODO: Paho Java Script Library does not work with a ByteArray as Last Will Message 
                            Config.WillQualityOfService, 
                            Config.WillRetain
                        );
                    }
                    MqttJsNative.MqttConnect(_instanceId);
                }
            }
#endif
        }

        public override void Disconnect()
        {
#if UNITY_WEBGL
            if (this.enabled)
            {
                if (Config.WillEnabled && Config.WillOnDisconnect)
                {
                    MqttJsNative.MqttSendLastWillMessage(_instanceId);
                }
                if (GetConnectState()) Config.BeforeDisconnected.Invoke();
                MqttJsNative.MqttDisconnect(_instanceId);
            }
#endif
        }

        public override bool GetConnectState()
        {
            return Config.ConnectState;
        }

        public override void Publish(string topic, byte[] payload, int qos = 0, bool retain = false)
        {
#if UNITY_WEBGL
            MqttJsNative.MqttPublishBuffer(_instanceId, topic, payload, payload.Length, qos, retain);
#endif
        }

        public override void Publish(string topic, string payload, int qos = 0, bool retain = false)
        {
#if UNITY_WEBGL
            MqttJsNative.MqttPublishString(_instanceId, topic, payload, qos, retain);
#endif
        }

        public override void Subscribe(string topic, int qos)
        {
#if UNITY_WEBGL
            MqttJsNative.MqttSubscribe(_instanceId, topic, qos);
#endif
        }

        public override void Unsubscribe(string topic)
        {
#if UNITY_WEBGL
            MqttJsNative.MqttUnsubscribe(_instanceId, topic);
#endif
        }
    }

#if UNITY_WEBGL
    public static class MqttJsNative
    {
        /* If callbacks are initialized and set */
        private static bool isInitialized = false;

        private static readonly Dictionary<Int32, MqttClientWeb> _instances = new Dictionary<Int32, MqttClientWeb>();

        private static void Initialize()
        {
            MqttLoadScript();
            MqttSetOnOpen(DelegateOnOpenEvent);
            MqttSetOnMessage(DelegateOnMessageEvent);
            MqttSetOnClose(DelegateOnCloseEvent);
            isInitialized = true;
        }

        public static int CreateInstance(
            string clientId, 
            string url, 
            bool tls, 
            string username, 
            string password, 
            bool cleanSession,
            bool debug, 
            MqttClientWeb ws
        )
        {
            if (!isInitialized)
                Initialize();

            int instanceId = MqttCreate(
                clientId, 
                url, 
                tls, 
                username, 
                password, 
                cleanSession,
                debug
            );
            _instances.Add(instanceId, ws);
            return instanceId;
        }

        public static void DestroyInstance(int instanceId)
        {
            _instances.Remove(instanceId);
            MqttRelease(instanceId);
        }

        /* Delegates */
        public delegate void OnOpenCallback(int instanceId);
        public delegate void OnMessageCallback(int instanceId, IntPtr topicPtr, IntPtr msgPtr, int msgSize);
        public delegate void OnCloseCallback(int instanceId, int closeCode);

        /* JavaScript Functions */
        [DllImport("__Internal")]
        public static extern void MqttLoadScript();

        [DllImport("__Internal")]
        public static extern int MqttCreate(
            string clientId, 
            string url, 
            bool tls, 
            string username, 
            string password, 
            bool cleanSession,
            bool debug
        );

        [DllImport("__Internal")]
        public static extern void MqttSetLastWillMessage(int instanceId, string topic, string message, int qos, bool retained);

        [DllImport("__Internal")]
        public static extern void MqttSendLastWillMessage(int instanceId);

        [DllImport("__Internal")]
        public static extern void MqttRelease(int instanceId);

        [DllImport("__Internal")]
        public static extern int MqttConnect(int instanceId);

        [DllImport("__Internal")]
        public static extern int MqttDisconnect(int instanceId);

        [DllImport("__Internal")]
        public static extern int MqttSubscribe(int instanceId, string topic, int qos);

        [DllImport("__Internal")]
        public static extern int MqttUnsubscribe(int instanceId, string topic);

        [DllImport("__Internal")]
        public static extern int MqttPublishBuffer(int instanceId, string topic, byte[] dataPtr, int dataLength, int qos, bool retain);

        [DllImport("__Internal")]
        public static extern int MqttPublishString(int instanceId, string topic, string data, int qos, bool retain);

        // Callback Functions

        [MonoPInvokeCallback(typeof(OnOpenCallback))]
        public static void DelegateOnOpenEvent(int instanceId)
        {
            if (_instances.TryGetValue(instanceId, out MqttClientWeb instanceRef))
            {
                instanceRef.Config.OnConnected.Invoke();
            }
        }

        [MonoPInvokeCallback(typeof(OnCloseCallback))]
        public static void DelegateOnCloseEvent(int instanceId, int closeCode)
        {
            if (_instances.TryGetValue(instanceId, out MqttClientWeb instanceRef))
            {
                instanceRef.Config.OnDisconnected.Invoke();
            }
        }

        [MonoPInvokeCallback(typeof(OnMessageCallback))]
        public static void DelegateOnMessageEvent(int instanceId,IntPtr topicPtr, IntPtr msgPtr, int msgSize)
        {
            if (_instances.TryGetValue(instanceId, out MqttClientWeb instanceRef))
            {
                string topic = Marshal.PtrToStringAuto(topicPtr);

                //string data = Marshal.PtrToStringAuto(msgPtr);

                byte[] byteArray = new byte[msgSize];
                Marshal.Copy(msgPtr, byteArray, 0, msgSize);

                var msg = new MqttMessage(topic, byteArray);
                instanceRef.Config.OnMessageArrived.Invoke(msg);
            }
        }

        // Setter for Callback Functions

        [DllImport("__Internal")]
        public static extern void MqttSetOnOpen(OnOpenCallback callback);

        [DllImport("__Internal")]
        public static extern void MqttSetOnMessage(OnMessageCallback callback);

        [DllImport("__Internal")]
        public static extern void MqttSetOnClose(OnCloseCallback callback);
    }
#endif
}
