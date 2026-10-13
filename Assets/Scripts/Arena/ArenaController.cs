using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using Rocworks.Mqtt;

public class ArenaController : MonoBehaviour, IObserver
{
    [Header("UI - Images")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private Image weaponImage;
    [SerializeField] private Image enemyImage;

    [Header("UI - Stats Text")]
    [SerializeField] private Text strengthText;
    [SerializeField] private Text defenceText;
    [SerializeField] private Text healthText;

    [Header("UI - Enemy Stats Text")]
    [SerializeField] private Text enemyStrengthText;
    [SerializeField] private Text enemyDefenceText;
    [SerializeField] private Text enemyHealthText;

    [Header("UI - Health Bars (Image.fillAmount)")]
    [SerializeField] private Image avatarHealthBar;
    [SerializeField] private Image enemyHealthBar;

    [Header("UI - Navigation Buttons")]
    [SerializeField] private Button avatarPrevButton;
    [SerializeField] private Button avatarNextButton;
    [SerializeField] private Button weaponPrevButton;
    [SerializeField] private Button weaponNextButton;
    [SerializeField] private Button enemyPrevButton;
    [SerializeField] private Button enemyNextButton;

    [Header("UI - Hit Feedback Popups")]
    [SerializeField] private GameObject avatarCriticalPopup;
    [SerializeField] private Text avatarCriticalText;
    [SerializeField] private GameObject avatarBlockedPopup;
    [SerializeField] private Text avatarBlockedText;

    [SerializeField] private GameObject enemyCriticalPopup;
    [SerializeField] private Text enemyCriticalText;
    [SerializeField] private GameObject enemyBlockedPopup;
    [SerializeField] private Text enemyBlockedText;

    [Header("VFX Prefabs (werden instanziert)")]
    [SerializeField] private ParticleSystem avatarCriticalPrefab;
    [SerializeField] private ParticleSystem avatarBlockedPrefab;
    [SerializeField] private ParticleSystem enemyCriticalPrefab;
    [SerializeField] private ParticleSystem enemyBlockedPrefab;

    [Header("MQTT Control")]
    [SerializeField] private string TopicControl = "ARENA/SELECTION"; 

    [Header("UI - Enemy State")]
    [SerializeField] private Image enemyStateIcon;          // kleines Icon beim Enemy
    [SerializeField] private Sprite enemyAliveStateSprite;  // z.B. leeres / normal
    [SerializeField] private Sprite enemyDefeatedStateSprite; // z.B. Skull / Haken

    [Header("UI - Arena Result")]
    [Tooltip("Text for the final arena result title, e.g. VICTORY or DEFEAT.")]
    [SerializeField] private Text arenaResultTitleText;
    [Tooltip("Text for the final arena result message.")]
    [SerializeField] private Text arenaResultMessageText;
    [Tooltip("0 means the result stays visible until PREPARE/FIGHT resets it.")]
    [SerializeField] private float resultAutoHideSeconds = 0f;

    [Header("Arena View Visibility")]
    [Tooltip("Zuordnen: bestehender PageUsageTracker der Hauptnavigation.")]
    [SerializeField] private PageUsageTracker pageUsageTracker;
    [Tooltip("Exakte Page-ID der Arena im PageUsageTracker. Falls leer: nur CanvasGroup prüfen.")]
    [SerializeField] private string arenaPageId = "";
    [Tooltip("Optional: echte Arena-CanvasGroup (alpha=0 bedeutet unsichtbar).")]
    [SerializeField] private CanvasGroup arenaCanvasGroup;

    [Header("Camera")]
    [SerializeField] private Camera mainCamera;

    [Header("Assets (Sprites von Disk)")]
    [Tooltip("Relativer Ordner unter StreamingAssets.")]
    public string avatarsFolder = "Avatars/v1_0";
    public string weaponsFolder = "Weapons/v1_0";
    public string enemiesFolder = "Enemies/v1_0";

    public Sprite defaultAvatarSprite;
    public Sprite defaultWeaponSprite;
    public Sprite defaultEnemySprite;

    [Header("MQTT / MsgHandler")]
    public MsgHandler msgHandler;

    [Header("Topic Namespace")]
    public string groupUserData = "USERDATA";
    public string keyUserAvatars = "AVATARS";
    public string keyUserWeapons = "WEAPONS";

    public string groupGame = "GAMING";
    public string keyArena = "ARENA";

    // ---- runtime state ----
    private readonly List<AvatarInfo> _avatars = new();
    private readonly List<WeaponInfo> _weapons = new();
    private readonly List<EnemyInfo> _enemies = new();

    private int _avatarIndex = 0;
    private int _weaponIndex = 0;
    private int _enemyIndex = 0;

    private ArenaPhase _phase = ArenaPhase.Prepare;

    private float _avatarMaxHealth;
    private float _avatarCurrentHealth;
    private float _enemyMaxHealth;
    private float _enemyCurrentHealth;

    private Coroutine _avatarHealthCoroutine;
    private Coroutine _enemyHealthCoroutine;

    private readonly HashSet<string> _seenArenaHitIds = new HashSet<string>();
    private readonly Queue<string> _seenArenaHitOrder = new Queue<string>();
    private const int MaxArenaEventIds = 512;

    private readonly List<GameObject> _activeHitEffects = new List<GameObject>();
    private bool _wasArenaVisible;
    private bool _warnedMissingViewSource;

    private Coroutine _avatarHitSequenceCo;
    private Coroutine _enemyHitSequenceCo;

    private Coroutine _avatarCritCo, _avatarBlockCo, _enemyCritCo, _enemyBlockCo;
    private Coroutine _resultTextCo;
    private string _lastArenaResult;

    private enum ArenaPhase { Prepare, Fight, Finished }

    // -------- Unity --------
    private void Awake()
    {
        if (!msgHandler) msgHandler = FindObjectOfType<MsgHandler>();
        if (!mainCamera) mainCamera = Camera.main;
        if (!pageUsageTracker) pageUsageTracker = FindObjectOfType<PageUsageTracker>();

        WireButtons();
        LoadSpritesFromDisk();

        if (avatarCriticalPopup) avatarCriticalPopup.SetActive(false);
        if (avatarBlockedPopup)  avatarBlockedPopup.SetActive(false);
        if (enemyCriticalPopup)  enemyCriticalPopup.SetActive(false);
        if (enemyBlockedPopup)   enemyBlockedPopup.SetActive(false);

        HideArenaResult();
    }

    private void Start()
    {
        RegisterObservers();
        RefreshUI();
    }

    private void OnDestroy()
    {
        UnregisterObservers();
        StopHiddenHitFeedback();
    }

    private void Update()
    {
        bool visible = IsArenaVisible();
        if (_wasArenaVisible && !visible) StopHiddenHitFeedback();
        _wasArenaVisible = visible;
    }

    private bool IsArenaVisible()
    {
        bool hasPage = pageUsageTracker != null;
        bool hasCanvas = arenaCanvasGroup != null;
        // A scrolling navigation needs an explicit page identity. A CanvasGroup
        // alone cannot prove its offscreen page is currently visible.
        if (hasPage && string.IsNullOrWhiteSpace(arenaPageId)) return false;
        if (!hasPage && !hasCanvas)
        {
            if (!_warnedMissingViewSource)
            {
                Debug.LogWarning("[Arena] Configure PageUsageTracker/arenaPageId or arenaCanvasGroup. HIT VFX disabled until configured.");
                _warnedMissingViewSource = true;
            }
            return false;
        }

        bool pageVisible = !hasPage || pageUsageTracker.IsCurrentPage(arenaPageId);
        bool canvasVisible = !hasCanvas ||
            (arenaCanvasGroup.gameObject.activeInHierarchy && arenaCanvasGroup.alpha > 0.01f);
        return pageVisible && canvasVisible;
    }

    private void StopHiddenHitFeedback()
    {
        if (_avatarHitSequenceCo != null) StopCoroutine(_avatarHitSequenceCo);
        if (_enemyHitSequenceCo != null) StopCoroutine(_enemyHitSequenceCo);
        if (_avatarCritCo != null) StopCoroutine(_avatarCritCo);
        if (_avatarBlockCo != null) StopCoroutine(_avatarBlockCo);
        if (_enemyCritCo != null) StopCoroutine(_enemyCritCo);
        if (_enemyBlockCo != null) StopCoroutine(_enemyBlockCo);
        _avatarHitSequenceCo = _enemyHitSequenceCo = null;
        _avatarCritCo = _avatarBlockCo = _enemyCritCo = _enemyBlockCo = null;
        if (avatarCriticalPopup) avatarCriticalPopup.SetActive(false);
        if (avatarBlockedPopup) avatarBlockedPopup.SetActive(false);
        if (enemyCriticalPopup) enemyCriticalPopup.SetActive(false);
        if (enemyBlockedPopup) enemyBlockedPopup.SetActive(false);
        foreach (var effect in _activeHitEffects)
            if (effect) Destroy(effect);
        _activeHitEffects.Clear();
    }

    // -------- MQTT topics --------
    private string TopicUserAvatars()   => TopicNamespace.TopicFor(groupUserData, keyUserAvatars);
    private string TopicUserWeapons()   => TopicNamespace.TopicFor(groupUserData, keyUserWeapons);
    private string TopicArenaState()    => TopicNamespace.TopicFor(groupGame, keyArena + "/STATE");
    private string TopicArenaHit()      => TopicNamespace.TopicFor(groupGame, keyArena + "/HIT");
    private string TopicArenaEnemies()  => TopicNamespace.TopicFor(groupGame, keyArena + "/ENEMIES");

    // NEW: Topic für die Auswahl, die beim Freeze/Fight an das Backend gesendet wird
    private string TopicArenaSelection() => TopicNamespace.TopicFor(groupGame, keyArena + "/SELECTION");

    private void RegisterObservers()
    {
        if (msgHandler == null) return;

        msgHandler.RegisterObserver(TopicUserAvatars(),  this);
        msgHandler.RegisterObserver(TopicUserWeapons(),  this);
        msgHandler.RegisterObserver(TopicArenaState(),   this);
        msgHandler.RegisterObserver(TopicArenaHit(),     this);
        msgHandler.RegisterObserver(TopicArenaEnemies(), this);
    }

    private void UnregisterObservers()
    {
        if (msgHandler == null) return;

        msgHandler.RemoveObserver(TopicUserAvatars(),  this);
        msgHandler.RemoveObserver(TopicUserWeapons(),  this);
        msgHandler.RemoveObserver(TopicArenaState(),   this);
        msgHandler.RemoveObserver(TopicArenaHit(),     this);
        msgHandler.RemoveObserver(TopicArenaEnemies(), this);
    }

    // -------- Buttons --------
    private void WireButtons()
    {
        if (avatarPrevButton) avatarPrevButton.onClick.AddListener(PrevAvatar);
        if (avatarNextButton) avatarNextButton.onClick.AddListener(NextAvatar);
        if (weaponPrevButton) weaponPrevButton.onClick.AddListener(PrevWeapon);
        if (weaponNextButton) weaponNextButton.onClick.AddListener(NextWeapon);
        if (enemyPrevButton)  enemyPrevButton.onClick.AddListener(PrevEnemy);
        if (enemyNextButton)  enemyNextButton.onClick.AddListener(NextEnemy);
    }

    // -------- IObserver --------
    public void OnNotify(string eventType, object data)
    {
        try
        {
            if (string.IsNullOrEmpty(eventType)) return;

            if (eventType == TopicUserAvatars())
            {
                var json = data as string;
                if (!string.IsNullOrEmpty(json))
                {
                    ApplyAvatarStatus(json);
                    ClampIndices();
                    RefreshUI();
                }
            }
            else if (eventType == TopicUserWeapons())
            {
                var json = data as string;
                if (!string.IsNullOrEmpty(json))
                {
                    ApplyWeaponStatus(json);
                    ClampIndices();
                    RefreshUI();
                }
            }
            else if (eventType == TopicArenaState())
            {
                var json = data as string;
                if (!string.IsNullOrEmpty(json))
                {
                    ApplyArenaState(json);
                    ClampIndices();
                    RefreshUI();
                }
            }
            else if (eventType == TopicArenaHit())
            {
                var json = data as string;
                if (!string.IsNullOrEmpty(json))
                {
                    HandleHitEvent(json);
                }
            }
            else if (eventType == TopicArenaEnemies())
            {
                var json = data as string;
                if (!string.IsNullOrEmpty(json))
                {
                    ApplyEnemyStatus(json);
                    ClampIndices();
                    RefreshUI();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ArenaController] OnNotify error for '{eventType}': {ex}");
        }
    }

    // -------- Load sprites einmalig --------
    private void LoadSpritesFromDisk()
    {
        _avatars.Clear();
        _weapons.Clear();
        _enemies.Clear();

        // Avatars
        string avatarPath = Path.Combine(Application.streamingAssetsPath, avatarsFolder);
        if (Directory.Exists(avatarPath))
        {
            foreach (var file in Directory.GetFiles(avatarPath))
            {
                var ext = Path.GetExtension(file)?.ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg")
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    _avatars.Add(new AvatarInfo
                    {
                        name = name,
                        sprite = LoadSprite(file),
                        strength = 0,
                        health = 0,
                        isPurchased = false
                    });
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ArenaController] Avatar directory not found: {avatarPath}");
        }

        // Weapons
        string weaponPath = Path.Combine(Application.streamingAssetsPath, weaponsFolder);
        if (Directory.Exists(weaponPath))
        {
            foreach (var file in Directory.GetFiles(weaponPath))
            {
                var ext = Path.GetExtension(file)?.ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg")
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    _weapons.Add(new WeaponInfo
                    {
                        name = name,
                        sprite = LoadSprite(file),
                        strength = 0,
                        defence = 0,
                        isPurchased = false
                    });
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ArenaController] Weapon directory not found: {weaponPath}");
        }

        // Enemies
        string enemyPath = Path.Combine(Application.streamingAssetsPath, enemiesFolder);
        if (Directory.Exists(enemyPath))
        {
            foreach (var file in Directory.GetFiles(enemyPath))
            {
                var ext = Path.GetExtension(file)?.ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg")
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    _enemies.Add(new EnemyInfo
                    {
                        name = name,
                        sprite = LoadSprite(file),
                        strength = 0,
                        health = 0,
                        defence = 0,
                        tier = 0,
                        isDefeated = false
                    });
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ArenaController] Enemy directory not found: {enemyPath}");
        }
    }

    private Sprite LoadSprite(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(bytes)) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f));
        }
        catch
        {
            return null;
        }
    }

    // -------- Apply JSON vom Backend --------
    private void ApplyAvatarStatus(string json)
    {
        var root = JToken.Parse(json);
        var container = (root["items"] as JArray) ?? root as JArray;
        if (container == null) return;

        foreach (var it in container)
        {
            string name = it["name"]?.ToString();
            if (string.IsNullOrEmpty(name)) continue;

            var av = _avatars.FirstOrDefault(a => a.name == name);
            if (av == null) continue;

            av.isPurchased = it["is_purchased"]?.Value<bool>() ?? av.isPurchased;
            av.strength    = it["strength"]?.Value<int>()      ?? av.strength;
            av.health      = it["health"]?.Value<int>()        ?? av.health;
        }

        if (_phase == ArenaPhase.Prepare)
        {
            var pav = PurchasedAvatars();
            var cur = pav.Count > 0 ? pav[Mathf.Clamp(_avatarIndex, 0, pav.Count - 1)] : null;
            _avatarMaxHealth     = cur?.health ?? 0f;
            _avatarCurrentHealth = _avatarMaxHealth;
        }
    }

    private void ApplyWeaponStatus(string json)
    {
        var root = JToken.Parse(json);
        var container = (root["items"] as JArray) ?? root as JArray;
        if (container == null) return;

        foreach (var it in container)
        {
            string name = it["name"]?.ToString();
            if (string.IsNullOrEmpty(name)) continue;

            var w = _weapons.FirstOrDefault(x => x.name == name);
            if (w == null) continue;

            w.isPurchased = it["is_purchased"]?.Value<bool>() ?? w.isPurchased;
            w.strength    = it["strength"]?.Value<int>()      ?? w.strength;
            w.defence     = it["defence"]?.Value<int>()       ?? w.defence;
        }
    }

    private void ApplyEnemyStatus(string json)
    {
        var root = JToken.Parse(json);
        var container = (root["items"] as JArray) ?? root as JArray;
        if (container == null) return;

        foreach (var it in container)
        {
            string name = it["name"]?.ToString();
            if (string.IsNullOrEmpty(name)) continue;

            var e = _enemies.FirstOrDefault(x => x.name == name);
            if (e == null) continue;

            e.strength = it["strength"]?.Value<int>() ?? e.strength;
            e.health   = it["health"]?.Value<int>()   ?? e.health;
            e.defence  = it["defense"]?.Value<int>()  ?? e.defence;
            e.tier     = it["tier"]?.Value<int>()     ?? e.tier;
            e.isDefeated = it["is_defeated"]?.Value<bool>() ?? e.isDefeated;
        }

        if (_phase == ArenaPhase.Prepare)
        {
            var pen = AllEnemies();
            var cur = pen.Count > 0 ? pen[Mathf.Clamp(_enemyIndex, 0, pen.Count - 1)] : null;
            _enemyMaxHealth     = cur?.health ?? 0f;
            _enemyCurrentHealth = _enemyMaxHealth;
        }
    }

    private void ApplyArenaState(string json)
    {
        var jo = JObject.Parse(json);

        string eventName = jo["event"]?.ToString()?.Trim().ToUpperInvariant();
        string result = jo["result"]?.ToString()?.Trim().ToUpperInvariant();

        var phaseToken = jo["phase"];
        bool changedToFight = false;

        if (phaseToken != null)
        {
            var raw = phaseToken.ToString();
            var phaseStr = raw.Trim().ToUpperInvariant();
            var oldPhase = _phase;

            _phase = phaseStr switch
            {
                "FIGHT"    => ArenaPhase.Fight,
                "PREPARE"  => ArenaPhase.Prepare,
                "FINISHED" => ArenaPhase.Finished,
                _          => _phase
            };

            changedToFight = (oldPhase != _phase && _phase == ArenaPhase.Fight);

            Debug.Log($"[Arena] ApplyArenaState phase='{raw}' -> '{phaseStr}' {oldPhase} => {_phase}");
        }

        var avatarName = jo["avatar"]?.ToString();
        var weaponName = jo["weapon"]?.ToString();
        var enemyName  = jo["enemy"]?.ToString();

        if (!string.IsNullOrEmpty(avatarName))
        {
            var idx = PurchasedAvatars().FindIndex(a => a.name == avatarName);
            if (idx >= 0) _avatarIndex = idx;
        }

        if (!string.IsNullOrEmpty(weaponName))
        {
            var idx = PurchasedWeapons().FindIndex(w => w.name == weaponName);
            if (idx >= 0) _weaponIndex = idx;
        }

        if (!string.IsNullOrEmpty(enemyName))
        {
            var pen = AllEnemies();
            var idx = pen.FindIndex(e => e.name == enemyName);
            if (idx >= 0) _enemyIndex = idx;
        }

        if (_phase == ArenaPhase.Prepare || changedToFight)
        {
            HideArenaResult();
            StopHiddenHitFeedback();
            ResetSelectedHealth();
        }

        float avatarCurrent = jo["avatar_current_health"]?.Value<float>() ?? -1f;
        float avatarMax     = jo["avatar_max_health"]?.Value<float>()     ?? -1f;
        float enemyCurrent  = jo["enemy_current_health"]?.Value<float>()  ?? -1f;
        float enemyMax      = jo["enemy_max_health"]?.Value<float>()      ?? -1f;

        if (avatarMax > 0f) _avatarMaxHealth = avatarMax;
        if (avatarCurrent >= 0f) _avatarCurrentHealth = Mathf.Clamp(avatarCurrent, 0f, _avatarMaxHealth);

        if (enemyMax > 0f) _enemyMaxHealth = enemyMax;
        if (enemyCurrent >= 0f) _enemyCurrentHealth = Mathf.Clamp(enemyCurrent, 0f, _enemyMaxHealth);

        if (changedToFight)
        {
            HideArenaResult();
            PublishCurrentSelection();
        }

        bool fightFinished =
            eventName == "FIGHT_FINISHED" ||
            _phase == ArenaPhase.Finished ||
            !string.IsNullOrEmpty(result);

        if (fightFinished)
        {
            _phase = ArenaPhase.Finished;
            _lastArenaResult = result;

            if (result == "WIN")
            {
                MarkCurrentEnemyDefeated(enemyName);
            }

            ShowArenaResult(result, jo);
        }
    }

    private static string FormatArenaOutcomeReason(string reason, string fallback)
    {
        switch (reason)
        {
            case "QUALITY_FAILED": return "Quality check failed. No victory.";
            case "ERRORS_DETECTED": return "Assembly errors detected. No victory.";
            case "BENCHMARK_EXCEEDED": return "Assembly time exceeded the benchmark.";
            case "AVATAR_DEFEATED": return "Your avatar has no health remaining.";
            case "INCOMPLETE_WORKSTEP_EVIDENCE": return "Not all worksteps have verified completion evidence.";
            case "BENCHMARK_UNKNOWN": return "No verified benchmark available for this run.";
            case "DURATION_UNKNOWN": return "No reliable total assembly duration was measured.";
            case "ERROR_COVERAGE_UNKNOWN": return "Errors cannot be assessed with the available sensor data.";
            case "QUALITY_UNKNOWN": return "Product quality was not verified.";
            default: return fallback;
        }
    }

    private void ShowArenaResult(string result, JObject payload)
    {
        string normalized = string.IsNullOrEmpty(result)
            ? "FINISHED"
            : result.Trim().ToUpperInvariant();

        string title;
        string message;

        switch (normalized)
        {
            case "WIN":
                title = "VICTORY!";
                message = BuildWinMessage(payload);
                break;

            case "LOSE":
                title = "CHALLENGE FAILED";
                message = FormatArenaOutcomeReason(payload["result_reason"]?.ToString(),
                    "Benchmark, quality or error-free completion was not achieved.");
                break;

            case "UNVERIFIED":
                title = "RESULT NOT VERIFIED";
                message = FormatArenaOutcomeReason(payload["result_reason"]?.ToString(),
                    "Missing benchmark, quality, error coverage or workstep evidence. No reward granted.");
                break;

            case "DRAW":
                title = "DRAW!";
                message = "Both fighters collapsed at the same time.";
                break;

            default:
                title = "FIGHT FINISHED";
                message = "Arena fight completed.";
                break;
        }

        if (arenaResultTitleText)
        {
            arenaResultTitleText.enabled = true;
            arenaResultTitleText.text = title;
        }

        if (arenaResultMessageText)
        {
            arenaResultMessageText.enabled = true;
            arenaResultMessageText.text = message;
        }

        if (_resultTextCo != null)
        {
            StopCoroutine(_resultTextCo);
            _resultTextCo = null;
        }

        if (resultAutoHideSeconds > 0f)
        {
            _resultTextCo = StartCoroutine(HideArenaResultAfterDelay(resultAutoHideSeconds));
        }

        Debug.Log($"[Arena] Result shown: {title} - {message}");
    }

    private string BuildWinMessage(JObject payload)
    {
        string enemyName = payload["enemy"]?.ToString();
        string quality = payload["quality"]?.ToString();

        if (!string.IsNullOrEmpty(enemyName))
            return $"Enemy defeated: {enemyName}";

        if (!string.IsNullOrEmpty(quality))
            return $"Enemy defeated! Quality: {quality}";

        return "Enemy defeated!";
    }

    private IEnumerator HideArenaResultAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideArenaResult();
    }

    private void HideArenaResult()
    {
        if (_resultTextCo != null)
        {
            StopCoroutine(_resultTextCo);
            _resultTextCo = null;
        }

        if (arenaResultTitleText)
        {
            arenaResultTitleText.text = string.Empty;
            arenaResultTitleText.enabled = false;
        }

        if (arenaResultMessageText)
        {
            arenaResultMessageText.text = string.Empty;
            arenaResultMessageText.enabled = false;
        }

        _lastArenaResult = null;
    }

    private void MarkCurrentEnemyDefeated(string enemyNameFromPayload)
    {
        string enemyName = enemyNameFromPayload;

        if (string.IsNullOrEmpty(enemyName))
        {
            var enemies = AllEnemies();
            EnemyInfo currentEnemy = enemies.Count > 0
                ? enemies[Mathf.Clamp(_enemyIndex, 0, enemies.Count - 1)]
                : null;

            enemyName = currentEnemy?.name;
        }

        if (string.IsNullOrEmpty(enemyName)) return;

        var enemy = _enemies.FirstOrDefault(e => e.name == enemyName);
        if (enemy == null) return;

        enemy.isDefeated = true;
        Debug.Log($"[Arena] Enemy marked defeated locally: {enemyName}");
    }

    // NEW: Auswahl des Users an Backend senden
    private void PublishCurrentSelection()
    {
        if (msgHandler == null) return;

        var pav = PurchasedAvatars();
        var pwe = PurchasedWeapons();
        var pen = AllEnemies();

        AvatarInfo curAvatar = pav.Count > 0 ? pav[Mathf.Clamp(_avatarIndex, 0, pav.Count - 1)] : null;
        WeaponInfo curWeapon = pwe.Count > 0 ? pwe[Mathf.Clamp(_weaponIndex, 0, pwe.Count - 1)] : null;
        EnemyInfo  curEnemy  = pen.Count > 0 ? pen[Mathf.Clamp(_enemyIndex,  0, pen.Count - 1)] : null;

        var payload = new JObject
        {
            ["mqtt"] = true   // wichtig fürs Rauspublishen
        };

        if (curAvatar != null) payload["avatar"] = curAvatar.name;
        if (curWeapon != null) payload["weapon"] = curWeapon.name;
        if (curEnemy  != null) payload["enemy"]  = curEnemy.name;

        Debug.Log($"[Arena] Sending selection on freeze/fight: {payload}");

        // intern wie überall: Topic wird generisch gebaut,
        // SystemController macht daraus FHAACHEN/LEANLAB/.../GAMING/ARENA/SELECTION
        msgHandler?.NotifyObservers(TopicArenaSelection(), payload);
    }

    // -------- HIT Handling --------
    private void HandleHitEvent(string json)
    {
        var jo = JObject.Parse(json);

        string hitId = jo["event_id"]?.ToString();
        if (!string.IsNullOrEmpty(hitId))
        {
            if (!_seenArenaHitIds.Add(hitId)) return;
            _seenArenaHitOrder.Enqueue(hitId);
            if (_seenArenaHitOrder.Count > MaxArenaEventIds)
                _seenArenaHitIds.Remove(_seenArenaHitOrder.Dequeue());
        }

        string target = jo["target"]?.ToString()?.Trim().ToUpperInvariant();
        if (target != "AVATAR" && target != "ENEMY")
        {
            Debug.LogWarning($"[Arena] Ignoring HIT with unknown target '{target}'. Payload={json}");
            return;
        }

        float currentHealth = jo["current_health"]?.Value<float>() ?? 0f;
        float maxHealth = jo["max_health"]?.Value<float>() ?? 0f;

        int damage = jo["damage"]?.Value<int>() ?? 0;
        bool criticalHit = ReadFlag(jo["critical_hit"]);
        int criticalDamage = jo["critical_damage"]?.Value<int>() ?? 0;
        bool blockedHit = ReadFlag(jo["blocked_hit"]);
        int blockedDamage = jo["blocked_damage"]?.Value<int>() ?? 0;
        bool overtimeHit = ReadFlag(jo["overtime_hit"]);
        bool finalHit = ReadFlag(jo["final_hit"]);

        currentHealth = Mathf.Max(0f, currentHealth);
        damage = Mathf.Max(0, damage);
        criticalDamage = Mathf.Max(0, criticalDamage);
        blockedDamage = Mathf.Max(0, blockedDamage);

        // Ein HIT ist ein eindeutiges Signal, dass der Kampf läuft. So kann ein
        // verspäteter STATE-Event die Healthbar nicht durch PREPARE-Logik resetten.
        if (_phase == ArenaPhase.Prepare)
        {
            _phase = ArenaPhase.Fight;
            HideArenaResult();
        }

        bool isEnemy = target == "ENEMY";

        if (isEnemy)
        {
            if (maxHealth > 0f)
                _enemyMaxHealth = maxHealth;

            if (_enemyMaxHealth <= 0f)
                _enemyMaxHealth = Mathf.Max(1f, currentHealth);

            _enemyCurrentHealth = Mathf.Clamp(currentHealth, 0f, _enemyMaxHealth);

            if (enemyHealthText)
                enemyHealthText.text = $"Health: {_enemyCurrentHealth:F0}";

            StartEnemyHealthAnimation();
            StartHitFeedback(
                isEnemy: true,
                damage: damage,
                criticalHit: criticalHit,
                criticalDamage: criticalDamage,
                blockedHit: blockedHit,
                blockedDamage: blockedDamage,
                overtimeHit: overtimeHit,
                finalHit: finalHit);
        }
        else
        {
            if (maxHealth > 0f)
                _avatarMaxHealth = maxHealth;

            if (_avatarMaxHealth <= 0f)
                _avatarMaxHealth = Mathf.Max(1f, currentHealth);

            _avatarCurrentHealth = Mathf.Clamp(currentHealth, 0f, _avatarMaxHealth);

            if (healthText)
                healthText.text = $"Health: {_avatarCurrentHealth:F0}";

            StartAvatarHealthAnimation();
            StartHitFeedback(
                isEnemy: false,
                damage: damage,
                criticalHit: criticalHit,
                criticalDamage: criticalDamage,
                blockedHit: blockedHit,
                blockedDamage: blockedDamage,
                overtimeHit: overtimeHit,
                finalHit: finalHit);
        }

        Debug.Log(
            $"[Arena] HIT target={target} damage={damage} current={currentHealth} max={maxHealth} " +
            $"critical={criticalHit}/{criticalDamage} blocked={blockedHit}/{blockedDamage} " +
            $"overtime={overtimeHit} final={finalHit}");
    }

    private static bool ReadFlag(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
            return false;

        if (token.Type == JTokenType.Boolean)
            return token.Value<bool>();

        if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            return Mathf.Abs(token.Value<float>()) > Mathf.Epsilon;

        string value = token.ToString().Trim();
        return value == "1" ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private void StartAvatarHealthAnimation()
    {
        if (!avatarHealthBar)
            return;

        if (_avatarHealthCoroutine != null)
            StopCoroutine(_avatarHealthCoroutine);

        _avatarHealthCoroutine = StartCoroutine(AnimateAvatarHealthBar());
    }

    private void StartEnemyHealthAnimation()
    {
        if (!enemyHealthBar)
            return;

        if (_enemyHealthCoroutine != null)
            StopCoroutine(_enemyHealthCoroutine);

        _enemyHealthCoroutine = StartCoroutine(AnimateEnemyHealthBar());
    }

    private IEnumerator AnimateAvatarHealthBar()
    {
        yield return AnimateHealthBar(
            avatarHealthBar,
            _avatarCurrentHealth,
            _avatarMaxHealth);

        _avatarHealthCoroutine = null;
    }

    private IEnumerator AnimateEnemyHealthBar()
    {
        yield return AnimateHealthBar(
            enemyHealthBar,
            _enemyCurrentHealth,
            _enemyMaxHealth);

        _enemyHealthCoroutine = null;
    }

    private IEnumerator AnimateHealthBar(Image bar, float current, float max)
    {
        if (!bar)
            yield break;

        float targetFill = max <= 0f
            ? 0f
            : Mathf.Clamp01(current / max);

        float initialFill = bar.fillAmount;
        float elapsedTime = 0f;
        const float duration = 0.25f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f); // easeOutCubic
            bar.fillAmount = Mathf.Lerp(initialFill, targetFill, easedT);
            yield return null;
        }

        bar.fillAmount = targetFill;
    }

    private void StartHitFeedback(
        bool isEnemy,
        int damage,
        bool criticalHit,
        int criticalDamage,
        bool blockedHit,
        int blockedDamage,
        bool overtimeHit,
        bool finalHit)
    {
        if (!IsArenaVisible()) return;
        if (isEnemy)
        {
            if (_enemyHitSequenceCo != null)
                StopCoroutine(_enemyHitSequenceCo);

            _enemyHitSequenceCo = StartCoroutine(
                PlayHitFeedback(
                    isEnemy: true,
                    damage: damage,
                    criticalHit: criticalHit,
                    criticalDamage: criticalDamage,
                    blockedHit: blockedHit,
                    blockedDamage: blockedDamage,
                    overtimeHit: overtimeHit,
                    finalHit: finalHit));
        }
        else
        {
            if (_avatarHitSequenceCo != null)
                StopCoroutine(_avatarHitSequenceCo);

            _avatarHitSequenceCo = StartCoroutine(
                PlayHitFeedback(
                    isEnemy: false,
                    damage: damage,
                    criticalHit: criticalHit,
                    criticalDamage: criticalDamage,
                    blockedHit: blockedHit,
                    blockedDamage: blockedDamage,
                    overtimeHit: overtimeHit,
                    finalHit: finalHit));
        }
    }

    private IEnumerator PlayHitFeedback(
        bool isEnemy,
        int damage,
        bool criticalHit,
        int criticalDamage,
        bool blockedHit,
        int blockedDamage,
        bool overtimeHit,
        bool finalHit)
    {
        float longestEffectDuration = 0f;

        // Die Healthbar läuft bereits parallel. VFX dürfen das Health-Update nie blockieren.
        if (criticalHit)
        {
            float duration = PlayCriticalHitEffect(criticalDamage, isEnemy);
            longestEffectDuration = Mathf.Max(longestEffectDuration, duration);
        }

        if (blockedHit)
        {
            float duration = PlayBlockedHitEffect(blockedDamage, isEnemy);
            longestEffectDuration = Mathf.Max(longestEffectDuration, duration);
        }

        if (overtimeHit)
        {
            Debug.Log($"[Arena] Overtime attack against {(isEnemy ? "enemy" : "avatar")}: -{damage}");
        }

        if (finalHit)
        {
            Debug.Log($"[Arena] Final hit against {(isEnemy ? "enemy" : "avatar")}: -{damage}");
        }

        if (longestEffectDuration > 0f)
            yield return new WaitForSeconds(longestEffectDuration);
        else
            yield return null;

        if (isEnemy)
            _enemyHitSequenceCo = null;
        else
            _avatarHitSequenceCo = null;
    }

    // -------- VFX + Text --------
    private float PlayCriticalHitEffect(float bonusDamage, bool isEnemy)
    {
        float duration = 0.3f;

        ParticleSystem prefab = isEnemy ? enemyCriticalPrefab : avatarCriticalPrefab;
        if (prefab && mainCamera)
        {
            Vector3 worldPos = isEnemy ? GetEnemyVfxPos() : GetAvatarVfxPos();
            worldPos += mainCamera.transform.up * 0.1f;

            ParticleSystem instance = Instantiate(prefab, worldPos, Quaternion.identity);
            _activeHitEffects.Add(instance.gameObject);

            var mainModule = instance.main;
            instance.Play(true);

            duration = mainModule.duration;
            float destroyTime = mainModule.duration + mainModule.startLifetimeMultiplier + 0.5f;
            Destroy(instance.gameObject, destroyTime);
        }

        ShowCriticalHitPopup(isEnemy, bonusDamage);
        return duration;
    }

    private float PlayBlockedHitEffect(float blockedDamage, bool isEnemy)
    {
        float duration = 0.25f;

        ParticleSystem prefab = isEnemy ? enemyBlockedPrefab : avatarBlockedPrefab;
        if (prefab && mainCamera)
        {
            Vector3 worldPos = isEnemy ? GetEnemyVfxPos() : GetAvatarVfxPos();
            worldPos += mainCamera.transform.up * 0.05f;

            ParticleSystem instance = Instantiate(prefab, worldPos, Quaternion.identity);
            _activeHitEffects.Add(instance.gameObject);

            var mainModule = instance.main;
            instance.Play(true);

            duration = mainModule.duration;
            float destroyTime = mainModule.duration + mainModule.startLifetimeMultiplier + 0.5f;
            Destroy(instance.gameObject, destroyTime);
        }

        ShowBlockedHitPopup(isEnemy, blockedDamage);
        return duration;
    }

    private Vector3 GetPositionInFrontOfCamera(float distance)
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!mainCamera)
            return Vector3.zero;

        return mainCamera.transform.position + mainCamera.transform.forward * distance;
    }

    private Vector3 GetViewportWorldPos(float viewportX, float viewportY, float distanceFromCamera)
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!mainCamera)
            return Vector3.zero;

        var vp = new Vector3(viewportX, viewportY, distanceFromCamera);
        return mainCamera.ViewportToWorldPoint(vp);
    }

    private Vector3 GetAvatarVfxPos()
    {
        return GetViewportWorldPos(0.25f, 0.18f, 5f);
    }

    private Vector3 GetEnemyVfxPos()
    {
        return GetViewportWorldPos(0.75f, 0.18f, 5f);
    }

    private void ShowCriticalHitPopup(bool isEnemy, float bonusDamage)
    {
        string text = bonusDamage > 0f
            ? $"CRITICAL +{bonusDamage:0}"
            : "CRITICAL";

        if (isEnemy)
        {
            if (!enemyCriticalPopup || !enemyCriticalText) return;
            if (_enemyCritCo != null) StopCoroutine(_enemyCritCo);
            _enemyCritCo = StartCoroutine(ShowPopupCoroutine(enemyCriticalPopup, enemyCriticalText, text));
        }
        else
        {
            if (!avatarCriticalPopup || !avatarCriticalText) return;
            if (_avatarCritCo != null) StopCoroutine(_avatarCritCo);
            _avatarCritCo = StartCoroutine(ShowPopupCoroutine(avatarCriticalPopup, avatarCriticalText, text));
        }
    }

    private void ShowBlockedHitPopup(bool isEnemy, float blockedDamage)
    {
        string text = blockedDamage > 0f
            ? $"BLOCKED {blockedDamage:0}"
            : "BLOCKED";

        if (isEnemy)
        {
            if (!enemyBlockedPopup || !enemyBlockedText) return;
            if (_enemyBlockCo != null) StopCoroutine(_enemyBlockCo);
            _enemyBlockCo = StartCoroutine(ShowPopupCoroutine(enemyBlockedPopup, enemyBlockedText, text));
        }
        else
        {
            if (!avatarBlockedPopup || !avatarBlockedText) return;
            if (_avatarBlockCo != null) StopCoroutine(_avatarBlockCo);
            _avatarBlockCo = StartCoroutine(ShowPopupCoroutine(avatarBlockedPopup, avatarBlockedText, text));
        }
    }

    private IEnumerator ShowPopupCoroutine(GameObject panel, Text textComponent, string message)
    {
        panel.SetActive(true);
        textComponent.text = message;

        var tr = panel.transform;
        Vector3 startScale = Vector3.one * 0.6f;
        Vector3 endScale   = Vector3.one;
        float durationIn   = 0.15f;
        float durationStay = 0.5f;
        float durationOut  = 0.2f;

        tr.localScale = startScale;

        float t = 0f;
        while (t < durationIn)
        {
            t += Time.deltaTime;
            float lerpT = Mathf.Clamp01(t / durationIn);
            float eased = 1f - Mathf.Pow(1f - lerpT, 3f);
            tr.localScale = Vector3.Lerp(startScale, endScale, eased);
            yield return null;
        }

        tr.localScale = endScale;
        yield return new WaitForSeconds(durationStay);

        var cg = panel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            float startAlpha = 1f;
            float endAlpha = 0f;
            t = 0f;
            while (t < durationOut)
            {
                t += Time.deltaTime;
                float lerpT = Mathf.Clamp01(t / durationOut);
                cg.alpha = Mathf.Lerp(startAlpha, endAlpha, lerpT);
                yield return null;
            }
            cg.alpha = 1f;
        }

        panel.SetActive(false);
    }

    // -------- Navigation & Filter --------
    private List<AvatarInfo> PurchasedAvatars() =>
        _avatars.Where(a => a.isPurchased).OrderBy(a => a.name).ToList();

    private List<WeaponInfo> PurchasedWeapons() =>
        _weapons.Where(w => w.isPurchased).OrderBy(w => w.name).ToList();

    private List<EnemyInfo> AllEnemies() =>
        _enemies.OrderBy(e => e.name).ToList();

    private void ClampIndices()
    {
        var pav = PurchasedAvatars();
        var pwe = PurchasedWeapons();
        var pen = AllEnemies();

        _avatarIndex = pav.Count == 0 ? 0 : Mathf.Clamp(_avatarIndex, 0, pav.Count - 1);
        _weaponIndex = pwe.Count == 0 ? 0 : Mathf.Clamp(_weaponIndex, 0, pwe.Count - 1);
        _enemyIndex  = pen.Count == 0 ? 0 : Mathf.Clamp(_enemyIndex,  0, pen.Count - 1);
    }

    public void NextAvatar()
    {
        if (!CanChangeSelection()) return;
        var pav = PurchasedAvatars();
        if (pav.Count == 0) return;
        _avatarIndex = (_avatarIndex + 1) % pav.Count;
        PrepareSelectedCombatants();
    }

    public void PrevAvatar()
    {
        if (!CanChangeSelection()) return;
        var pav = PurchasedAvatars();
        if (pav.Count == 0) return;
        _avatarIndex = (_avatarIndex - 1 + pav.Count) % pav.Count;
        PrepareSelectedCombatants();
    }

    public void NextWeapon()
    {
        if (!CanChangeSelection()) return;
        var pwe = PurchasedWeapons();
        if (pwe.Count == 0) return;
        _weaponIndex = (_weaponIndex + 1) % pwe.Count;
        PrepareSelectedCombatants();
    }

    public void PrevWeapon()
    {
        if (!CanChangeSelection()) return;
        var pwe = PurchasedWeapons();
        if (pwe.Count == 0) return;
        _weaponIndex = (_weaponIndex - 1 + pwe.Count) % pwe.Count;
        PrepareSelectedCombatants();
    }

    public void NextEnemy()
    {
        if (!CanChangeSelection()) return;
        var pen = AllEnemies();
        if (pen.Count == 0) return;
        _enemyIndex = (_enemyIndex + 1) % pen.Count;
        PrepareSelectedCombatants();
    }

    public void PrevEnemy()
    {
        if (!CanChangeSelection()) return;
        var pen = AllEnemies();
        if (pen.Count == 0) return;
        _enemyIndex = (_enemyIndex - 1 + pen.Count) % pen.Count;
        PrepareSelectedCombatants();
    }

    // A user selection after a finished fight starts a fresh preview. General
    // UI refreshes and page changes must keep the current combat health intact.
    private void PrepareSelectedCombatants()
    {
        _phase = ArenaPhase.Prepare;
        HideArenaResult();
        StopHiddenHitFeedback();
        ResetSelectedHealth();
        RefreshUI();
    }

    private void ResetSelectedHealth()
    {
        if (_avatarHealthCoroutine != null)
            StopCoroutine(_avatarHealthCoroutine);
        if (_enemyHealthCoroutine != null)
            StopCoroutine(_enemyHealthCoroutine);
        _avatarHealthCoroutine = null;
        _enemyHealthCoroutine = null;

        var avatars = PurchasedAvatars();
        var enemies = AllEnemies();
        var avatar = avatars.Count > 0
            ? avatars[Mathf.Clamp(_avatarIndex, 0, avatars.Count - 1)] : null;
        var enemy = enemies.Count > 0
            ? enemies[Mathf.Clamp(_enemyIndex, 0, enemies.Count - 1)] : null;

        _avatarMaxHealth = avatar?.health ?? 0f;
        _avatarCurrentHealth = _avatarMaxHealth;
        _enemyMaxHealth = enemy?.health ?? 0f;
        _enemyCurrentHealth = _enemyMaxHealth;
    }

    private bool CanChangeSelection()
    {
        return _phase == ArenaPhase.Prepare || _phase == ArenaPhase.Finished;
    }

    // -------- UI Refresh --------
    private void RefreshUI()
    {
        var pav = PurchasedAvatars();
        var pwe = PurchasedWeapons();
        var pen = AllEnemies();

        AvatarInfo curAvatar = pav.Count > 0 ? pav[Mathf.Clamp(_avatarIndex, 0, pav.Count - 1)] : null;
        WeaponInfo curWeapon = pwe.Count > 0 ? pwe[Mathf.Clamp(_weaponIndex, 0, pwe.Count - 1)] : null;
        EnemyInfo  curEnemy  = pen.Count > 0 ? pen[Mathf.Clamp(_enemyIndex,  0, pen.Count - 1)] : null;

        if (avatarImage) avatarImage.sprite = curAvatar?.sprite ?? defaultAvatarSprite;
        if (weaponImage) weaponImage.sprite = curWeapon?.sprite ?? defaultWeaponSprite;
        if (enemyImage)  enemyImage.sprite  = curEnemy?.sprite  ?? defaultEnemySprite;

        int avatarStrength   = curAvatar?.strength ?? 0;
        int baseAvatarHealth = curAvatar?.health   ?? 0;
        int weaponStrength   = curWeapon?.strength ?? 0;
        int weaponDefence    = curWeapon?.defence  ?? 0;

        int enemyStrength    = curEnemy?.strength  ?? 0;
        int baseEnemyHealth  = curEnemy?.health    ?? 0;
        int enemyDefence     = curEnemy?.defence   ?? 0;

        int totalStrength = avatarStrength + weaponStrength;
        if (enemyStateIcon)
        {
            if (curEnemy == null)
            {
                enemyStateIcon.enabled = false;
            }
            else
            {
                enemyStateIcon.enabled = true;

                if (curEnemy.isDefeated && enemyDefeatedStateSprite)
                    enemyStateIcon.sprite = enemyDefeatedStateSprite;
                else if (!curEnemy.isDefeated && enemyAliveStateSprite)
                    enemyStateIcon.sprite = enemyAliveStateSprite;
                else
                    enemyStateIcon.enabled = false;
            }
        }

        if (_phase == ArenaPhase.Prepare || _avatarMaxHealth <= 0f)
        {
            _avatarMaxHealth     = baseAvatarHealth;
            _avatarCurrentHealth = _avatarMaxHealth;
        }

        if (_phase == ArenaPhase.Prepare || _enemyMaxHealth <= 0f)
        {
            _enemyMaxHealth     = baseEnemyHealth;
            _enemyCurrentHealth = _enemyMaxHealth;
        }

        if (strengthText)      strengthText.text      = $"Strength: {totalStrength}";
        if (defenceText)       defenceText.text       = $"Defence: {weaponDefence}";
        if (healthText)        healthText.text        = $"Health: {_avatarCurrentHealth:F0}";
        if (enemyStrengthText) enemyStrengthText.text = $"Strength: {enemyStrength}";
        if (enemyDefenceText)  enemyDefenceText.text  = $"Defence: {enemyDefence}";
        if (enemyHealthText)   enemyHealthText.text   = $"Health: {_enemyCurrentHealth:F0}";

        // Eine laufende Healthbar-Animation darf durch allgemeine UI-Refreshes
        // nicht direkt auf ihren Zielwert gesetzt werden.
        if (avatarHealthBar && _avatarHealthCoroutine == null)
        {
            float fill = (_avatarMaxHealth <= 0f)
                ? 0f
                : Mathf.Clamp01(_avatarCurrentHealth / _avatarMaxHealth);
            avatarHealthBar.fillAmount = fill;
        }

        if (enemyHealthBar && _enemyHealthCoroutine == null)
        {
            float fill = (_enemyMaxHealth <= 0f)
                ? 0f
                : Mathf.Clamp01(_enemyCurrentHealth / _enemyMaxHealth);
            enemyHealthBar.fillAmount = fill;
        }

        bool canChange = CanChangeSelection();
        if (avatarPrevButton) avatarPrevButton.interactable = canChange && pav.Count > 1;
        if (avatarNextButton) avatarNextButton.interactable = canChange && pav.Count > 1;
        if (weaponPrevButton) weaponPrevButton.interactable = canChange && pwe.Count > 1;
        if (weaponNextButton) weaponNextButton.interactable = canChange && pwe.Count > 1;
        if (enemyPrevButton)  enemyPrevButton.interactable  = canChange && pen.Count > 1;
        if (enemyNextButton)  enemyNextButton.interactable  = canChange && pen.Count > 1;
    }

    // -------- Models --------
    [Serializable]
    private sealed class AvatarInfo
    {
        public string name;
        public Sprite sprite;
        public int strength;
        public int health;
        public bool isPurchased;
    }

    [Serializable]
    private sealed class WeaponInfo
    {
        public string name;
        public Sprite sprite;
        public int strength;
        public int defence;
        public bool isPurchased;
    }

    [Serializable]
    private sealed class EnemyInfo
    {
        public string name;
        public Sprite sprite;
        public int strength;
        public int health;
        public int defence;
        public int tier;
        public bool isDefeated;
    }
}