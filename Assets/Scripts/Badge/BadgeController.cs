using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Rocworks.Mqtt;

public class BadgeController : MonoBehaviour, IObserver
{
    [SerializeField] private Image[] badgeImages; // Assign in the Inspector
    [SerializeField] private Image overlayImage; // Halbtransparentes Overlay
    [SerializeField] private GameObject successGameObject; // Erfolgselement
    [SerializeField] private GameObject failGameObject; // Misserfolgselement

    private MsgHandler msgHandler;
    private bool[] badgeUnlocked;

    void Awake()
    {
        badgeUnlocked = new bool[badgeImages.Length];
        if (successGameObject != null)
            successGameObject.SetActive(false);
        if (failGameObject != null)
            failGameObject.SetActive(false);
        if (overlayImage != null)
        overlayImage.gameObject.SetActive(false);
    }

    void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("UserDataUpdate/Badges", this);
            msgHandler.RegisterObserver("UserResultEvent/Challenge", this);
        }
    }

    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver("UserDataUpdate/Badges", this);
            msgHandler.RemoveObserver("UserResultEvent/Challenge", this);
        }
    }

    public void OnNotify(string eventType, object data)
    {
        if (eventType == "UserDataUpdate/Badges")
        {
            string badgesJson = (string)data;
            JArray badges = JArray.Parse(badgesJson);

            if (badges.Count == 0)
            {
                Debug.Log("Empty badge array received. Resetting all badges to locked.");
                ResetAllBadges();
            }
            else
            {
                UpdateBadges(badges);
            }
        }

        if (eventType == "UserResultEvent/Challenge")
        {
            JObject challengeResult = JObject.Parse((string)data);
            HandleChallengeResult(challengeResult);
        }
    }

    private void UpdateBadges(JArray badges)
    {
        ResetAllBadges();

        foreach (var badge in badges)
        {
            int level = (int)badge["level"];
            int difficulty = (int)badge["difficulty"];
            bool isUnlocked = (bool)badge["is_unlocked"];

            int index = GetBadgeIndex(level, difficulty-1);

            if (index >= 0 && index < badgeImages.Length)
            {
                badgeImages[index].enabled = !isUnlocked;
                badgeUnlocked[index] = isUnlocked; // Status im Array speichern
            }
            else
            {
                Debug.LogWarning($"Invalid badge index for level {level}, difficulty {difficulty}.");
            }
        }

        NotifyBadgeUpdate();
    }

    private int GetBadgeIndex(int level, int difficulty)
    {
        int badgesPerLevel = 7; // Assuming each level has 7 badges
        return (level - 1) * badgesPerLevel + (difficulty);
    }

    private void NotifyBadgeUpdate()
    {
        Debug.Log("Badges updated");
        msgHandler.NotifyObservers("BadgesUpdated", null);
    }

    private void ResetAllBadges()
    {
        foreach (var img in badgeImages)
        {
            img.enabled = true;
        }
    }

    private void HandleChallengeResult(JObject result)
    {
        bool success = result["result"]?.ToString() == "success";
        int level = result["level"]?.ToObject<int>() ?? 0;
        int difficultyIndex = GetDifficultyIndex(result["difficulty"]?.ToString() ?? "");
        int index = GetBadgeIndex(level, difficultyIndex);
        if (success)
        {
            if (!badgeUnlocked[index]) // Animation nur, wenn Badge noch nicht freigeschaltet
            {
                badgeUnlocked[index] = true; // Als freigeschaltet markieren
                UnlockBadgeWithoutAnimation(level, difficultyIndex);
                StartCoroutine(PlaySuccessAnimation());
            }
            else
            {
                Debug.Log($"Badge for level {level}, difficulty {difficultyIndex} already unlocked. Skipping animation.");
            }
        }
        else
        {
            StartCoroutine(PlayFailureAnimation());
        }
    }

    private IEnumerator PlaySuccessAnimation()
    {
        yield return StartCoroutine(FadeInOverlay());

        // Erfolgselement skalieren und anzeigen
        yield return StartCoroutine(ScaleGameObject(successGameObject));

        // Nach einer kurzen Wartezeit das Erfolgselement ausblenden
        successGameObject.SetActive(false);

        yield return StartCoroutine(FadeOutOverlay());
    }

    private IEnumerator PlayFailureAnimation()
    {
        yield return StartCoroutine(FadeInOverlay());

        // Misserfolgselement skalieren und anzeigen
        yield return StartCoroutine(ScaleGameObject(failGameObject));

        // Nach einer kurzen Wartezeit das Misserfolgselement ausblenden
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
            color.a = Mathf.Lerp(0f, 0.8f, elapsedTime / 0.5f); // Auf 80% Deckkraft
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
            color.a = Mathf.Lerp(0.8f, 0f, elapsedTime / 0.5f); // Auf 0% Deckkraft
            overlayImage.color = color;
            yield return null;
        }
        overlayImage.gameObject.SetActive(false); 
    }

    private IEnumerator ScaleGameObject(GameObject obj)
    {
        obj.SetActive(true);
        Vector3 initialScale = new Vector3(0.1f, 0.1f, 0.1f);
        Vector3 targetScale = new Vector3(1.5f, 1.5f, 1.5f);

        float elapsedTime = 0f;
        while (elapsedTime < 0.5f) // 0.5 Sekunden für das Skalieren
        {
            elapsedTime += Time.deltaTime;
            obj.transform.localScale = Vector3.Lerp(initialScale, targetScale, elapsedTime / 0.5f);
            yield return null;
        }

        // Verweile kurz bei maximaler Skalierung
        yield return new WaitForSeconds(2.5f);

        elapsedTime = 0f;
        while (elapsedTime < 0.5f) // 0.5 Sekunden für das Zurückskalieren
        {
            elapsedTime += Time.deltaTime;
            obj.transform.localScale = Vector3.Lerp(targetScale, initialScale, elapsedTime / 0.5f);
            yield return null;
        }
    }
       private int GetDifficultyIndex(string difficulty)
    {
        string[] difficultyLevels = {
            "Very Easy", "Easy", "Medium", "Hard", "Very Hard", "Extreme", "Insane"
        };

        for (int i = 0; i < difficultyLevels.Length; i++)
        {
            if (difficultyLevels[i] == difficulty)
                return i;
        }

        Debug.LogWarning($"Unknown difficulty: {difficulty}");
        return -1;
    }
        private void UnlockBadgeWithoutAnimation(int level, int difficulty)
    {
        int index = GetBadgeIndex(level, difficulty);

        if (index >= 0 && index < badgeImages.Length)
        {
            badgeImages[index].enabled = false;
            //badgeAnimator?.SetTrigger(unlockAnimationName);
        }
        else
        {
            Debug.LogWarning($"Invalid badge index for level {level}, difficulty {difficulty}.");
        }
    }
}
