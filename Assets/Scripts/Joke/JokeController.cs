using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;
using System;
using System.Collections;

public class ScrollingJokeController : MonoBehaviour, IObserver
{
    [Header("UI")]
    [SerializeField] private Text jokeText;   // Text-UI für Joke / Message

    [Header("MQTT / MsgHandler")]
    [SerializeField] private MsgHandler msgHandler;

    [Header("Topic-Konfiguration")]
    [Tooltip("Topic-Gruppe, z.B. GAMING (wird über TopicNamespace an den Pfad gehängt).")]
    [SerializeField] private string group = "GAMING";

    [Tooltip("Topic-Key relativ zur Gruppe, z.B. FEATURE/JOKE/DATA oder FEATURE/FACT/DATA.")]
    [SerializeField] private string topicKey = "FEATURE/JOKE/DATA";

    [Header("JSON-Konfiguration")]
    [Tooltip("Name des Feldes im JSON, z.B. 'joke', 'text' oder 'message'.")]
    [SerializeField] private string jsonFieldName = "joke";

    [Header("Scrolling")]
    [SerializeField] private float scrollSpeed = 100f; // Pixel pro Sekunde

    private Coroutine scrollingTextCoroutine;

    private string Topic() => TopicNamespace.TopicFor(group, topicKey);

    // ----------------------------------------------------
    // Lifecycle
    // ----------------------------------------------------
    private void Awake()
    {
        if (!msgHandler)
            msgHandler = FindObjectOfType<MsgHandler>();

        if (!msgHandler)
        {
            Debug.LogError("[ScrollingJokeController] MsgHandler not found in scene!");
        }

        if (!jokeText)
        {
            Debug.LogError("[ScrollingJokeController] jokeText is not assigned in the inspector!");
        }
        else
        {
            jokeText.text = string.Empty;
        }
    }

    private void OnEnable()
    {
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver(Topic(), this);
            Debug.Log($"[ScrollingJokeController] Observer registered for topic '{Topic()}'.");
        }
    }

    private void OnDisable()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver(Topic(), this);
            Debug.Log($"[ScrollingJokeController] Observer removed for topic '{Topic()}'.");
        }
    }

    // ----------------------------------------------------
    // IObserver
    // ----------------------------------------------------
    public void OnNotify(string eventType, object data)
    {
        if (eventType != Topic())
            return;

        if (data is not string jsonString)
        {
            Debug.LogError("[ScrollingJokeController] Received data is not a JSON string!");
            return;
        }

        try
        {
            JObject json = JObject.Parse(jsonString);

            // 1. primär konfigurierbares Feld
            string text = null;
            if (!string.IsNullOrEmpty(jsonFieldName))
                text = json[jsonFieldName]?.ToString();

            // 2. Fallbacks, falls das Feld anders heißt
            if (string.IsNullOrEmpty(text))
                text = json["joke"]?.ToString()
                    ?? json["text"]?.ToString()
                    ?? json["message"]?.ToString();

            if (!string.IsNullOrEmpty(text))
            {
                ShowJoke(text);
            }
            else
            {
                Debug.LogWarning("[ScrollingJokeController] Parsed text is null or empty.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScrollingJokeController] Error parsing JSON: {ex}");
        }
    }

    // ----------------------------------------------------
    // UI / Scrolling
    // ----------------------------------------------------
    private void ShowJoke(string text)
    {
        if (!jokeText)
            return;

        // laufendes Scrolling stoppen
        if (scrollingTextCoroutine != null)
        {
            StopCoroutine(scrollingTextCoroutine);
            scrollingTextCoroutine = null;
        }

        // neuen Scroll-Effekt starten
        scrollingTextCoroutine = StartCoroutine(ScrollJokeText(text));
    }

    private IEnumerator ScrollJokeText(string text)
    {
        RectTransform rt = jokeText.rectTransform;
        if (!rt || rt.parent == null)
            yield break;

        // Text setzen und preferredWidth ermitteln
        jokeText.text = text;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        float textWidth = jokeText.preferredWidth;
        RectTransform containerRt = rt.parent.GetComponent<RectTransform>();
        float containerWidth = containerRt ? containerRt.rect.width : textWidth;

        // Wenn Text kürzer als Container ist: einfach in der Mitte anzeigen, kein Scrolling
        if (textWidth <= containerWidth)
        {
            // Breite wieder normalisieren
            rt.sizeDelta = new Vector2(containerWidth, rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
            yield break;
        }

        // Breite an Text anpassen
        rt.sizeDelta = new Vector2(textWidth, rt.sizeDelta.y);

        // Start- und Endposition (rechts rein, links raus)
        Vector2 startPosition = new Vector2(containerWidth, rt.anchoredPosition.y);
        Vector2 endPosition = new Vector2(-textWidth, rt.anchoredPosition.y);
        rt.anchoredPosition = startPosition;

        while (true)
        {
            rt.anchoredPosition += Vector2.left * scrollSpeed * Time.deltaTime;

            if (rt.anchoredPosition.x <= endPosition.x)
            {
                rt.anchoredPosition = startPosition;
            }

            yield return null;
        }
    }
}
