using System.Collections.Generic;
using UnityEngine;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance;

    [Header("Leaderboard Settings")]
    public int maximumEntries = 10;

    private List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

    private const string PlayerNameKey = "PlayerName";
    private const string LeaderboardKey = "LeaderboardData";

    public IReadOnlyList<LeaderboardEntry> Entries => entries;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        LoadLeaderboard();
    }

    public void AddTime(float time)
    {
        string playerName = PlayerPrefs.GetString(PlayerNameKey, "Player");

        LeaderboardEntry newEntry = new LeaderboardEntry(
            playerName,
            time
        );

        entries.Add(newEntry);

        SortLeaderboard();

        if (entries.Count > maximumEntries)
        {
            entries.RemoveRange(
                maximumEntries,
                entries.Count - maximumEntries
            );
        }

        SaveLeaderboard();

        int position = GetPosition(newEntry);

        Debug.Log(
            "Leaderboard position: " + position +
            " | Time: " + RaceTimer.FormatTime(time)
        );

        if (LeaderboardUI.Instance != null)
        {
            LeaderboardUI.Instance.ShowLeaderboard(position);
        }
    }

    private void SortLeaderboard()
    {
        entries.Sort((a, b) =>
            a.time.CompareTo(b.time)
        );
    }

    public int GetPosition(LeaderboardEntry entry)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] == entry)
            {
                return i + 1;
            }
        }

        return -1;
    }

    public void SetPlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Player";
        }

        playerName = playerName.Trim();

        if (playerName.Length > 16)
        {
            playerName = playerName.Substring(0, 16);
        }

        PlayerPrefs.SetString(PlayerNameKey, playerName);
        PlayerPrefs.Save();
    }

    public string GetPlayerName()
    {
        return PlayerPrefs.GetString(PlayerNameKey, "Player");
    }

    private void SaveLeaderboard()
    {
        LeaderboardSaveData saveData = new LeaderboardSaveData();

        saveData.entries = entries;

        string json = JsonUtility.ToJson(saveData);

        PlayerPrefs.SetString(LeaderboardKey, json);
        PlayerPrefs.Save();
    }

    private void LoadLeaderboard()
    {
        entries.Clear();

        if (!PlayerPrefs.HasKey(LeaderboardKey))
            return;

        string json = PlayerPrefs.GetString(LeaderboardKey);

        if (string.IsNullOrEmpty(json))
            return;

        LeaderboardSaveData saveData =
            JsonUtility.FromJson<LeaderboardSaveData>(json);

        if (saveData == null || saveData.entries == null)
            return;

        entries = saveData.entries;

        SortLeaderboard();

        if (entries.Count > maximumEntries)
        {
            entries.RemoveRange(
                maximumEntries,
                entries.Count - maximumEntries
            );
        }
    }

    public void ClearLeaderboard()
    {
        entries.Clear();

        PlayerPrefs.DeleteKey(LeaderboardKey);
        PlayerPrefs.Save();

        if (LeaderboardUI.Instance != null)
        {
            LeaderboardUI.Instance.RefreshLeaderboard();
        }

        Debug.Log("Leaderboard cleared.");
    }
}

[System.Serializable]
public class LeaderboardSaveData
{
    public List<LeaderboardEntry> entries =
        new List<LeaderboardEntry>();
}
