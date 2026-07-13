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
public class AvatarController : MonoBehaviour, IObserver
{
    [Header("UI")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private Text costText;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private GameObject shadeOverlay;
    [SerializeField] private GameObject purchaseButton;
    [SerializeField] private Text requiredScoreText;
    [SerializeField] private Text requiredLevelText;

    [SerializeField] private Text strengthText;
    [SerializeField] private Text healthText;

    [Header("MQTT / MsgHandler")]
    [Tooltip("Wird automatisch gefunden, wenn leer.")]
    public MsgHandler msgHandler;

    [Header("Topic Namespace (flexibel)")]
    [Tooltip("Hauptgruppe für Game-Themen, z. B. 'GAMING'.")]
    public string groupGame = "GAMING";
    [Tooltip("Entity-Key für Avatare, z. B. 'AVATAR'.")]
    public string keyAvatar = "AVATAR";
    [Tooltip("Userdaten-Gruppe, z. B. 'USERDATA'.")]
    public string groupUserData = "USERDATA";
    [Tooltip("Coins-Key innerhalb USERDATA.")]
    public string keyCoins = "COINS";
    [Tooltip("Avatars-Key innerhalb USERDATA.")]
    public string keyUserAvatars = "AVATARS";

    [Header("Assets")]
    [Tooltip("Relativer Ordner unter StreamingAssets.")]
    public string avatarsFolder = "Avatars/v1_0";
    [Tooltip("Fallback-Sprite, falls Datei fehlt.")]
    public Sprite defaultSprite;

    // ---- runtime state ----
    private readonly List<AvatarInfo> _avatars = new();
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
        LoadAvatarsFromDisk();
        RefreshUI();
    }

    void OnDestroy()
    {
        UnregisterObservers();
    }

    // --------------- Button wiring ---------------
    void WireButtons()
    {
        if (nextButton)     nextButton.onClick.AddListener(NextAvatar);
        if (previousButton) previousButton.onClick.AddListener(PreviousAvatar);
        if (purchaseButton)
        {
            var btn = purchaseButton.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(PurchaseCurrentAvatar);
        }
    }

    // --------------- Topic building ---------------
    string TopicPurchase()         => TopicNamespace.TopicFor(groupGame, keyAvatar + "/" + SUB_PURCHASE);
    string TopicPurchaseResponse() => TopicNamespace.TopicFor(groupGame, keyAvatar + "/" + SUB_RESPONSE);
    string TopicActive()           => TopicNamespace.TopicFor(groupGame, keyAvatar + "/" + SUB_ACTIVE);
    string TopicUserAvatars()      => TopicNamespace.TopicFor(groupUserData, keyUserAvatars);
    string TopicUserCoins()        => TopicNamespace.TopicFor(groupUserData, keyCoins);

    void RegisterObservers()
    {
        if (msgHandler == null) return;

        msgHandler.RegisterObserver(TopicUserCoins(), this);
        msgHandler.RegisterObserver(TopicPurchaseResponse(), this);
        msgHandler.RegisterObserver(TopicUserAvatars(), this);
    }

    void UnregisterObservers()
    {
        if (msgHandler == null) return;

        msgHandler.RemoveObserver(TopicUserCoins(), this);
        msgHandler.RemoveObserver(TopicPurchaseResponse(), this);
        msgHandler.RemoveObserver(TopicUserAvatars(), this);
    }

