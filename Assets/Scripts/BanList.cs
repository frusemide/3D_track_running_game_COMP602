using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Devices banned by the host. Saved in PlayerPrefs so bans carry over to later sessions.
public static class BanList
{
    [Serializable]
    public class Entry
    {
        public string DeviceId;
        public string Name;
    }

    [Serializable]
    private class SaveData
    {
        public List<Entry> Entries = new List<Entry>();
    }

    private const string PrefsKey = "BannedPlayers";
    private const string FallbackIdKey = "DeviceId";

    private static SaveData _data;

    public static IReadOnlyList<Entry> Entries => Data.Entries;

    // Identifies this device. Sent to the host as the connection token when joining.
    public static string LocalDeviceId
    {
        get
        {
            string id = SystemInfo.deviceUniqueIdentifier;
            if (!string.IsNullOrEmpty(id) && id != SystemInfo.unsupportedIdentifier)
                return id;

            id = PlayerPrefs.GetString(FallbackIdKey, "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(FallbackIdKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    public static byte[] LocalConnectionToken => Encoding.UTF8.GetBytes(LocalDeviceId);

    public static string DeviceIdFromToken(byte[] token)
    {
        if (token == null || token.Length == 0)
            return null;
        return Encoding.UTF8.GetString(token);
    }

    public static bool IsBanned(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
            return false;
        return Data.Entries.Exists(e => e.DeviceId == deviceId);
    }

    public static void Ban(string deviceId, string playerName)
    {
        if (string.IsNullOrEmpty(deviceId) || IsBanned(deviceId))
            return;

        Data.Entries.Add(new Entry { DeviceId = deviceId, Name = playerName });
        Save();
    }

    public static void Unban(string deviceId)
    {
        if (Data.Entries.RemoveAll(e => e.DeviceId == deviceId) > 0)
            Save();
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
        if (_data == null || _data.Entries == null)
            _data = new SaveData();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }
}
