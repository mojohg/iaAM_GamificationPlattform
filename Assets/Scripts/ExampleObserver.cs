using UnityEngine;
using Rocworks.Mqtt;
public class ExampleObserver : MonoBehaviour, IObserver
{
    private void Start()
    {
        var msgHandler = FindObjectOfType<MsgHandler>();
        msgHandler.RegisterObserver("iaAM/ManualWorkStation/v1/Test", this);
    }

    private void OnDestroy()
    {
        var msgHandler = FindObjectOfType<MsgHandler>();
        msgHandler.RemoveObserver("iaAM/ManualWorkStation/v1/Test", this);
    }

    public void OnNotify(string eventType, object data)
    {
        if (eventType == "iaAM/ManualWorkStation/v1/Test")
        {
            Debug.Log("ObserverReached");
            Debug.Log($"Received data: {data}");
        }
    }
}
