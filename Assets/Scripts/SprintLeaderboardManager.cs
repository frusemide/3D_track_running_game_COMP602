
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class SprintLeaderboardEntry
{
    public string mapId;
    public string playerId;
    public string playerName;
    public float timeSeconds;

    public SprintLeaderboardEntry(
        string mapId,
        string playerId,
        string playerName,
        float timeSeconds)
    {
        this.mapId = mapId;
        this.playerId = playerId;
        this.playerName = playerName;
        this.timeSeconds = timeSeconds;
    }
}

[Serializable]
public class SprintLeaderboardSaveData
{
    public List<SprintLeaderboardEntry> entries =
        new List<SprintLeaderboardEntry>();
}

public class SprintLeaderboardManager : MonoBehaviour
{
    public static SprintLeaderboardManager Instance { get; private set; }

    [Header("Leaderboard Settings")]
    [Min(1)]
    public int maximumEntries = 10;

    private const string SaveKey = "SprintLeaderboardData";
    private const string PlayerIdKey = "SprintPlayerId";
    private const string PlayerNameKey = "PlayerName";

    private SprintLeaderboardSaveData saveData =
        new SprintLeaderboardSaveData();

    public string CurrentPlayerId { get; private set; }

    public string CurrentPlayerName
    {
        get { return PlayerPrefs.GetString(PlayerNameKey, "Player"); }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadData();
        GetOrCreatePlayerId();
    }

    private void GetOrCreatePlayerId()
    {
        CurrentPlayerId = PlayerPrefs.GetString(PlayerIdKey, "");

        if (string.IsNullOrWhiteSpace(CurrentPlayerId))
        {
            CurrentPlayerId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(PlayerIdKey, CurrentPlayerId);
            PlayerPrefs.Save();
        }
    }

    public void SetPlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return;

        PlayerPrefs.SetString(PlayerNameKey, playerName.Trim());
        PlayerPrefs.Save();
    }

    public bool RecordSprintTime(string mapId, float timeSeconds)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            Debug.LogWarning("Cannot record a sprint: map ID is missing.");
            return false;
        }

        if (float.IsNaN(timeSeconds) ||
            float.IsInfinity(timeSeconds) ||
            timeSeconds <= 0f)
        {
            Debug.LogWarning("Cannot record an invalid sprint time.");
            return false;
        }

        SprintLeaderboardEntry existing = saveData.entries.FirstOrDefault(
            entry => entry.mapId == mapId &&
                     entry.playerId == CurrentPlayerId);

        // Keep the existing personal best unless the new time is faster.
        if (existing != null)
        {
            if (timeSeconds >= existing.timeSeconds)
            {
                Debug.Log("Sprint completed, but no new personal best.");
                return false;
            }

            existing.timeSeconds = timeSeconds;
            existing.playerName = CurrentPlayerName;
        }
        else
        {
            saveData.entries.Add(new SprintLeaderboardEntry(
                mapId,
                CurrentPlayerId,
                CurrentPlayerName,
                timeSeconds));
        }

        SaveData();

        Debug.Log(
            $"New personal best on {mapId}: {FormatTime(timeSeconds)}");

        return true;
    }

    public List<SprintLeaderboardEntry> GetLeaderboard(string mapId)
    {
        return saveData.entries
            .Where(entry => entry.mapId == mapId)
            .OrderBy(entry => entry.timeSeconds)
            .ThenBy(entry => entry.playerName)
            .Take(maximumEntries)
            .ToList();
    }

    public SprintLeaderboardEntry GetPersonalBest(string mapId)
    {
        return saveData.entries.FirstOrDefault(
            entry => entry.mapId == mapId &&
                     entry.playerId == CurrentPlayerId);
    }

    public int GetPosition(string mapId)
    {
        List<SprintLeaderboardEntry> allEntries = saveData.entries
            .Where(entry => entry.mapId == mapId)
            .OrderBy(entry => entry.timeSeconds)
            .ThenBy(entry => entry.playerName)
            .ToList();

        int index = allEntries.FindIndex(
            entry => entry.playerId == CurrentPlayerId);

        // Position is one-based. Zero means no recorded result.
        return index < 0 ? 0 : index + 1;
    }

    public static string FormatTime(float timeSeconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(timeSeconds);

        int minutes = (int)time.TotalMinutes;
        int seconds = time.Seconds;
        int milliseconds = time.Milliseconds;

        return $"{minutes:00}:{seconds:00}.{milliseconds:000}";
    }

    private void SaveData()
    {
        string json = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    private void LoadData()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            saveData = new SprintLeaderboardSaveData();
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey);
        SprintLeaderboardSaveData loaded =
            JsonUtility.FromJson<SprintLeaderboardSaveData>(json);

        saveData = loaded ?? new SprintLeaderboardSaveData();

        if (saveData.entries == null)
            saveData.entries = new List<SprintLeaderboardEntry>();
    }
}
