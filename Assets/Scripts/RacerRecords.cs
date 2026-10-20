using System;
using UnityEngine;

// The local racer's career record: races run, wins, losses and best finishing time.
// Saved in PlayerPrefs on this device.
public static class RacerRecords
{
    [Serializable]
    private class SaveData
    {
        public int Races;
        public int Wins;
        public int Losses;
        public float BestTime = -1f;   // seconds; -1 = no finished race yet
    }

    private const string PrefsKey = "RacerRecords";
    private const string LegacyHighScoreKey = "HighScore";
    private const float LegacyNoScore = 999f;

    private static SaveData _data;

    public static int Races => Data.Races;
    public static int Wins => Data.Wins;
    public static int Losses => Data.Losses;
    public static bool HasBestTime => Data.BestTime > 0f;
    public static float BestTime => Data.BestTime;

    public static float WinRate => Data.Races > 0 ? (float)Data.Wins / Data.Races : 0f;

    // Records one finished race. Returns true if the time is a new personal best.
    public static bool RecordRace(bool won, float finishTime)
    {
        Data.Races++;
        if (won)
            Data.Wins++;
        else
            Data.Losses++;

        bool newBest = finishTime > 0f && (!HasBestTime || finishTime < Data.BestTime);
        if (newBest)
            Data.BestTime = finishTime;

        Save();
        return newBest;
    }

    public static string FormatTime(float seconds)
    {
        if (seconds <= 0f)
            return "--:--.---";

        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainder = seconds - minutes * 60f;
        return $"{minutes}:{remainder:00.000}";
    }

    private static SaveData Data
    {
        get
        {
            if (_data == null)
                Load();
            return _data;
        }
    }

    private static void Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        _data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveData>(json);

        if (_data == null)
        {
            _data = new SaveData();

            // Carries over a best time saved by HighScoreManager before records existed.
            float legacyBest = PlayerPrefs.GetFloat(LegacyHighScoreKey, LegacyNoScore);
            if (legacyBest > 0f && legacyBest < LegacyNoScore)
                _data.BestTime = legacyBest;
        }
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_data));
        PlayerPrefs.Save();
    }
}
