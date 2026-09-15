using UnityEngine;
using TMPro;

public class LeaderboardUI : MonoBehaviour
{
    public static LeaderboardUI Instance;

    [Header("Main Panel")]
    public GameObject leaderboardPanel;

    [Header("Title")]
    public TMP_Text titleText;

    [Header("Player Result")]
    public TMP_Text playerResultText;

    [Header("Leaderboard Rows")]
    public TMP_Text[] positionTexts;
    public TMP_Text[] nameTexts;
    public TMP_Text[] timeTexts;

    [Header("Player Name")]
    public TMP_InputField playerNameInput;

    private void Awake()
    {
        Instance = this;

        HideLeaderboard();

        LoadPlayerName();
    }

    public void ShowLeaderboard(int playerPosition)
    {
        if (leaderboardPanel != null)
        {
            leaderboardPanel.SetActive(true);
        }

        RefreshLeaderboard();

        if (playerResultText != null)
        {
            if (playerPosition > 0)
            {
                playerResultText.text =
                    "Your Position: #" + playerPosition;
            }
            else
            {
                playerResultText.text =
                    "Your time did not make the leaderboard.";
            }
        }
    }

    public void HideLeaderboard()
    {
        if (leaderboardPanel != null)
        {
            leaderboardPanel.SetActive(false);
        }
    }

    public void RefreshLeaderboard()
    {
        if (LeaderboardManager.Instance == null)
            return;

        var entries = LeaderboardManager.Instance.Entries;

        for (int i = 0; i < positionTexts.Length; i++)
        {
            bool hasEntry = i < entries.Count;

            if (positionTexts[i] != null)
                positionTexts[i].gameObject.SetActive(hasEntry);

            if (nameTexts[i] != null)
                nameTexts[i].gameObject.SetActive(hasEntry);

            if (timeTexts[i] != null)
                timeTexts[i].gameObject.SetActive(hasEntry);

            if (!hasEntry)
                continue;

            LeaderboardEntry entry = entries[i];

            if (positionTexts[i] != null)
            {
                positionTexts[i].text = "#" + (i + 1);
            }

            if (nameTexts[i] != null)
            {
                nameTexts[i].text = entry.playerName;
            }

            if (timeTexts[i] != null)
            {
                timeTexts[i].text =
                    RaceTimer.FormatTime(entry.time);
            }
        }
    }

    public void SavePlayerName()
    {
        if (playerNameInput == null)
            return;

        if (LeaderboardManager.Instance == null)
            return;

        LeaderboardManager.Instance.SetPlayerName(
            playerNameInput.text
        );
    }

    private void LoadPlayerName()
    {
        if (playerNameInput == null)
            return;

        if (LeaderboardManager.Instance == null)
            return;

        playerNameInput.text =
            LeaderboardManager.Instance.GetPlayerName();
    }
}
