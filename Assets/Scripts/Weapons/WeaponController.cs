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
public class WeaponController : MonoBehaviour, IObserver
{
    [Header("UI")]
    [SerializeField] private Image weaponImage;
    [SerializeField] private Text costText;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private GameObject shadeOverlay;
    [SerializeField] private GameObject purchaseButton;
    [SerializeField] private Text requiredScoreText;
    [SerializeField] private Text requiredLevelText;

    [SerializeField] private Text strengthText;
    [SerializeField] private Text defenceText;

    [Header("MQTT / MsgHandler")]
    [Tooltip("Wird automatisch gefunden, wenn leer.")]
    public MsgHandler msgHandler;

    [Header("Topic Namespace (flexibel)")]
    [Tooltip("Hauptgruppe für Game-Themen, z. B. 'GAMING'.")]
    public string groupGame = "GAMING";
    [Tooltip("Entity-Key für Waffen, z. B. 'WEAPON'.")]
    public string keyWeapon = "WEAPON";
    [Tooltip("Userdaten-Gruppe, z. B. 'USERDATA'.")]
    public string groupUserData = "USERDATA";
    [Tooltip("Coins-Key innerhalb USERDATA.")]
    public string keyCoins = "COINS";
    [Tooltip("Weapons-Key innerhalb USERDATA.")]
    public string keyUserWeapons = "WEAPONS";

    [Header("Assets")]
    [Tooltip("Relativer Ordner unter StreamingAssets.")]
    public string weaponsFolder = "Weapons/v1_0";
    [Tooltip("Fallback-Sprite, falls Datei fehlt.")]
    public Sprite defaultSprite;

