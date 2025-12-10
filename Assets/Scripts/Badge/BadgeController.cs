using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class BadgeController : MonoBehaviour, IObserver
{
    [Header("Badge UI")]
    [SerializeField] private Image[] badgeImages;          // jede Badge-„Fliese“
    [SerializeField] private Image overlayImage;           // halbtransparentes Overlay
    [SerializeField] private GameObject successGameObject; // Erfolgselement
    [SerializeField] private GameObject failGameObject;    // Misserfolgselement

    [Header("MQTT Topic Settings")]
    [Tooltip("Group für Badges, z.B. USERDATA -> .../GAMING/USERDATA/BADGES")]
    [SerializeField] private string badgesGroup = "USERDATA";

    [Tooltip("Kategorie für Badges innerhalb der Group.")]
    [SerializeField] private string badgesCategory = "BADGES";

    [Tooltip("Group für Challenge-Resultate, z.B. USERRESULT -> .../GAMING/USERRESULT/CHALLENGE")]
    [SerializeField] private string challengeGroup = "USERRESULT";

    [Tooltip("Kategorie für Challenge-Resultate.")]
    [SerializeField] private string challengeCategory = "CHALLENGE";

    private MsgHandler msgHandler;
    private bool[] badgeUnlocked;

    private string _topicBadges;
    private string _topicChallenge;

    void Awake()
    {
        badgeUnlocked = new bool[badgeImages.Length];

        if (successGameObject != null)
            successGameObject.SetActive(false);

        if (failGameObject != null)
            failGameObject.SetActive(false);

        if (overlayImage != null)
            overlayImage.gameObject.SetActive(false);

        msgHandler = FindObjectOfType<MsgHandler>();
    }

    void OnEnable()
    {
        if (msgHandler == null)
        {
            Debug.LogError("[BadgeController] MsgHandler not found in scene.");
            return;
        }

        _topicBadges    = TopicNamespace.TopicFor(badgesGroup,    badgesCategory);
        _topicChallenge = TopicNamespace.TopicFor(challengeGroup, challengeCategory);

        msgHandler.RegisterObserver(_topicBadges,    this);
        msgHandler.RegisterObserver(_topicChallenge, this);

        Debug.Log($"[BadgeController] Registered for topics: {_topicBadges}, {_topicChallenge}");
    }

    void OnDisable()
    {
        if (msgHandler != null)
        {
            if (!string.IsNullOrEmpty(_topicBadges))
                msgHandler.RemoveObserver(_topicBadges, this);

            if (!string.IsNullOrEmpty(_topicChallenge))
                msgHandler.RemoveObserver(_topicChallenge, this);
        }
    }

    // ---- IObserver ----
    public void OnNotify(string eventType, object data)
    {
        if (data is not string s || string.IsNullOrWhiteSpace(s))
            return;

        if (eventType == _topicBadges)
        {
            // Erwartet: JSON-Array mit Badge-Objekten
            // [
            //   { "level": 1, "difficulty": 1..7, "is_unlocked": true/false },
            //   ...
            // ]
            JArray badges = JArray.Parse(s);

            if (badges.Count == 0)
            {
                Debug.Log("[BadgeController] Empty badge array received. Resetting all badges to locked.");
                ResetAllBadges();
            }
            else
            {
                UpdateBadges(badges);
            }
        }
        else if (eventType == _topicChallenge)
        {
            // Erwartet:
            // {
            //   "result": "success" | "fail" | ...,
            //   "level": 1,
            //   "difficulty": "Very Easy" | "Easy" | ... | "Insane"
            // }
            JObject challengeResult = JObject.Parse(s);
            HandleChallengeResult(challengeResult);
        }
    }

    // ---- Badges aktualisieren ----
    private void UpdateBadges(JArray badges)
    {
        ResetAllBadges();

        foreach (var badge in badges)
        {
            int level       = (int)badge["level"];
            int difficulty  = (int)badge["difficulty"];      // 1..7
            bool isUnlocked = (bool)badge["is_unlocked"];

            int index = GetBadgeIndex(level, difficulty - 1);

            if (index >= 0 && index < badgeImages.Length)
            {
                // Wenn unlocked -> Bild ausblenden (oder umgekehrt, je nach Design)
                badgeImages[index].enabled = !isUnlocked;
                badgeUnlocked[index]       = isUnlocked;
            }
            else
            {
                Debug.LogWarning($"[BadgeController] Invalid badge index for level {level}, difficulty {difficulty}.");
            }
        }

        NotifyBadgeUpdate();
    }

    private int GetBadgeIndex(int level, int difficultyIndex)
    {
        int badgesPerLevel = 7; // 7 Schwierigkeitsgrade
        return (level - 1) * badgesPerLevel + difficultyIndex;
    }

    private void ResetAllBadges()
    {
        for (int i = 0; i < badgeImages.Length; i++)
        {
            if (badgeImages[i])
                badgeImages[i].enabled = true;

            badgeUnlocked[i] = false;
        }
    }

    private void NotifyBadgeUpdate()
    {
        Debug.Log("[BadgeController] Badges updated");
        // internes Event innerhalb des MsgHandlers (kein MQTT)
        msgHandler?.NotifyObservers("BadgesUpdated", null);
    }

    // ---- Challenge-Resultate / Animation ----
    private void HandleChallengeResult(JObject result)
    {
        bool success = result["result"]?.ToString() == "success";
        int level    = result["level"]?.ToObject<int>() ?? 0;
        int diffIdx  = GetDifficultyIndex(result["difficulty"]?.ToString() ?? "");

        int index = GetBadgeIndex(level, diffIdx);

        if (index < 0 || index >= badgeImages.Length)
        {
            Debug.LogWarning($"[BadgeController] Invalid badge index in challenge result: level {level}, diffIdx {diffIdx}.");
            return;
        }

        if (success)
        {
            if (!badgeUnlocked[index])
            {
                badgeUnlocked[index] = true;
                UnlockBadgeWithoutAnimation(level, diffIdx);
                if (overlayImage && successGameObject)
                    StartCoroutine(PlaySuccessAnimation());
            }
            else
            {
                Debug.Log($"[BadgeController] Badge for level {level}, difficulty {diffIdx} already unlocked. Skipping animation.");
            }
        }
        else
        {
            if (overlayImage && failGameObject)
                StartCoroutine(PlayFailureAnimation());
        }
    }

    private IEnumerator PlaySuccessAnimation()
    {
        yield return StartCoroutine(FadeInOverlay());
        yield return StartCoroutine(ScaleGameObject(successGameObject));
        successGameObject.SetActive(false);
        yield return StartCoroutine(FadeOutOverlay());
    }

    private IEnumerator PlayFailureAnimation()
    {
        yield return StartCoroutine(FadeInOverlay());
        yield return StartCoroutine(ScaleGameObject(failGameObject));
        failGameObject.SetActive(false);
        yield return StartCoroutine(FadeOutOverlay());
    }

    private IEnumerator FadeInOverlay()
    {
        overlayImage.gameObject.SetActive(true);
        float elapsedTime = 0f;
        Color color = overlayImage.color;

        while (elapsedTime < 0.5f)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Lerp(0f, 0.8f, elapsedTime / 0.5f);
            overlayImage.color = color;
            yield return null;
        }
    }

    private IEnumerator FadeOutOverlay()
    {
        float elapsedTime = 0f;
        Color color = overlayImage.color;

        while (elapsedTime < 0.5f)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Lerp(0.8f, 0f, elapsedTime / 0.5f);
            overlayImage.color = color;
            yield return null;
        }

        overlayImage.gameObject.SetActive(false);
    }

    private IEnumerator ScaleGameObject(GameObject obj)
    {
        obj.SetActive(true);
        Vector3 initialScale = new Vector3(0.1f, 0.1f, 0.1f);
        Vector3 targetScale  = new Vector3(1.5f, 1.5f, 1.5f);

        float elapsedTime = 0f;
        while (elapsedTime < 0.5f)
        {
            elapsedTime += Time.deltaTime;
            obj.transform.localScale = Vector3.Lerp(initialScale, targetScale, elapsedTime / 0.5f);
            yield return null;
        }

        yield return new WaitForSeconds(2.5f);

        elapsedTime = 0f;
        while (elapsedTime < 0.5f)
        {
            elapsedTime += Time.deltaTime;
            obj.transform.localScale = Vector3.Lerp(targetScale, initialScale, elapsedTime / 0.5f);
            yield return null;
        }
    }

    private int GetDifficultyIndex(string difficulty)
    {
        string[] difficultyLevels =
        {
            "Very Easy", "Easy", "Medium", "Hard", "Very Hard", "Extreme", "Insane"
        };

        for (int i = 0; i < difficultyLevels.Length; i++)
        {
            if (difficultyLevels[i] == difficulty)
                return i;
        }

        Debug.LogWarning($"[BadgeController] Unknown difficulty: {difficulty}");
        return -1;
    }

    private void UnlockBadgeWithoutAnimation(int level, int difficultyIndex)
    {
        int index = GetBadgeIndex(level, difficultyIndex);

        if (index >= 0 && index < badgeImages.Length)
        {
            badgeImages[index].enabled = false;
        }
        else
        {
            Debug.LogWarning($"[BadgeController] Invalid badge index for level {level}, difficulty {difficultyIndex}.");
        }
    }
    [ContextMenu("Test Last Badge")]
    void TestLastBadge()
    {
        int idx = GetBadgeIndex(8, 6);   // Level 8, difficultyIndex 6 (= difficulty 7)
        Debug.Log($"TestLastBadge -> index {idx}");

        if (idx >= 0 && idx < badgeImages.Length && badgeImages[idx] != null)
        {
            badgeImages[idx].enabled = !badgeImages[idx].enabled;
        }
        else
        {
            Debug.LogWarning($"Badge at index {idx} is out of range or null.");
        }
    }
}
