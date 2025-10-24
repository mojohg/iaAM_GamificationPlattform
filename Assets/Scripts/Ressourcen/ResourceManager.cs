using System;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    private int userCoins;
    private int userScore;
    private string username;

    public event Action OnUserDataChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("ResourceManager Initialized");
        }
        else
        {
            Destroy(gameObject); // Ensures there's only one instance
        }
    }

    public void SetCoins(int coins)
    {
        userCoins = coins;
        NotifyUI();
    }

    public void SetScore(int score)
    {
        userScore = score;
        NotifyUI();
    }

    public void SetUsername(string newUsername)
    {
        username = newUsername;
        NotifyUI();
    }

    private void NotifyUI()
    {
        OnUserDataChanged?.Invoke();  // Notify any UI element that is listening for changes
        Debug.Log("User data updated");
    }

    public int GetCoins() => userCoins;
    public int GetScore() => userScore;
    public string GetUsername() => username;
}
