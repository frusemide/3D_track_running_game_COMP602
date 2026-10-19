using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

// Everything saved for the local player, in one place. SaveSystem writes this to a
// single JSON file (profile.json in Application.persistentDataPath).
//
// Adding data later: add a field (or a new section class) with a sensible default.
// Older save files just get that default for anything they don't contain yet, so
// existing profiles keep working. Bump Version if a change needs special handling.
[Serializable]
public class PlayerProfile
{
    public int Version = 1;
    public string PlayerName = "Player";

    public CosmeticsData Cosmetics = new CosmeticsData();
    public WalletData Wallet = new WalletData();
    public RaceStats Stats = new RaceStats();
    public List<LeaderboardEntry> Leaderboard = new List<LeaderboardEntry>();
    public SettingsData Settings = new SettingsData();

    // Set once the old PlayerPrefs data (high score, player name, leaderboard,
    // keybinds) has been copied in. Not used yet -- that import happens when
    // HighScoreManager / LeaderboardManager / KeybindManager move onto the profile.
    public bool LegacyPrefsImported;

    // Guards against missing sections (e.g. a hand-edited or partial file).
    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(PlayerName)) PlayerName = "Player";
        Cosmetics ??= new CosmeticsData();
        Wallet ??= new WalletData();
        Stats ??= new RaceStats();
        Leaderboard ??= new List<LeaderboardEntry>();
        Settings ??= new SettingsData();
    }
}

// Indices into CosmeticDatabase lists (-1 = nothing equipped for head/feet).
// The database lists must stay append-only so saved indices keep pointing at the same items.
[Serializable]
public class CosmeticsData
{
    public int Head = -1;
    public int Colour = 0;
    public int Feet = -1;
}

// Currency earned from races. Placeholder for the wallet feature -- owned items etc.
// can be added here once the currency design is agreed.
[Serializable]
public class WalletData
{
    public int Coins;
}

[Serializable]
public class RaceStats
{
    public int TotalRaces;
    public int Wins;
    public int Losses;
    public float FastestTime = -1f;   // seconds; -1 = no race finished yet
}

[Serializable]
public class SettingsData
{
    public int PedalA = (int)Key.LeftArrow;
    public int PedalB = (int)Key.RightArrow;
}
