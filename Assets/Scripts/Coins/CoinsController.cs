using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class CoinsController : MonoBehaviour, IObserver
{
    [Header("Coin Prefabs & Targets")]
    [SerializeField] private GameObject coinPrefab;

    [Tooltip("Zielposition der Coins – dort sitzt deine Userdaten-/Coin-Anzeige.")]
    [SerializeField] private Transform coinTarget;

    [Tooltip("Spawnbereich, aus dem Coins bei Gutschrift kommen (z. B. Challenge-Bereich).")]
    [SerializeField] private Transform creditSpawnArea;

    [Tooltip("Spawnbereich/Ziel, wohin Coins bei Abbuchung fliegen (z. B. Shop-Bereich).")]
    [SerializeField] private Transform debitTargetArea;

    [Header("UI - Delta Text (+10 / -5)")]
    [SerializeField] private Text coinDeltaText;

    [Header("MQTT / MsgHandler")]
    [SerializeField] private MsgHandler msgHandler;

    [Header("Topic-Konfiguration")]
    [Tooltip("MQTT-Gruppe, z.B. GAMING")]
    [SerializeField] private string group = "GAMING";

    [Tooltip("Topic-Key relativ zur Gruppe, z.B. USERDATA/COIN/EVENT")]
    [SerializeField] private string category = "USERDATA/COIN/EVENT";

    [Header("Animation Settings")]
    [SerializeField] private float coinTravelDuration = 1.0f;
    [SerializeField] private float coinSpawnDelay = 0.05f;
    [SerializeField] private Vector3 textInitialScale = new Vector3(0.5f, 0.5f, 0.5f);
    [SerializeField] private Vector3 textTargetScale = new Vector3(1.5f, 1.5f, 1.5f);
    [SerializeField] private float textScaleDuration = 0.5f;
    [SerializeField] private float textStayDuration = 1.0f;

    private string CoinEventTopic => TopicNamespace.TopicFor(group, category);

    private void Awake()
    {
        if (!msgHandler)
            msgHandler = FindObjectOfType<MsgHandler>();

        if (!coinDeltaText)
            Debug.LogWarning("[CoinsController] coinDeltaText is not assigned.");

        if (coinDeltaText)
            coinDeltaText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver(CoinEventTopic, this);
            Debug.Log($"[CoinsController] Listening to {CoinEventTopic}");
        }
    }

    private void OnDisable()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver(CoinEventTopic, this);
        }
    }

    // ---------------------------------------------------------
    // IObserver
    // ---------------------------------------------------------
    public void OnNotify(string eventType, object data)
    {
        if (eventType != CoinEventTopic)
            return;

        if (data is not string jsonString)
        {
            Debug.LogError($"[CoinsController] Expected JSON string, got {data?.GetType()}");
            return;
        }

        HandleCoinEvent(jsonString);
    }

    // ---------------------------------------------------------
    // MQTT Event Handling
    // ---------------------------------------------------------
    private void HandleCoinEvent(string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
        {
            Debug.LogWarning("[CoinsController] Empty coin event payload.");
            return;
        }

        JObject root = JObject.Parse(jsonString);
        JToken payload = root["data"] ?? root;

        // Wir akzeptieren "delta" oder "amount"
        int delta = payload["delta"]?.ToObject<int>() 
                    ?? payload["amount"]?.ToObject<int>() ?? 0;

        if (delta == 0)
        {
            Debug.Log("[CoinsController] Coin delta is 0 – no animation.");
            return;
        }

        bool isCredit = delta > 0;
        int amount = Mathf.Abs(delta);

        StartCoroutine(PlayCoinAnimation(amount, isCredit));
    }

    // ---------------------------------------------------------
    // Animation
    // ---------------------------------------------------------
    private IEnumerator PlayCoinAnimation(int amount, bool isCredit)
    {
        if (!coinDeltaText)
            yield break;

        // Text vorbereiten
        coinDeltaText.gameObject.SetActive(true);
        coinDeltaText.text = (isCredit ? "+" : "-") + amount;

        // Optional: Farbe für Credit/Debit
        coinDeltaText.color = isCredit ? Color.green : Color.red;

        // Text-Skalierung
        StartCoroutine(ScaleTextRoutine());

        // Coin-Flug
        for (int i = 0; i < amount; i++)
        {
            CreateFlyingCoin(isCredit);
            yield return new WaitForSeconds(coinSpawnDelay);
        }
    }

    private IEnumerator ScaleTextRoutine()
    {
        coinDeltaText.transform.localScale = textInitialScale;

        float elapsed = 0f;

        // Scale-Up
        while (elapsed < textScaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / textScaleDuration);
            coinDeltaText.transform.localScale = Vector3.Lerp(textInitialScale, textTargetScale, t);
            yield return null;
        }

        elapsed = 0f;

        // Scale-Down
        while (elapsed < textScaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / textScaleDuration);
            coinDeltaText.transform.localScale = Vector3.Lerp(textTargetScale, textInitialScale, t);
            yield return null;
        }

        // kurz stehen lassen
        yield return new WaitForSeconds(textStayDuration);

        coinDeltaText.gameObject.SetActive(false);
    }

    private void CreateFlyingCoin(bool isCredit)
    {
        if (!coinPrefab || !coinTarget || !creditSpawnArea || !debitTargetArea)
        {
            Debug.LogWarning("[CoinsController] Missing references for coin animation.");
            return;
        }

        Vector3 startPos;
        Vector3 targetPos;

        if (isCredit)
        {
            // Gutschrift: Coin von creditSpawnArea -> coinTarget (Richtung Userdaten)
            startPos = GetRandomPointInRect(creditSpawnArea);
            targetPos = coinTarget.position;
        }
        else
        {
            // Abbuchung: Coin von coinTarget -> debitTargetArea (Richtung Shop / Ausgabe)
            startPos = coinTarget.position;
            targetPos = GetRandomPointInRect(debitTargetArea);
        }

        GameObject coin = Instantiate(coinPrefab, startPos, Quaternion.identity, transform);
        StartCoroutine(MoveCoinToTarget(coin, targetPos));
    }

    private Vector3 GetRandomPointInRect(Transform area)
    {
        RectTransform rt = area.GetComponent<RectTransform>();
        if (!rt)
            return area.position;

        var rect = rt.rect;
        Vector3 local = new Vector3(
            Random.Range(rect.xMin, rect.xMax),
            Random.Range(rect.yMin, rect.yMax),
            0f
        );

        return area.TransformPoint(local);
    }

    private IEnumerator MoveCoinToTarget(GameObject coin, Vector3 targetPos)
    {
        if (!coin) yield break;

        Vector3 startPos = coin.transform.position;
        float elapsed = 0f;

        while (elapsed < coinTravelDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / coinTravelDuration);
            coin.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        Destroy(coin);
    }
}
