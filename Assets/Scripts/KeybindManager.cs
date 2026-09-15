using UnityEngine;
using UnityEngine.InputSystem;

//keeps players new keybinds, saves to disk to keep between sessions.

public static class KeybindManager
{
    private const string PedalAKeyPref = "Keybind_PedalA";
    private const string PedalBKeyPref = "Keybind_PedalB";

    private const Key DefaultPedalA = Key.LeftArrow;
    private const Key DefaultPedalB = Key.RightArrow;

    public static Key PedalA
    {
        get => LoadKey(PedalAKeyPref, DefaultPedalA);
        set => SaveKey(PedalAKeyPref, value);
    }

    public static Key PedalB
    {
        get => LoadKey(PedalBKeyPref, DefaultPedalB);
        set => SaveKey(PedalBKeyPref, value);
    }

    private static Key LoadKey(string prefName, Key defaultKey)
    {
        int saved = PlayerPrefs.GetInt(prefName, (int)defaultKey);
        return (Key)saved;
    }

    private static void SaveKey(string prefName, Key key)
    {
        PlayerPrefs.SetInt(prefName, (int)key);
        PlayerPrefs.Save();
    }

    public static void ResetToDefaults()
    {
        PedalA = DefaultPedalA;
        PedalB = DefaultPedalB;
    }
}