    // ---- runtime state ----
    private readonly List<WeaponInfo> _weapons = new();
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
        LoadWeaponsFromDisk();
        RefreshUI();
    }

    void OnDestroy()
    {
        UnregisterObservers();
    }

    // --------------- Button wiring ---------------
    void WireButtons()
    {
        if (nextButton)     nextButton.onClick.AddListener(NextWeapon);
        if (previousButton) previousButton.onClick.AddListener(PreviousWeapon);
        if (purchaseButton)
        {
            var btn = purchaseButton.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(PurchaseCurrentWeapon);
        }
    }

    // --------------- Topic building ---------------
    string TopicPurchase()         => TopicNamespace.TopicFor(groupGame, keyWeapon + "/" + SUB_PURCHASE);
    string TopicPurchaseResponse() => TopicNamespace.TopicFor(groupGame, keyWeapon + "/" + SUB_RESPONSE);
    string TopicActive()           => TopicNamespace.TopicFor(groupGame, keyWeapon + "/" + SUB_ACTIVE);
    string TopicUserWeapons()      => TopicNamespace.TopicFor(groupUserData, keyUserWeapons);
    string TopicUserCoins()        => TopicNamespace.TopicFor(groupUserData, keyCoins);

    void RegisterObservers()
    {
        if (msgHandler == null) return;

        msgHandler.RegisterObserver(TopicUserCoins(), this);
        msgHandler.RegisterObserver(TopicPurchaseResponse(), this);
        msgHandler.RegisterObserver(TopicUserWeapons(), this);
    }

    void UnregisterObservers()
    {
        if (msgHandler == null) return;

        msgHandler.RemoveObserver(TopicUserCoins(), this);
        msgHandler.RemoveObserver(TopicPurchaseResponse(), this);
        msgHandler.RemoveObserver(TopicUserWeapons(), this);
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
                    Debug.LogWarning($"[WeaponController] Could not parse coins from: {data}");
                }
                return;
            }

            // Weapons (status + active)
            if (eventType == TopicUserWeapons())
            {
                var json = data as string;
                if (!string.IsNullOrEmpty(json))
                {
                    ApplyWeaponStatusFromJson(json, mayContainActiveFlag: true);
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
            Debug.LogError($"[WeaponController] OnNotify error for '{eventType}': {ex}");
        }
    }

    // --------------- Disk load ---------------
    void LoadWeaponsFromDisk()
    {
        _weapons.Clear();

        string path = Path.Combine(Application.streamingAssetsPath, weaponsFolder);
        if (!Directory.Exists(path))
        {
            Debug.LogError($"[WeaponController] Weapons directory not found: {path}");
            return;
        }

        foreach (var file in Directory.GetFiles(path))
        {
            var ext = Path.GetExtension(file)?.ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".sprite")
            {
                var name = Path.GetFileNameWithoutExtension(file);
                _weapons.Add(new WeaponInfo
                {
                    name = name,
                    sprite = LoadSprite(file),
                    isUnlocked = false,
                    isPurchased = false,
                    cost = 0,
                    required_level = 0,
                    required_score = 0,
                    strength = 0,
                    defence = 0
                });
            }
        }

        if (_weapons.Count == 0)
            Debug.LogWarning($"[WeaponController] No weapon files found in: {path}");
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
    void ApplyWeaponStatusFromJson(string json, bool mayContainActiveFlag)
    {
        // Erlaubt sowohl Array-Root als auch Objekt { items:[.], active_weapon:"." }
        JToken root = JToken.Parse(json);

        IEnumerable<JToken> items = Enumerable.Empty<JToken>();
        string active = null;

        if (root.Type == JTokenType.Array)
        {
            items = root.Children();
            if (mayContainActiveFlag)
                active = root.FirstOrDefault()?["active_weapon"]?.ToString();
        }
        else if (root.Type == JTokenType.Object)
        {
            var ro = (JObject)root;
            if (ro["items"] is JArray arr) items = arr.Children();
            if (mayContainActiveFlag && ro["active_weapon"] != null)
                active = ro["active_weapon"]?.ToString();
        }

        foreach (var it in items)
        {
            string wName = it["name"]?.ToString();
            if (string.IsNullOrEmpty(wName)) continue;

            var w = _weapons.FirstOrDefault(a => a.name == wName);
            if (w == null)
            {
                Debug.LogWarning($"[WeaponController] Weapon '{wName}' not found in disk list.");
                continue;
            }

            w.isUnlocked      = it["is_unlocked"]?.Value<bool>()     ?? w.isUnlocked;
            w.isPurchased     = it["is_purchased"]?.Value<bool>()    ?? w.isPurchased;
            w.cost            = it["required_coins"]?.Value<int>()   ?? w.cost;
            w.required_score  = it["required_score"]?.Value<int>()   ?? w.required_score;
            w.required_level  = it["required_level"]?.Value<int>()   ?? w.required_level;
            w.strength        = it["strength"]?.Value<int>()         ?? w.strength;
            w.defence         = it["defence"]?.Value<int>()          ?? w.defence;
        }

        if (!string.IsNullOrEmpty(active))
            SetActiveWeapon(active);
    }

    void HandlePurchaseResponse(string json)
    {
        var jo = JObject.Parse(json);
        string status = jo["status"]?.ToString();
        string name   = jo["weapon"]?.ToString();

        var w = _weapons.FirstOrDefault(a => a.name == name);
        if (w == null)
        {
            Debug.LogWarning($"[WeaponController] Purchase response for unknown weapon '{name}'.");
            return;
        }

        if (status == "success")
        {
            w.isPurchased = true;
            costTextSafe("Unlocked");
            RefreshUI();
        }
        else
        {
            string err = jo["error"]?.ToString() ?? "Unknown error";
            costTextSafe("Purchase failed");
            Debug.LogWarning($"[WeaponController] Purchase failed for '{name}': {err}");
        }
    }

    // --------------- Active selection + publish ---------------
    public void NextWeapon()
    {
        if (_weapons.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _weapons.Count;
        RefreshUI();
        RestartPublishIfPurchasedCurrent();
    }

    public void PreviousWeapon()
    {
        if (_weapons.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _weapons.Count) % _weapons.Count;
        RefreshUI();
        RestartPublishIfPurchasedCurrent();
    }

    void SetActiveWeapon(string name)
    {
        var found = _weapons.FindIndex(a => a.name == name);
        _currentIndex = (found >= 0)
            ? found
            : Mathf.Max(0, _weapons.FindIndex(a => a.name == "10_1"));
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

    IEnumerator PublishActiveAfterDelay(WeaponInfo w, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        var jo = new JObject {
            ["weapon"] = w.name,
            ["mqtt"]   = true
        };

        try { msgHandler?.NotifyObservers(TopicActive(), jo); }
        catch (Exception ex) { Debug.LogWarning($"[WeaponController] Publish active failed: {ex.Message}"); }
    }

    // --------------- Purchase ---------------
    public void PurchaseCurrentWeapon()
    {
        var cur = Current();
        if (cur == null) return;

        // Nur, wenn vom Backend freigeschaltet und noch nicht gekauft
        if (!cur.isUnlocked || cur.isPurchased) return;

        var jo = new JObject {
            ["weapon"] = cur.name,
            ["mqtt"]   = true
        };

        try
        {
            msgHandler?.NotifyObservers(TopicPurchase(), jo);
            costTextSafe("Processing purchase...");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WeaponController] Purchase publish failed: {ex.Message}");
        }
    }

    // --------------- UI ---------------
    void RefreshUI()
    {
        if (_weapons.Count == 0)
        {
            ShowNoWeapons();
            return;
        }

        var cur = Current();
        if (weaponImage) weaponImage.sprite = cur?.sprite ?? defaultSprite;

        if (cur == null)
        {
            ShowNoWeapons();
            return;
        }

        strengthTextSafe($"Strength: {cur.strength}");
        defenceTextSafe($"Defence: {cur.defence}");

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
            SetPurchaseButton(true, interactable:true);
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

    void ShowNoWeapons()
    {
        SetLock(true);
        SetShade(true);
        SetPurchaseButton(false, interactable:false);
        costTextSafe("No Weapons Available");
        requiredScoreTextSafe("");
        requiredLevelTextSafe("");
        strengthTextSafe("");
        defenceTextSafe("");
    }

    // --------------- helpers ---------------
    WeaponInfo Current() =>
        (_currentIndex >= 0 && _currentIndex < _weapons.Count) ? _weapons[_currentIndex] : null;

    void SetLock(bool v)  { if (lockIcon)     lockIcon.SetActive(v); }
    void SetShade(bool v) { if (shadeOverlay) shadeOverlay.SetActive(v); }

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
    void defenceTextSafe(string s)       { if (defenceText) defenceText.text = s; }

    [ContextMenu("Reset All (Locked)")]
    void ResetAllWeapons()
    {
        foreach (var w in _weapons) { w.isUnlocked = false; w.isPurchased = false; }
        _currentIndex = 0;
        RefreshUI();
    }

    // --------------- model ---------------
    sealed class WeaponInfo
    {
        public string name;
        public Sprite sprite;
        public int cost;
        public bool isUnlocked;
        public bool isPurchased;
        public int required_score;
        public int required_level;
        public int strength;
        public int defence;
    }
}
