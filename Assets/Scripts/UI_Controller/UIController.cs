using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIController : MonoBehaviour
{
    public CanvasGroup progressPanel;
    public CanvasGroup enemyPanel;
    public CanvasGroup userPanel;
    public CanvasGroup historyPanel;
    public CanvasGroup statsPanel;
    public CanvasGroup badgePanel;
    public CanvasGroup avatarPanel;
    public CanvasGroup musicPanel;
    public CanvasGroup backgroundPanel;
    public CanvasGroup feedbackPanel;
    public CanvasGroup scorePanel;
    public CanvasGroup effectPanel;
    public CanvasGroup jokerPanel;

    // Method to toggle visibility of the Progress Panel
    public void ToggleProgressPanel()
    {
        TogglePanel(progressPanel);
    }

    // Method to toggle visibility of the Enemy Panel
    public void ToggleEnemyPanel()
    {
        TogglePanel(enemyPanel);
    }

    // Method to toggle visibility of the User Panel
    public void ToggleUserPanel()
    {
        TogglePanel(userPanel);
    }

    // Method to toggle visibility of the History Panel
    public void ToggleHistoryPanel()
    {
        TogglePanel(historyPanel);
    }

    // Method to toggle visibility of the Stats Panel
    public void ToggleStatsPanel()
    {
        TogglePanel(statsPanel);
    }

    // Method to toggle visibility of the Badge Panel
    public void ToggleBadgePanel()
    {
        TogglePanel(badgePanel);
    }

    // Method to toggle visibility of the Avatar Panel
    public void ToggleAvatarPanel()
    {
        TogglePanel(avatarPanel);
    }

    // Method to toggle visibility of the Music Panel
    public void ToggleMusicPanel()
    {
        TogglePanel(musicPanel);
    }

    // Method to toggle visibility of the Background Panel
    public void ToggleBackgroundPanel()
    {
        TogglePanel(backgroundPanel);
    }

    // Method to toggle visibility of the Feedback Panel
    public void ToggleFeedbackPanel()
    {
        TogglePanel(feedbackPanel);
    }

    // Method to toggle visibility of the Score Panel
    public void ToggleScorePanel()
    {
        TogglePanel(scorePanel);
    }

    // Method to toggle visibility of the Effect Panel
    public void ToggleEffectPanel()
    {
        TogglePanel(effectPanel);
    }

    // Method to toggle visibility of the Joker Panel
    public void ToggleJokerPanel()
    {
        TogglePanel(jokerPanel);
    }

    // General method to toggle visibility for any panel
    private void TogglePanel(CanvasGroup panel)
    {
        bool isActive = panel.alpha == 1;  // Check current state
        panel.alpha = isActive ? 0 : 1;    // Toggle alpha
        panel.interactable = !isActive;    // Toggle interactable
        panel.blocksRaycasts = !isActive;  // Toggle raycast blocking
    }
}