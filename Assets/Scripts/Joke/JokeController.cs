using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;
using System;
using System.Collections;

public class ScrollingJokeController : MonoBehaviour, IObserver
{
   [SerializeField] private Text jokeText; // Text-UI für den Witz
    private MsgHandler msgHandler;

    private Coroutine scrollingTextCoroutine;

    private void Start()
    {
        // Hole den MsgHandler und registriere Observer
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/FEATURE/JOKE/DATA", this);
            Debug.Log("Observer registered for JOKE/DATA topic.");
        }
        else
        {
            Debug.LogError("MsgHandler not found in the scene!");
        }

        // Textfeld zunächst leeren
        if (jokeText != null)
        {
            jokeText.text = "";
            Debug.Log("Initialized jokeText.");
        }
        else
        {
            Debug.LogError("jokeText is not assigned in the inspector!");
        }
    }

    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/FEATURE/JOKE/DATA", this);
            Debug.Log("Observer removed for JOKE/DATA topic.");
        }
    }

    public void OnNotify(string eventType, object data)
    {
        Debug.Log($"OnNotify called with eventType: {eventType}");

        if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/FEATURE/JOKE/DATA")
        {
            if (data is string jsonString)
            {
                Debug.Log($"Received JSON string: {jsonString}");

                try
                {
                    JObject json = JObject.Parse(jsonString);
                    string joke = json["joke"]?.ToString();

                    if (!string.IsNullOrEmpty(joke))
                    {
                        Debug.Log($"Parsed joke: {joke}");
                        ShowJoke(joke);
                    }
                    else
                    {
                        Debug.LogWarning("Parsed joke is null or empty.");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error parsing joke data: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError("Received data is not a valid JSON string!");
            }
        }
    }

    private void ShowJoke(string joke)
    {
        Debug.Log("ShowJoke called.");

        // Stoppe den aktuellen Scroll-Effekt, falls einer läuft
        if (scrollingTextCoroutine != null)
        {
            StopCoroutine(scrollingTextCoroutine);
        }

        // Starte einen neuen Scroll-Effekt
        Debug.Log("Starting new scrolling coroutine.");
        scrollingTextCoroutine = StartCoroutine(ScrollJokeText(joke));
    }

    private IEnumerator ScrollJokeText(string joke)
    {
        // Text-Scrolling vorbereiten
        RectTransform rt = jokeText.rectTransform;
        float scrollSpeed = 100f; // Pixels per second
        float textWidth = jokeText.preferredWidth; // Vollständige Textbreite
        float containerWidth = rt.parent.GetComponent<RectTransform>().rect.width;

        // Text in das Textfeld setzen
        jokeText.text = joke;

        // Passe die Breite des RectTransforms an den Text an
        rt.sizeDelta = new Vector2(textWidth, rt.sizeDelta.y);

        // Setze den Text zunächst außerhalb des sichtbaren Bereichs
        Vector2 startPosition = new Vector2(containerWidth, rt.anchoredPosition.y);
        Vector2 endPosition = new Vector2(-textWidth, rt.anchoredPosition.y);

        rt.anchoredPosition = startPosition;

        while (true)
        {
            rt.anchoredPosition += Vector2.left * scrollSpeed * Time.deltaTime;

            if (rt.anchoredPosition.x <= endPosition.x)
            {
                rt.anchoredPosition = startPosition; // Zurück zur Startposition
            }

            yield return null;
        }
    }
}