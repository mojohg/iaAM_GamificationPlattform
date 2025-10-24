using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using Rocworks.Mqtt;

[RequireComponent(typeof(RectTransform))]
public class BackgroundController : MonoBehaviour, IObserver
{
    [Header("UI")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Text costText;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private GameObject shadeOverlay;
    [SerializeField] private GameObject purchaseButton;
    [SerializeField] private Text requiredScoreText;
    [SerializeField] private Text requiredLevelText;

    [Header("MQTT / MsgHandler")]
    [Tooltip("Wird automatisch gefunden, wenn leer.")]
    public MsgHandler msgHandler;

    [Header("Topic Namespace (flexibel)")]
    [Tooltip("Hauptgruppe für Game-Themen, z. B. 'GAMING'.")]
    public string groupGame = "GAMING";
    [Tooltip("Entity-Key für Hintergründe, z. B. 'BACKGROUND'.")]
    public string keyBackground = "BACKGROUND";
    [Tooltip("Userdaten-Gruppe, z. B. 'USERDATA'.")]
    public string groupUserData = "USERDATA";
    [Tooltip("Coins-Key innerhalb USERDATA.")]
    public string keyCoins = "COINS";
    [Tooltip("Backgrounds-Key innerhalb USERDATA.")]
    public string keyUserBackgrounds = "BACKGROUNDS";

    [Header("Local/Legacy Fallback (optional)")]
    [Tooltip("Zusätzlich alte, lokale EventKeys (UserDataUpdate/...) beobachten?")]
    public bool alsoSubscribeLegacyLocal = false;

    [Header("Assets")]
    [Tooltip("Relativer Ordner unter StreamingAssets.")]
    public string backgroundsFolder = "Backgrounds/v1_0";
    [Tooltip("Fallback-Sprite, falls Datei fehlt.")]
    public Sprite defaultSprite;

    // ---- runtime state ----
    private readonly List<BackgroundInfo> _backgrounds = new();
    private int _currentIndex = 0;
    private float _userCoins;
    private Coroutine _publishCoroutine;

    // ---- constants for subkeys ----
    private const string SUB_PURCHASE = "PURCHASE";
    private const string SUB_RESPONSE = "RESPONSE";
    private const string SUB_ACTIVE   = "ACTIVE";

    // --------------- Unity lifecycle ---------------
    void Awake()
    {
        if (!msgHandler) msgHandler = FindObjectOfType<MsgHandler>();
        WireButtons();
    }

    void Start()
    {
        RegisterObservers();
        LoadBackgroundsFromDisk();
        RefreshUI();
    }

    void OnDestroy()
    {
        UnregisterObservers();
    }

    // --------------- Button wiring ---------------
    void WireButtons()
    {
        if (nextButton)     nextButton.onClick.AddListener(NextBackground);
        if (previousButton) previousButton.onClick.AddListener(PreviousBackground);
        if (purchaseButton)
        {
            var btn = purchaseButton.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(PurchaseCurrentBackground);
        }
    }

    // --------------- Topic building ---------------
    string TopicPurchase()         => TopicNamespace.TopicFor(groupGame, keyBackground + "/" + SUB_PURCHASE);
    string TopicPurchaseResponse() => TopicNamespace.TopicFor(groupGame, keyBackground + "/" + SUB_RESPONSE);
    string TopicActive()           => TopicNamespace.TopicFor(groupGame, keyBackground + "/" + SUB_ACTIVE);
    string TopicUserBackgrounds()  => TopicNamespace.TopicFor(groupUserData, keyUserBackgrounds);
    string TopicUserCoins()        => TopicNamespace.TopicFor(groupUserData, keyCoins);

    // (Optional) Legacy local keys
    const string LEGACY_LOCAL_BG   = "UserDataUpdate/Backgrounds";
    const string LEGACY_LOCAL_COINS= "UserDataUpdate/Coins";

    void RegisterObservers()
    {
        if (msgHandler == null) return;

        // new flexible topics
        msgHandler.RegisterObserver(TopicUserCoins(), this);
        msgHandler.RegisterObserver(TopicPurchaseResponse(), this);
        msgHandler.RegisterObserver(TopicUserBackgrounds(), this);

        // optional legacy local
        if (alsoSubscribeLegacyLocal)
        {
            msgHandler.RegisterObserver(LEGACY_LOCAL_COINS, this);
            msgHandler.RegisterObserver(LEGACY_LOCAL_BG, this);
        }
    }

    void UnregisterObservers()
    {
        if (msgHandler == null) return;

        msgHandler.RemoveObserver(TopicUserCoins(), this);
        msgHandler.RemoveObserver(TopicPurchaseResponse(), this);
        msgHandler.RemoveObserver(TopicUserBackgrounds(), this);

        if (alsoSubscribeLegacyLocal)
        {
            msgHandler.RemoveObserver(LEGACY_LOCAL_COINS, this);
            msgHandler.RemoveObserver(LEGACY_LOCAL_BG, this);
        }
    }

    // --------------- IObserver ---------------
    public void OnNotify(string eventType, object data)
    {
        try
        {
            if (string.IsNullOrEmpty(eventType)) return;

            // Coins
            if (eventType == TopicUserCoins() || (alsoSubscribeLegacyLocal && eventType == LEGACY_LOCAL_COINS))
            {
                if (float.TryParse(data?.ToString(), out var coinFloat))
                {
                    _userCoins = coinFloat;
                    RefreshUI();
                }
                else
                {
                    Debug.LogWarning($"[BackgroundController] Could not parse coins from: {data}");
                }
                return;
            }

            // Backgrounds (status + active)
            if (eventType == TopicUserBackgrounds() || (alsoSubscribeLegacyLocal && eventType == LEGACY_LOCAL_BG))
            {
                var json = data as string;
                if (!string.IsNullOrEmpty(json))
                {
                    ApplyBackgroundStatusFromJson(json, mayContainActiveFlag:true);
                    RefreshUI();
                }
                return;
            }

            // Purchase response
            if (eventType == TopicPurchaseResponse())
            {
                var json = data as string;
                if (!string.IsNullOrEmpty(json))
                {
                    HandlePurchaseResponse(json);
                }
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BackgroundController] OnNotify error for '{eventType}': {ex}");
        }
    }

    // --------------- Disk load ---------------
    void LoadBackgroundsFromDisk()
    {
        _backgrounds.Clear();

        string path = Path.Combine(Application.streamingAssetsPath, backgroundsFolder);
        if (!Directory.Exists(path))
        {
            Debug.LogError($"[BackgroundController] Backgrounds directory not found: {path}");
            return;
        }

        foreach (var file in Directory.GetFiles(path))
        {
            var ext = Path.GetExtension(file)?.ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".sprite")
            {
                var name = Path.GetFileNameWithoutExtension(file);
                _backgrounds.Add(new BackgroundInfo
                {
                    name = name,
                    sprite = LoadSprite(file),
                    isUnlocked = false,
                    isPurchased = false,
                    cost = 0,
                    required_level = 0,
                    required_score = 0
                });
            }
        }

        if (_backgrounds.Count == 0)
            Debug.LogWarning($"[BackgroundController] No background files found in: {path}");
    }

