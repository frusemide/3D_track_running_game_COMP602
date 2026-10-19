using UnityEngine;

// Stores the local player's chosen name in PlayerPrefs.
// Shares the "PlayerName" key with LeaderboardManager.
public static class PlayerNameStore
{
    public const int MaxLength = 16;
    private const string PrefsKey = "PlayerName";

    public static string Name
    {
        get => PlayerPrefs.GetString(PrefsKey, "");
        set
        {
            PlayerPrefs.SetString(PrefsKey, Sanitise(value));
            PlayerPrefs.Save();
        }
    }

    public static bool HasName => !string.IsNullOrWhiteSpace(Name);

    // Trims whitespace, removes rich text tags and caps the length.
    public static string Sanitise(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        string clean = raw.Replace("<", "").Replace(">", "").Trim();
        if (clean.Length > MaxLength)
            clean = clean.Substring(0, MaxLength);
        return clean;
    }
}
