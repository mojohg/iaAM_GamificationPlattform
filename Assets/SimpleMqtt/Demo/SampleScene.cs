using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Rocworks.Mqtt
{
    public class SampleScene : MonoBehaviour
    {
        public MqttClient MqttClient;
        public GameObject ObjectToHandle;
        public Toggle IsConnected;

        private float _positionX = 0;
        private float _positionY = 0;
        private float _positionZ = 0;

        private float _rotationX = 0;
        private float _rotationY = 0;
        private float _rotationZ = 0;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            this.ObjectToHandle.transform.position = new Vector3(_positionX, _positionY, _positionZ);
            this.ObjectToHandle.transform.eulerAngles = new Vector3(_rotationX, _rotationY, _rotationZ);
            this.IsConnected.isOn = this.MqttClient.Connection.GetConnectState();
        }

        public void OnToggleButtonChanged(bool value)
        {
            Debug.Log("OnToggleButtonChanged");
            this.MqttClient.Connection.SetConnectFlag(value);
        }

        public void OnSliderPositionXChanged(float value)
        {
            MqttClient.Connection.Publish("Rocworks/Cube/Position/X", value.ToString(CultureInfo.InvariantCulture));
        }

        public void OnSliderPositionYChanged(float value)
        {
            MqttClient.Connection.Publish("Rocworks/Cube/Position/Y", value.ToString(CultureInfo.InvariantCulture));
        }

        public void OnSliderPositionZChanged(float value)
        {
            MqttClient.Connection.Publish("Rocworks/Cube/Position/Z", value.ToString(CultureInfo.InvariantCulture));
        }

        public void OnSliderRotationXChanged(float value)
        {
            MqttClient.Connection.Publish("Rocworks/Cube/Rotation/X", value.ToString(CultureInfo.InvariantCulture));
        }

        public void OnSliderRotationYChanged(float value)
        {
            MqttClient.Connection.Publish("Rocworks/Cube/Rotation/Y", value.ToString(CultureInfo.InvariantCulture));
        }

        public void OnSliderRotationZChanged(float value)
        {
            MqttClient.Connection.Publish("Rocworks/Cube/Rotation/Z", value.ToString(CultureInfo.InvariantCulture));
        }

        public void SetDebug(bool value)
        {
            MqttClient.DebugMessages = value;
        }

        public void OnMessageArrived(MqttMessage m)
        {
            switch (m.GetTopic()) {
                case "Rocworks/Cube/Position/X":
                    _positionX = float.Parse(m.GetString(), CultureInfo.InvariantCulture);
                    break;
                case "Rocworks/Cube/Position/Y":
                    _positionY = float.Parse(m.GetString(), CultureInfo.InvariantCulture);
                    break;
                case "Rocworks/Cube/Position/Z":
                    _positionZ = float.Parse(m.GetString(), CultureInfo.InvariantCulture);
                    break;
                case "Rocworks/Cube/Rotation/X":
                    _rotationX = float.Parse(m.GetString(), CultureInfo.InvariantCulture);
                    break;
                case "Rocworks/Cube/Rotation/Y":
                    _rotationY = float.Parse(m.GetString(), CultureInfo.InvariantCulture);
                    break;
                case "Rocworks/Cube/Rotation/Z":
                    _rotationZ = float.Parse(m.GetString(), CultureInfo.InvariantCulture);
                    break;             
            }
        }    
    }
}