    Sprite LoadSprite(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return defaultSprite;
            var bytes = File.ReadAllBytes(filePath);
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(bytes)) return defaultSprite;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        catch
        {
            return defaultSprite;
        }
    }

    // --------------- JSON parsing ---------------
    void ApplyBackgroundStatusFromJson(string json, bool mayContainActiveFlag)
    {
        // Erlaubt sowohl Array-Root, als auch Objekt { items:[...], active_background:"..." }
        JToken root = JToken.Parse(json);

        IEnumerable<JToken> items = Enumerable.Empty<JToken>();
        string active = null;

        if (root.Type == JTokenType.Array)
        {
            items = root.Children();
            if (mayContainActiveFlag)
                active = root.FirstOrDefault()?["active_background"]?.ToString();
        }
        else if (root.Type == JTokenType.Object)
        {
            var ro = (JObject)root;
            if (ro["items"] is JArray arr) items = arr.Children();
            if (mayContainActiveFlag && ro["active_background"] != null)
                active = ro["active_background"]?.ToString();
        }

        foreach (var it in items)
        {
            string bgName = it["name"]?.ToString();
            if (string.IsNullOrEmpty(bgName)) continue;

            var bg = _backgrounds.FirstOrDefault(b => b.name == bgName);
            if (bg == null)
            {
                Debug.LogWarning($"[BackgroundController] Background '{bgName}' not found in disk list.");
                continue;
            }

            bg.isUnlocked      = it["is_unlocked"]?.Value<bool>()  ?? bg.isUnlocked;
            bg.isPurchased     = it["is_purchased"]?.Value<bool>() ?? bg.isPurchased;
            bg.cost            = it["required_coins"]?.Value<int>() ?? bg.cost;
            bg.required_score  = it["required_score"]?.Value<int>() ?? bg.required_score;
            bg.required_level  = it["required_level"]?.Value<int>() ?? bg.required_level;
        }

        if (!string.IsNullOrEmpty(active))
            SetActiveBackground(active);
    }

    void HandlePurchaseResponse(string json)
    {
        var jo = JObject.Parse(json);
        string status = jo["status"]?.ToString();
        string bgName = jo["background"]?.ToString();

        var bg = _backgrounds.FirstOrDefault(b => b.name == bgName);
        if (bg == null)
        {
            Debug.LogWarning($"[BackgroundController] Purchase response for unknown background '{bgName}'.");
            return;
        }

        if (status == "success")
        {
            bg.isPurchased = true;
            if (float.TryParse(jo["remaining_coins"]?.ToString(), out var rest))
                _userCoins = rest;

            costTextSafe("Unlocked");
            RefreshUI();
        }
        else
        {
            string err = jo["error"]?.ToString() ?? "Unknown error";
            costTextSafe("Purchase failed");
            Debug.LogWarning($"[BackgroundController] Purchase failed for '{bgName}': {err}");
        }
    }

    // --------------- Active selection + publish ---------------
    public void NextBackground()
    {
        if (_backgrounds.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _backgrounds.Count;
        RefreshUI();
        RestartPublishIfPurchasedCurrent();
    }

    public void PreviousBackground()
    {
        if (_backgrounds.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _backgrounds.Count) % _backgrounds.Count;
        RefreshUI();
        RestartPublishIfPurchasedCurrent();
    }

    void SetActiveBackground(string bgName)
    {
        var found = _backgrounds.FindIndex(b => b.name == bgName);
        _currentIndex = (found >= 0) ? found : Mathf.Max(0, _backgrounds.FindIndex(b => b.name == "10_1"));
    }

    void RestartPublishIfPurchasedCurrent()
    {
        var cur = Current();
        if (cur == null) return;
        if (cur.isUnlocked && cur.isPurchased)
        {
            if (_publishCoroutine != null) StopCoroutine(_publishCoroutine);
            _publishCoroutine = StartCoroutine(PublishActiveAfterDelay(cur, 3f));
        }
    }

    IEnumerator PublishActiveAfterDelay(BackgroundInfo bg, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        var jo = new JObject {
            ["background"] = bg.name,
            ["mqtt"] = true
        };
        try { msgHandler?.NotifyObservers(TopicActive(), jo); }
        catch (Exception ex) { Debug.LogWarning($"[BackgroundController] Publish active failed: {ex.Message}"); }
    }

    // --------------- Purchase ---------------
    public void PurchaseCurrentBackground()
    {
        var cur = Current();
        if (cur == null) return;

        if (!cur.isUnlocked || cur.isPurchased) return;

        var jo = new JObject {
            ["background"] = cur.name,
            ["coins"] = cur.cost,
            ["mqtt"] = true
        };

        try
        {
            msgHandler?.NotifyObservers(TopicPurchase(), jo);
            costTextSafe("Processing purchase...");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BackgroundController] Purchase publish failed: {ex.Message}");
        }
    }

    // --------------- UI ---------------
    void RefreshUI()
    {
        if (_backgrounds.Count == 0)
        {
            ShowNoBackgrounds();
            return;
        }

        var cur = Current();
        if (backgroundImage) backgroundImage.sprite = cur?.sprite ?? defaultSprite;

        if (cur == null)
        {
            ShowNoBackgrounds();
            return;
        }

        if (!cur.isUnlocked)
        {
            SetLock(true);
            SetShade(true);
            SetPurchaseButton(false, interactable:false);
            costTextSafe("Locked");
            requiredScoreTextSafe($"Required Score: {cur.required_score}");
            requiredLevelTextSafe($"Required Level: {cur.required_level}");
        }
        else if (!cur.isPurchased)
        {
            SetLock(false);
            SetShade(true);
            SetPurchaseButton(true, interactable:true); // Coins-Prüfung erfolgt im Core
            costTextSafe($"Cost: {cur.cost}");
            requiredScoreTextSafe("");
            requiredLevelTextSafe("");
        }
        else
        {
            SetLock(false);
            SetShade(false);
            SetPurchaseButton(false, interactable:false);
            costTextSafe("Unlocked");
            requiredScoreTextSafe("");
            requiredLevelTextSafe("");
        }
    }

    void ShowNoBackgrounds()
    {
        SetLock(true);
        SetShade(true);
        SetPurchaseButton(false, interactable:false);
        costTextSafe("No Backgrounds Available");
        requiredScoreTextSafe("");
        requiredLevelTextSafe("");
    }

    // --------------- helpers ---------------
    BackgroundInfo Current() => (_currentIndex >= 0 && _currentIndex < _backgrounds.Count) ? _backgrounds[_currentIndex] : null;

    void SetLock(bool v)          { if (lockIcon)       lockIcon.SetActive(v); }
    void SetShade(bool v)         { if (shadeOverlay)   shadeOverlay.SetActive(v); }
    void SetPurchaseButton(bool active, bool interactable)
    {
        if (!purchaseButton) return;
        purchaseButton.SetActive(active);
        var btn = purchaseButton.GetComponent<Button>();
        if (btn) btn.interactable = interactable;
    }

    void costTextSafe(string s)            { if (costText) costText.text = s; }
    void requiredScoreTextSafe(string s)   { if (requiredScoreText) requiredScoreText.text = s; }
    void requiredLevelTextSafe(string s)   { if (requiredLevelText) requiredLevelText.text = s; }

    [ContextMenu("Reset All (Locked)")]
    void ResetAllBackgrounds()
    {
        foreach (var b in _backgrounds) { b.isUnlocked = false; b.isPurchased = false; }
        _currentIndex = 0;
        RefreshUI();
    }

    // --------------- model ---------------
    sealed class BackgroundInfo
    {
        public string name;
        public Sprite sprite;
        public int cost;
        public bool isUnlocked;
        public bool isPurchased;
        public int required_score;
        public int required_level;
    }
}
