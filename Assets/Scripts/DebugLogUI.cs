using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class DebugLogUI : MonoBehaviour
{
    public Text debugText; // Verknüpft mit dem Textfeld im Canvas
    [SerializeField] private int maxLines = 50; // Maximale Anzahl an Zeilen

    private Queue<string> logMessages = new Queue<string>();

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog; // Log-Event abonnieren
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog; // Log-Event abbestellen
    }

private void HandleLog(string logString, string stackTrace, LogType type)
{
    if (type == LogType.Error || type == LogType.Warning || type == LogType.Log)
    {
        logMessages.Enqueue($"{type}: {logString}");
    }

    if (logMessages.Count > maxLines)
    {
        logMessages.Dequeue();
    }

    debugText.text = string.Join("\n", logMessages.ToArray());
}
}
