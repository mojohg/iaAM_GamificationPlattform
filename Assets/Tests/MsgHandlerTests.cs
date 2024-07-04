using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using Rocworks.Mqtt;

public class MockObserver : IObserver
{
    public string LastEventType { get; private set; }
    public object LastData { get; private set; }

    public void OnNotify(string eventType, object data)
    {
        LastEventType = eventType;
        LastData = data;
    }
}

[TestFixture]
public class MsgHandlerTests
{
    private MsgHandler msgHandler;
    private MockObserver mockObserver;
    private MqttClient mockMqttClient;

    [SetUp]
    public void SetUp()
    {
        var gameObject = new GameObject();
        mockMqttClient = gameObject.AddComponent<MqttClient>();
        msgHandler = gameObject.AddComponent<MsgHandler>();
        msgHandler.MqttClient = mockMqttClient;
        mockObserver = new MockObserver();
        msgHandler.RegisterObserver("test/topic", mockObserver);

        mockMqttClient.OnConnected = new MqttConnectedEvent();
        mockMqttClient.OnDisconnected = new MqttDisconnectedEvent();
        mockMqttClient.OnMessageArrived = new MqttMessageEvent();
    }

    [TearDown]
    public void TearDown()
    {
        GameObject.DestroyImmediate(msgHandler.gameObject);
    }

    [Test]
    public void RegisterObserver_ObserverGetsRegistered()
    {
        var anotherObserver = new MockObserver();
        msgHandler.RegisterObserver("test/topic", anotherObserver);
        msgHandler.NotifyObservers("test/topic", "testData");

        Assert.AreEqual("test/topic", anotherObserver.LastEventType);
        Assert.AreEqual("testData", anotherObserver.LastData);
    }

    [Test]
    public void RemoveObserver_ObserverGetsRemoved()
    {
        msgHandler.RemoveObserver("test/topic", mockObserver);
        msgHandler.NotifyObservers("test/topic", "testData");

        Assert.IsNull(mockObserver.LastEventType);
        Assert.IsNull(mockObserver.LastData);
    }

    [Test]
    public void OnMessageArrived_HandlesNullObserver()
    {
        msgHandler.RegisterObserver("null/topic", null);
        var message = new MqttMessage("null/topic", "test payload");

        // This should not throw an exception
        Assert.DoesNotThrow(() => mockMqttClient.OnMessageArrived.Invoke(message));
    }
}
