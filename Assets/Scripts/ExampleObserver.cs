using UnityEngine;
using Rocworks.Mqtt;
public class ExampleObserver : MonoBehaviour, IObserver
{
    private MsgHandler msgHandler;

    private void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("iaAM/ManualWorkStation:1/Gamification/Progressbar/ProgressComplete", this);
            Debug.Log("ExampleObserver registered for ProgressComplete");
        }
    }

    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver("iaAM/ManualWorkStation:1/Gamification/Progressbar/ProgressComplete", this);
        }
    }

    public void OnNotify(string eventType, object data)
    {
        if (eventType == "iaAM/ManualWorkStation:1/Gamification/Progressbar/ProgressComplete")
        {
            Debug.Log("ObserverReached_BY_Progressbar");
            Debug.Log($"Received data: {data}");
        }
    }
}
