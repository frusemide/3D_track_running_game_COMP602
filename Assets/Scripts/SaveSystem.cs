using System;
using System.IO;
using UnityEngine;

// Loads and saves the local PlayerProfile as one JSON file.
//
//   SaveSystem.Profile.Cosmetics.Head = 2;
//   SaveSystem.Save();
//
// The file lives in Application.persistentDataPath -- on Windows:
//   C:\Users\<you>\AppData\LocalLow\<Company>\<Product>\profile.json
// Open it in a text editor to check what saved, or delete it to reset.
//
// Testing two players on one PC: launch one copy with "-profile 2" to use
// profile_2.json instead, so they don't share a save.
//
// This is the only script that touches storage, so moving saves online later
// (e.g. for a cheat-proof wallet) only means changing Load() and Save().
public static class SaveSystem
{
    private static PlayerProfile _profile;
    private static string _filePath;

    // The local player's profile. Loaded from disk the first time it's used.
    public static PlayerProfile Profile
    {
        get
        {
            if (_profile == null)
                Load();
            return _profile;
        }
    }

    public static string FilePath => _filePath ??= BuildFilePath();

    public static void Load()
    {
        string path = FilePath;
        PlayerProfile loaded = null;

        try
        {
            if (File.Exists(path))
            {
                // Overwrite a fresh profile so anything missing from the file keeps its default.
                loaded = new PlayerProfile();
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), loaded);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] Couldn't read {path} ({e.Message}). " +
                             "Keeping a backup copy and starting a fresh profile.");
            BackUpBrokenFile(path);
            loaded = null;
        }

        _profile = loaded ?? new PlayerProfile();
        _profile.EnsureValid();
    }

    public static void Save()
    {
        if (_profile == null)
            return;

        string path = FilePath;
        string tempPath = path + ".tmp";

        try
        {
            // Write to a temp file first, then swap it in, so a crash mid-write
            // can't leave a half-written (corrupt) profile behind.
            File.WriteAllText(tempPath, JsonUtility.ToJson(_profile, true));

            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Couldn't save {path}: {e.Message}");
        }
    }

    // Wipes the profile back to defaults (useful for a "reset progress" option or testing).
    public static void ResetProfile()
    {
        _profile = new PlayerProfile();
        Save();
    }

    private static string BuildFilePath()
    {
        string fileName = "profile";

        // Optional "-profile <name>" launch argument for testing several players on one PC.
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-profile")
            {
                string suffix = MakeSafeFileName(args[i + 1]);
                if (suffix.Length > 0)
                    fileName = "profile_" + suffix;
            }
        }

        return Path.Combine(Application.persistentDataPath, fileName + ".json");
    }

    private static string MakeSafeFileName(string input)
    {
        var chars = new System.Text.StringBuilder();
        foreach (char c in input)
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                chars.Append(c);
        return chars.ToString();
    }

    private static void BackUpBrokenFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Copy(path, path + ".broken", true);
        }
        catch { /* best effort only */ }
    }

    // Clears cached state when entering Play mode, in case domain reload is disabled
    // in the Editor ("Enter Play Mode Options"), so each play session reads fresh.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _profile = null;
        _filePath = null;
    }
}
