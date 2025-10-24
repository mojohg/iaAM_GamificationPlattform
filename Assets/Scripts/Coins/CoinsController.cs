using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class CoinsController : MonoBehaviour, IObserver
{
    [SerializeField] private GameObject coinPrefab; // Prefab für einzelne Coins
    [SerializeField] private Transform coinTarget; // Zielposition der Coins (z. B. UI-Anzeige)
    [SerializeField] private Text coinText; // Textfeld, das die Anzahl der Coins anzeigt
    [SerializeField] private Transform coinSpawnArea; // Bereich, aus dem die Coins starten (z. B. ein Rechteck)
    private MsgHandler msgHandler;

    private int coinsAwarded = 0;
    private bool awaitingResponse = false;
    private bool resultReceived = false;
    private JObject challengeResultData;

    private void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("UserResultEvent/Challenge", this);
            msgHandler.RegisterObserver("UserDataUpdate/Coins", this);
            msgHandler.RegisterObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/COIN/RESPONSE", this);
        }
        coinText.gameObject.SetActive(false);
        
    }

    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver("UserResultEvent/Challenge", this);
            msgHandler.RemoveObserver("UserDataUpdate/Coins", this);
            msgHandler.RemoveObserver("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/COIN/RESPONSE", this);
        }
    }

public void OnNotify(string eventType, object data)
{
    if (eventType == "UserResultEvent/Challenge")
    {
        HandleChallengeResult(data as string);
    }
    else if (eventType == "UserDataUpdate/Coins")
    {
        HandleCoinsUpdate(data as string);
    }
    else if (eventType == "MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/COIN/RESPONSE")
    {
        HandleCoinResponse(data as string);
    }
}


    private void HandleChallengeResult(string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
        {
            Debug.LogWarning("Challenge result data is null or empty.");
            return;
        }

        challengeResultData = JObject.Parse(jsonString);

        bool success = challengeResultData["result"]?.ToString() == "success";
        if (!success)
        {
            Debug.Log("Challenge failed. No coins will be requested.");
            return;
        }

        resultReceived = true;
        SendCoinRequestToCoreSystem(challengeResultData);
    }

    private void SendCoinRequestToCoreSystem(JObject resultData)
    {
        if (awaitingResponse)
        {
            Debug.LogWarning("Already waiting for coin response. Request ignored.");
            return;
        }

        JObject request = new JObject
        {
            ["level"] = resultData["level"],
            ["difficulty"] = resultData["difficulty_raw"],
            ["mqtt"] = true
        };

        awaitingResponse = true;
        msgHandler.NotifyObservers("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/USERDATA/COIN/REQUEST", request);
        Debug.Log($"Coin request sent: {request}");
    }

    private void HandleCoinsUpdate(string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
        {
            Debug.LogWarning("Coins update data is null or empty.");
            return;
        }

        JObject coinData = JObject.Parse(jsonString);
        coinsAwarded = coinData["coins"]?.ToObject<int>() ?? 0;
        awaitingResponse = false;

        Debug.Log($"Coins awarded: {coinsAwarded}");

        if (resultReceived)
        {
            StartCoroutine(PlayCoinAnimation());
        }
    }


    private void HandleCoinResponse(string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
        {
            Debug.LogWarning("Coin response data is null or empty.");
            return;
        }

        JObject coinData = JObject.Parse(jsonString);

        // Anzahl der Coins aus der Antwort extrahieren
        coinsAwarded = coinData["coins"]?.ToObject<int>() ?? 0;
        awaitingResponse = false;

        Debug.Log($"Received coin response: {coinsAwarded} coins.");

        // Wenn Challenge-Result-Daten bereits vorhanden sind, starte die Animation
        if (resultReceived)
        {
            StartCoroutine(PlayCoinAnimation());
        }
    }

    private IEnumerator PlayCoinAnimation()
    {
        // Text anzeigen
        coinText.text = $"+{coinsAwarded}";
        StartCoroutine(ScaleText());

        // Coin-Regen erzeugen
        for (int i = 0; i < coinsAwarded; i++)
        {
            CreateFlyingCoin();
            yield return new WaitForSeconds(0.05f); // Verzögerung zwischen Coins
        }
    }

    private IEnumerator ScaleText()
    {
        coinText.gameObject.SetActive(true);
        Vector3 initialScale = new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 targetScale = new Vector3(1.5f, 1.5f, 1.5f);

        float elapsedTime = 0f;

        // Vergrößern
        while (elapsedTime < 0.5f)
        {
            elapsedTime += Time.deltaTime;
            coinText.transform.localScale = Vector3.Lerp(initialScale, targetScale, elapsedTime / 0.5f);
            yield return null;
        }

        elapsedTime = 0f;

        // Verkleinern
        while (elapsedTime < 0.5f)
        {
            elapsedTime += Time.deltaTime;
            coinText.transform.localScale = Vector3.Lerp(targetScale, initialScale, elapsedTime / 0.5f);
            yield return null;
        }

        // Ausblenden
        yield return new WaitForSeconds(1f);
        coinText.gameObject.SetActive(false);
    }

    private void CreateFlyingCoin()
    {
        if (coinPrefab == null || coinTarget == null || coinSpawnArea == null)
        {
            Debug.LogWarning("Missing references for coin animation.");
            return;
        }

        // Erstelle eine neue Coin
        GameObject coin = Instantiate(coinPrefab, coinSpawnArea);

        // Zufällige Startposition im Spawnbereich
        RectTransform spawnRect = coinSpawnArea.GetComponent<RectTransform>();
        Vector3 randomPosition = new Vector3(
            Random.Range(spawnRect.rect.xMin, spawnRect.rect.xMax),
            Random.Range(spawnRect.rect.yMin, spawnRect.rect.yMax),
            0
        );

        coin.transform.localPosition = randomPosition;

        // Bewegung zur Zielposition
        StartCoroutine(MoveCoinToTarget(coin));
    }

    private IEnumerator MoveCoinToTarget(GameObject coin)
    {
        Vector3 startPosition = coin.transform.position;
        Vector3 targetPosition = coinTarget.position;

        float duration = 1f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            coin.transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
            yield return null;
        }

        // Coin am Ziel entfernen
        Destroy(coin);
    }
}
