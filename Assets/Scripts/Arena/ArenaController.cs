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

    private Coroutine _avatarHitSequenceCo;
    private Coroutine _enemyHitSequenceCo;

    private Coroutine _avatarCritCo, _avatarBlockCo, _enemyCritCo, _enemyBlockCo;

    private enum ArenaPhase { Prepare, Fight }

    // -------- Unity --------
    private void Awake()
    {
        if (!msgHandler) msgHandler = FindObjectOfType<MsgHandler>();
        if (!mainCamera) mainCamera = Camera.main;

        WireButtons();
        LoadSpritesFromDisk();

        if (avatarCriticalPopup) avatarCriticalPopup.SetActive(false);
        if (avatarBlockedPopup)  avatarBlockedPopup.SetActive(false);
        if (enemyCriticalPopup)  enemyCriticalPopup.SetActive(false);
        if (enemyBlockedPopup)   enemyBlockedPopup.SetActive(false);
    }

    private void Start()
    {
        RegisterObservers();
        RefreshUI();
    }

    private void OnDestroy()
    {
        UnregisterObservers();
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
            e.defence  = it["defence"]?.Value<int>()  ?? e.defence;
            e.tier     = it["tier"]?.Value<int>()     ?? e.tier;
            e.isDefeated = it["isDefeated"]?.Value<bool>() ?? e.isDefeated;
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
        var phaseToken = jo["phase"];
        bool changedToFight = false; // NEW: merken, ob wir jetzt in FIGHT wechseln

        if (phaseToken != null)
        {
            var raw = phaseToken.ToString();
            var phaseStr = raw.Trim().ToUpperInvariant();
            var oldPhase = _phase;

            _phase = phaseStr switch
            {
                "FIGHT"   => ArenaPhase.Fight,
                "PREPARE" => ArenaPhase.Prepare,
                _         => _phase
            };

            changedToFight = (oldPhase != _phase && _phase == ArenaPhase.Fight); // NEW

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

        if (_phase == ArenaPhase.Prepare)
        {
            var pav = PurchasedAvatars();
            var curAv = pav.Count > 0 ? pav[Mathf.Clamp(_avatarIndex, 0, pav.Count - 1)] : null;
            _avatarMaxHealth     = curAv?.health ?? 0f;
            _avatarCurrentHealth = _avatarMaxHealth;

            var pen = AllEnemies();
            var curEn = pen.Count > 0 ? pen[Mathf.Clamp(_enemyIndex, 0, pen.Count - 1)] : null;
            _enemyMaxHealth      = curEn?.health ?? 0f;
            _enemyCurrentHealth  = _enemyMaxHealth;
        }

        // NEW: Sobald wir vom Backend in den Fight-/Freeze-State gesetzt werden,
        // schicken wir die aktuelle Auswahl (Avatar, Weapon, Enemy) zurück.
        if (changedToFight)
        {
            PublishCurrentSelection();
        }
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

        string target       = jo["target"]?.ToString()?.ToUpperInvariant();
        float currentHealth = jo["current_health"]?.Value<float>() ?? 0f;
        float maxHealth     = jo["max_health"]?.Value<float>()     ?? 0f;
        float critical      = jo["critical_hit"]?.Value<float>()   ?? 0f;
        float blocked       = jo["blocked_hit"]?.Value<float>()    ?? 0f;

        currentHealth = Mathf.Max(0f, currentHealth);
        bool isEnemy = target == "ENEMY";

        if (isEnemy)
        {
            if (maxHealth > 0f) _enemyMaxHealth = maxHealth;
            if (_enemyMaxHealth <= 0f) _enemyMaxHealth = currentHealth;

            _enemyCurrentHealth = Mathf.Min(currentHealth, _enemyMaxHealth);

            if (_enemyHitSequenceCo != null) StopCoroutine(_enemyHitSequenceCo);
            _enemyHitSequenceCo = StartCoroutine(PlayHitSequence(isEnemy: true, critical: critical, blocked: blocked));
        }
        else
        {
            if (maxHealth > 0f) _avatarMaxHealth = maxHealth;
            if (_avatarMaxHealth <= 0f) _avatarMaxHealth = currentHealth;

            _avatarCurrentHealth = Mathf.Min(currentHealth, _avatarMaxHealth);

            if (_avatarHitSequenceCo != null) StopCoroutine(_avatarHitSequenceCo);
            _avatarHitSequenceCo = StartCoroutine(PlayHitSequence(isEnemy: false, critical: critical, blocked: blocked));
        }
    }

    private IEnumerator AnimateHealthBar(Image bar, float current, float max)
    {
        if (!bar) yield break;

        float targetFill = (max <= 0f) ? 0f : Mathf.Clamp01(current / max);
        float initialFill = bar.fillAmount;
        float elapsedTime = 0f;
        float duration = 0.25f;

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

    private IEnumerator PlayHitSequence(bool isEnemy, float critical, float blocked)
    {
        if (critical > 0f)
        {
            float critDuration = PlayCriticalHitEffect(critical, isEnemy);
            if (critDuration > 0f)
                yield return new WaitForSeconds(critDuration);
        }

        if (blocked > 0f)
        {
            float blockDuration = PlayBlockedHitEffect(blocked, isEnemy);
            if (blockDuration > 0f)
                yield return new WaitForSeconds(blockDuration);
        }

        if (isEnemy)
        {
            if (enemyHealthBar)
            {
                if (_enemyHealthCoroutine != null) StopCoroutine(_enemyHealthCoroutine);
                _enemyHealthCoroutine = StartCoroutine(
                    AnimateHealthBar(enemyHealthBar, _enemyCurrentHealth, _enemyMaxHealth));
            }
        }
        else
        {
            if (avatarHealthBar)
            {
                if (_avatarHealthCoroutine != null) StopCoroutine(_avatarHealthCoroutine);
                _avatarHealthCoroutine = StartCoroutine(
                    AnimateHealthBar(avatarHealthBar, _avatarCurrentHealth, _avatarMaxHealth));
            }
        }

        RefreshUI();
    }

    // -------- VFX + Text --------
    private float PlayCriticalHitEffect(float impact, bool isEnemy)
    {
        float duration = 0.3f;

        ParticleSystem prefab = isEnemy ? enemyCriticalPrefab : avatarCriticalPrefab;
        if (prefab && mainCamera)
        {
            Vector3 worldPos = isEnemy ? GetEnemyVfxPos() : GetAvatarVfxPos();
            worldPos += mainCamera.transform.up * 0.1f;

            ParticleSystem instance = Instantiate(prefab, worldPos, Quaternion.identity);

            var mainModule = instance.main;
            instance.Play(true);

            duration = mainModule.duration;
            float destroyTime = mainModule.duration + mainModule.startLifetimeMultiplier + 0.5f;
            Destroy(instance.gameObject, destroyTime);
        }

        ShowCriticalHitPopup(isEnemy, impact);
        return duration;
    }

    private float PlayBlockedHitEffect(float impact, bool isEnemy)
    {
        float duration = 0.25f;

        ParticleSystem prefab = isEnemy ? enemyBlockedPrefab : avatarBlockedPrefab;
        if (prefab && mainCamera)
        {
            Vector3 worldPos = isEnemy ? GetEnemyVfxPos() : GetAvatarVfxPos();
            worldPos += mainCamera.transform.up * 0.05f;

            ParticleSystem instance = Instantiate(prefab, worldPos, Quaternion.identity);

            var mainModule = instance.main;
            instance.Play(true);

            duration = mainModule.duration;
            float destroyTime = mainModule.duration + mainModule.startLifetimeMultiplier + 0.5f;
            Destroy(instance.gameObject, destroyTime);
        }

        ShowBlockedHitPopup(isEnemy, impact);
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

    private void ShowCriticalHitPopup(bool isEnemy, float impact)
    {
        float factor = 1f + impact;
        string text = $"CRITICAL x{factor:0.0}";

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

    private void ShowBlockedHitPopup(bool isEnemy, float impact)
    {
        string text;
        if (impact > 0.75f)      text = "FULL BLOCK";
        else if (impact > 0.4f)  text = "GOOD BLOCK";
        else                     text = "BLOCKED";

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
        if (_phase != ArenaPhase.Prepare) return;
        var pav = PurchasedAvatars();
        if (pav.Count == 0) return;
        _avatarIndex = (_avatarIndex + 1) % pav.Count;
        RefreshUI();
    }

    public void PrevAvatar()
    {
        if (_phase != ArenaPhase.Prepare) return;
        var pav = PurchasedAvatars();
        if (pav.Count == 0) return;
        _avatarIndex = (_avatarIndex - 1 + pav.Count) % pav.Count;
        RefreshUI();
    }

    public void NextWeapon()
    {
        if (_phase != ArenaPhase.Prepare) return;
        var pwe = PurchasedWeapons();
        if (pwe.Count == 0) return;
        _weaponIndex = (_weaponIndex + 1) % pwe.Count;
        RefreshUI();
    }

    public void PrevWeapon()
    {
        if (_phase != ArenaPhase.Prepare) return;
        var pwe = PurchasedWeapons();
        if (pwe.Count == 0) return;
        _weaponIndex = (_weaponIndex - 1 + pwe.Count) % pwe.Count;
        RefreshUI();
    }

    public void NextEnemy()
    {
        if (_phase != ArenaPhase.Prepare) return;
        var pen = AllEnemies();
        if (pen.Count == 0) return;
        _enemyIndex = (_enemyIndex + 1) % pen.Count;
        RefreshUI();
    }

    public void PrevEnemy()
    {
        if (_phase != ArenaPhase.Prepare) return;
        var pen = AllEnemies();
        if (pen.Count == 0) return;
        _enemyIndex = (_enemyIndex - 1 + pen.Count) % pen.Count;
        RefreshUI();
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
        if (enemyStrengthText) enemyStrengthText.text = $"Enemy STR: {enemyStrength}";
        if (enemyDefenceText)  enemyDefenceText.text  = $"Enemy DEF: {enemyDefence}";
        if (enemyHealthText)   enemyHealthText.text   = $"Enemy HP: {_enemyCurrentHealth:F0}";

        if (avatarHealthBar)
        {
            float fill = (_avatarMaxHealth <= 0f) ? 0f : Mathf.Clamp01(_avatarCurrentHealth / _avatarMaxHealth);
            avatarHealthBar.fillAmount = fill;
        }

        if (enemyHealthBar)
        {
            float fill = (_enemyMaxHealth <= 0f) ? 0f : Mathf.Clamp01(_enemyCurrentHealth / _enemyMaxHealth);
            enemyHealthBar.fillAmount = fill;
        }

        bool canChange = _phase == ArenaPhase.Prepare;
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