    // --------------- IObserver ---------------
    public void OnNotify(string eventType, object data)
    {
        try
        {
            if (string.IsNullOrEmpty(eventType)) return;

            // Coins
            if (eventType == TopicUserCoins())
            {
                if (float.TryParse(data?.ToString(), out var coinFloat))
                {
                    _userCoins = coinFloat;
                    RefreshUI();
                }
                else
                {
                    Debug.LogWarning($"[AvatarController] Could not parse coins from: {data}");
                }
                return;
            }

            // Avatars (status + active)
            if (eventType == TopicUserAvatars())
            {
                var json = data as string;
                if (!string.IsNullOrEmpty(json))
                {
                    ApplyAvatarStatusFromJson(json, mayContainActiveFlag: true);
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
            Debug.LogError($"[AvatarController] OnNotify error for '{eventType}': {ex}");
        }
    }

    // --------------- Disk load ---------------
    void LoadAvatarsFromDisk()
    {
        _avatars.Clear();

        string path = Path.Combine(Application.streamingAssetsPath, avatarsFolder);
        if (!Directory.Exists(path))
        {
            Debug.LogError($"[AvatarController] Avatars directory not found: {path}");
            return;
        }

        foreach (var file in Directory.GetFiles(path))
        {
            var ext = Path.GetExtension(file)?.ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".sprite")
            {
                var name = Path.GetFileNameWithoutExtension(file);
                _avatars.Add(new AvatarInfo
                {
                    name = name,
                    sprite = LoadSprite(file),
                    isUnlocked = false,
                    isPurchased = false,
                    cost = 0,
                    required_level = 0,
                    required_score = 0,
                    strength = 0,
                    health = 0
                });
            }
        }

        if (_avatars.Count == 0)
            Debug.LogWarning($"[AvatarController] No avatar files found in: {path}");
    }

    Sprite LoadSprite(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return defaultSprite;
            var bytes = File.ReadAllBytes(filePath);
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(bytes)) return defaultSprite;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f));
        }
        catch
        {
            return defaultSprite;
        }
    }

    // --------------- JSON parsing ---------------
    void ApplyAvatarStatusFromJson(string json, bool mayContainActiveFlag)
    {
        // Erlaubt sowohl Array-Root als auch Objekt { items:[.], active_avatar:"." }
        JToken root = JToken.Parse(json);

        IEnumerable<JToken> items = Enumerable.Empty<JToken>();
        string active = null;

        if (root.Type == JTokenType.Array)
        {
            items = root.Children();
            if (mayContainActiveFlag)
                active = root.FirstOrDefault()?["active_avatar"]?.ToString();
        }
        else if (root.Type == JTokenType.Object)
        {
            var ro = (JObject)root;
            if (ro["items"] is JArray arr) items = arr.Children();
            if (mayContainActiveFlag && ro["active_avatar"] != null)
                active = ro["active_avatar"]?.ToString();
        }

        foreach (var it in items)
        {
            string avName = it["name"]?.ToString();
            if (string.IsNullOrEmpty(avName)) continue;

            var av = _avatars.FirstOrDefault(a => a.name == avName);
            if (av == null)
            {
                Debug.LogWarning($"[AvatarController] Avatar '{avName}' not found in disk list.");
                continue;
            }

            av.isUnlocked      = it["is_unlocked"]?.Value<bool>()     ?? av.isUnlocked;
            av.isPurchased     = it["is_purchased"]?.Value<bool>()    ?? av.isPurchased;
            av.cost            = it["required_coins"]?.Value<int>()   ?? av.cost;
            av.required_score  = it["required_score"]?.Value<int>()   ?? av.required_score;
            av.required_level  = it["required_level"]?.Value<int>()   ?? av.required_level;
            av.strength        = it["strength"]?.Value<int>()         ?? av.strength;
            av.health          = it["health"]?.Value<int>()           ?? av.health;
        }

        if (!string.IsNullOrEmpty(active))
            SetActiveAvatar(active);
    }

    void HandlePurchaseResponse(string json)
    {
        var jo = JObject.Parse(json);
        string status = jo["status"]?.ToString();
        string name   = jo["avatar"]?.ToString();

        var av = _avatars.FirstOrDefault(a => a.name == name);
        if (av == null)
        {
            Debug.LogWarning($"[AvatarController] Purchase response for unknown avatar '{name}'.");
            return;
        }

        if (status == "success")
        {
            av.isPurchased = true;

            // optional: remaining_coins updaten, falls Backend es mitsendet
            if (float.TryParse(jo["remaining_coins"]?.ToString(), out var rest))
                _userCoins = rest;

            costTextSafe("Unlocked");
            RefreshUI();
        }
        else
        {
            string err = jo["error"]?.ToString() ?? "Unknown error";
            costTextSafe("Purchase failed");
            Debug.LogWarning($"[AvatarController] Purchase failed for '{name}': {err}");
        }
    }

    // --------------- Active selection + publish ---------------
    public void NextAvatar()
    {
        if (_avatars.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _avatars.Count;
        RefreshUI();
        RestartPublishIfPurchasedCurrent();
    }

    public void PreviousAvatar()
    {
        if (_avatars.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _avatars.Count) % _avatars.Count;
        RefreshUI();
        RestartPublishIfPurchasedCurrent();
    }

    void SetActiveAvatar(string name)
    {
        var found = _avatars.FindIndex(a => a.name == name);
        // Fallback: nimm den ersten Avatar (z. B. "10_1"), falls nicht gefunden
        _currentIndex = (found >= 0)
            ? found
            : Mathf.Max(0, _avatars.FindIndex(a => a.name == "10_1"));
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

    IEnumerator PublishActiveAfterDelay(AvatarInfo av, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        var jo = new JObject {
            ["avatar"] = av.name,
            ["mqtt"]   = true
        };

        try { msgHandler?.NotifyObservers(TopicActive(), jo); }
        catch (Exception ex) { Debug.LogWarning($"[AvatarController] Publish active failed: {ex.Message}"); }
    }

    // --------------- Purchase ---------------
    public void PurchaseCurrentAvatar()
    {
        var cur = Current();
        if (cur == null) return;

        // Nur, wenn vom Backend freigeschaltet und noch nicht gekauft
        if (!cur.isUnlocked || cur.isPurchased) return;

        var jo = new JObject {
            ["avatar"] = cur.name,
            ["coins"] = cur.cost,
            ["mqtt"]   = true
        };

        try
        {
            msgHandler?.NotifyObservers(TopicPurchase(), jo);
            costTextSafe("Processing purchase...");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AvatarController] Purchase publish failed: {ex.Message}");
        }
    }

    // --------------- UI ---------------
    void RefreshUI()
    {
        if (_avatars.Count == 0)
        {
            ShowNoAvatars();
            return;
        }

        var cur = Current();
        if (avatarImage) avatarImage.sprite = cur?.sprite ?? defaultSprite;

        if (cur == null)
        {
            ShowNoAvatars();
            return;
        }
        strengthTextSafe($"Strength: {cur.strength}");
        healthTextSafe($"Health: {cur.health}");
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
            SetPurchaseButton(true, interactable:true); // Coins-Check macht Backend
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

    void ShowNoAvatars()
    {
        SetLock(true);
        SetShade(true);
        SetPurchaseButton(false, interactable:false);
        costTextSafe("No Avatars Available");
        requiredScoreTextSafe("");
        requiredLevelTextSafe("");
        strengthTextSafe("");
        healthTextSafe("");
    }

    // --------------- helpers ---------------
    AvatarInfo Current() =>
        (_currentIndex >= 0 && _currentIndex < _avatars.Count) ? _avatars[_currentIndex] : null;

    void SetLock(bool v)        { if (lockIcon)     lockIcon.SetActive(v); }
    void SetShade(bool v)       { if (shadeOverlay) shadeOverlay.SetActive(v); }
    void SetPurchaseButton(bool active, bool interactable)
    {
        if (!purchaseButton) return;
        purchaseButton.SetActive(active);
        var btn = purchaseButton.GetComponent<Button>();
        if (btn) btn.interactable = interactable;
    }

    void costTextSafe(string s)          { if (costText) costText.text = s; }
    void requiredScoreTextSafe(string s) { if (requiredScoreText) requiredScoreText.text = s; }
    void requiredLevelTextSafe(string s) { if (requiredLevelText) requiredLevelText.text = s; }
    void strengthTextSafe(string s)      { if (strengthText) strengthText.text = s; }
    void healthTextSafe(string s)        { if (healthText) healthText.text = s; }
    [ContextMenu("Reset All (Locked)")]
    void ResetAllAvatars()
    {
        foreach (var a in _avatars) { a.isUnlocked = false; a.isPurchased = false; }
        _currentIndex = 0;
        RefreshUI();
    }

    // --------------- model ---------------
    sealed class AvatarInfo
    {
        public string name;
        public Sprite sprite;
        public int cost;
        public bool isUnlocked;
        public bool isPurchased;
        public int required_score;
        public int required_level;
        public int strength;
        public int health;
    }
}
