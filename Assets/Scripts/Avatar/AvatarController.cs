using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using Rocworks.Mqtt;
using System;

public class AvatarController : MonoBehaviour, IObserver
{
    [SerializeField] private Image avatarImage;    // Display avatar image
    [SerializeField] private Text costText;        // Display cost of the avatar
    [SerializeField] private GameObject lockIcon;  // Lock icon if avatar is locked
    [SerializeField] private Button nextButton;    // Navigate to next avatar
    [SerializeField] private Button previousButton;// Navigate to previous avatar
    [SerializeField] private Button unlockButton;  // Button to unlock avatar
    [SerializeField] private GameObject shadeOverlay; // Dim avatar if locked
    [SerializeField] private GameObject lockedButton;  // Button shown when locked

    private MsgHandler msgHandler;
    private List<AvatarInfo> avatars = new List<AvatarInfo>(); // List of all avatars
    private int currentAvatarIndex = 0;    // Current avatar index
    private Sprite defaultSprite;
    private int userCoins;
    private Coroutine publishCoroutine;

    private void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();
        if (msgHandler != null)
        {
            msgHandler.RegisterObserver("UserDataUpdate/Avatars", this);
            msgHandler.RegisterObserver("UserDataUpdate/Coins", this);
        }

        LoadAvatars();  // Load avatars from disk
        UpdateUI();     // Update the UI with the first avatar
    }

    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            msgHandler.RemoveObserver("UserDataUpdate/Avatars", this);
            msgHandler.RemoveObserver("UserDataUpdate/Coins", this);
        }
    }

 public void OnNotify(string eventType, object data)
{
    if (eventType == "UserDataUpdate/Coins")
    {
        if (data is string jsonString)
        {
            try
            {
                JObject json = JObject.Parse(jsonString);
                userCoins = json["coins"]?.ToObject<int>() ?? 0; // Extrahiere den Coins-Wert
                UpdateUI();  // Coins geändert, aktualisiere die UI
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing coins data: {ex.Message}");
            }
        }
    }
    else if (eventType == "UserDataUpdate/Avatars")
    {
        string jsonString = data as string;
        if (jsonString != null)
        {
            LoadAvatarStatusFromJson(jsonString);
            UpdateUI();
        }
    }
}

    private void LoadAvatars()
    {
        string avatarsPath = Path.Combine(Application.streamingAssetsPath, "Avatars");
        if (Directory.Exists(avatarsPath))
        {
            string[] costFolders = Directory.GetDirectories(avatarsPath);
            foreach (var folder in costFolders)
            {
                int cost;
                string folderName = Path.GetFileName(folder);
                if (folderName.StartsWith("Cost_") && int.TryParse(folderName.Substring(5), out cost))
                {
                    string[] files = Directory.GetFiles(folder);
                    int index = 1;
                    foreach (var file in files)
                    {
                        string extension = Path.GetExtension(file).ToLower();
                        if (IsValidImageFile(extension))
                        {
                            string spriteName = $"{cost}_{index}";
                            AvatarInfo avatarInfo = new AvatarInfo
                            {
                                cost = cost,
                                sprite = LoadAvatarSprite(file),
                                name = spriteName
                            };
                            avatars.Add(avatarInfo);
                            index++;
                        }
                    }
                }
            }
        }
        else
        {
            Debug.LogError($"Avatars directory not found at: {avatarsPath}");
        }
    }

    private bool IsValidImageFile(string extension)
    {
        return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".sprite";
    }

    private Sprite LoadAvatarSprite(string path)
    {
        if (!File.Exists(path))
        {
            return defaultSprite;
        }

        byte[] fileData = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(fileData);
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private void LoadAvatarStatusFromJson(string jsonString)
    {
        JArray responseArray = JArray.Parse(jsonString);

        foreach (var item in responseArray)
        {
            string avatarName = item["name"].ToString();
            bool isUnlocked = (bool)item["is_unlocked"];
            bool isPurchased = (bool)item["is_purchased"];

            var avatar = avatars.FirstOrDefault(a => a.name == avatarName);
            if (avatar != null)
            {
                avatar.isUnlocked = isUnlocked;
                avatar.isPurchased = isPurchased;
            }
        }

        // Set the active avatar after processing
        string activeAvatar = responseArray.FirstOrDefault()?["active_avatar"]?.ToString();
        SetActiveAvatar(activeAvatar);
        UpdateUI();
    }

    private void SetActiveAvatar(string activeAvatar)
    {
        var activeAv = avatars.FirstOrDefault(a => a.name == activeAvatar);
        if (activeAv != null)
        {
            currentAvatarIndex = avatars.IndexOf(activeAv);
        }
        else
        {
            currentAvatarIndex = avatars.IndexOf(avatars.FirstOrDefault(a => a.name == "10_1"));
        }
    }

    public void NextAvatar()
    {
        currentAvatarIndex = (currentAvatarIndex + 1) % avatars.Count;
        UpdateUI();
        Debug.Log($"Switched to next avatar: {avatars[currentAvatarIndex].sprite.name}");
         // Restart the publish coroutine if avatar is purchased
        RestartPublishCoroutine();
    }

    public void PreviousAvatar()
    {
        currentAvatarIndex = (currentAvatarIndex - 1 + avatars.Count) % avatars.Count;
        UpdateUI();
        Debug.Log($"Switched to previous avatar: {avatars[currentAvatarIndex].sprite.name}");
         // Restart the publish coroutine if avatar is purchased
        RestartPublishCoroutine();
    }

    private void UpdateUI()
    {
        AvatarInfo currentAvatar = avatars[currentAvatarIndex];
        avatarImage.sprite = currentAvatar.sprite != null ? currentAvatar.sprite : defaultSprite;

        if (!currentAvatar.isUnlocked)
        {
            lockIcon.SetActive(true);
            unlockButton.gameObject.SetActive(false);
            shadeOverlay.SetActive(true);
            lockedButton.SetActive(true);
        }
        else if (currentAvatar.isUnlocked && !currentAvatar.isPurchased)
        {
            lockIcon.SetActive(false);
            unlockButton.gameObject.SetActive(true);
            unlockButton.interactable = userCoins >= currentAvatar.cost;
            shadeOverlay.SetActive(true);
        }
        else
        {
            lockIcon.SetActive(false);
            unlockButton.gameObject.SetActive(false);
            shadeOverlay.SetActive(false);
            lockedButton.SetActive(false);
        }

        costText.text = currentAvatar.isPurchased ? "Unlocked" : $"Cost: {currentAvatar.cost}";
    }

 private void RestartPublishCoroutine()
    {
        AvatarInfo currentAvatar = avatars[currentAvatarIndex];
        
        // Only start the coroutine if the avatar is unlocked and purchased
        if (currentAvatar.isUnlocked && currentAvatar.isPurchased)
        {
            if (publishCoroutine != null)
            {
                StopCoroutine(publishCoroutine);
            }
            publishCoroutine = StartCoroutine(PublishAvatarAfterDelay(currentAvatar));
        }
    }

    // Coroutine to wait for 2 seconds and then publish the avatar info via MQTT
    private IEnumerator PublishAvatarAfterDelay(AvatarInfo avatar)
    {
        yield return new WaitForSeconds(2f);

        // Publish avatar info via MQTT
        JObject avatarMessage = new JObject
        {
            {"avatar", avatar.name},
            {"mqtt", true}
        };
        if (msgHandler != null)
        {
            msgHandler.NotifyObservers("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/AVATAR/ACTIVE", avatarMessage);
            Debug.Log($"Published avatar: {avatar.name}");
        }
    }
    private class AvatarInfo
    {
        public int cost;
        public bool isUnlocked;
        public bool isPurchased;
        public Sprite sprite;
        public string name;
    }
}
