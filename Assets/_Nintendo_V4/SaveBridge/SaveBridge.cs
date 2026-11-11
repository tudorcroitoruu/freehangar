using System.Globalization;
using UnityEngine;

/// <summary>Existing SaveSystem values use PlayerPrefs on desktop and NXSave on Switch.</summary>
public static class SaveBridge
{
#if UNITY_SWITCH && !UNITY_EDITOR
    private static SaveBridgeStore store;
    private static bool flushRequested;
    private static bool capturingExit;
    private static bool exitCaptured;
#endif

    // Initialize on the first SaveSystem read so startup can report/retry failures.
    public static void Initialise()
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        if (store != null) return;
        NXSave.Initialize();
        bool loaded = false;
        byte[] bytes = NXSave.Load(ref loaded);
        if (!loaded) throw new System.IO.IOException("Unable to read Switch save data.");
        // Do not permit writes after a failed/corrupt load.
        var restored = SaveBridgeStore.Decode(bytes);
        var runner = new GameObject("Switch Save Bridge");
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<SwitchSavePump>();
        store = restored;
#endif
    }

    public static bool HasKeyPP(string key)
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        Initialise(); return store.HasKey(key);
#else
        return PlayerPrefs.HasKey(key);
#endif
    }
    public static string GetStringPP(string key, string defaultValue = "")
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        Initialise(); return store.Get(key, defaultValue);
#else
        return PlayerPrefs.GetString(key, defaultValue);
#endif
    }
    public static void SetStringPP(string key, string value)
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        Initialise(); store.Set(key, value);
#else
        PlayerPrefs.SetString(key, value);
#endif
    }
    public static int GetIntPP(string key, int defaultValue = 0)
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        return int.TryParse(GetStringPP(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : defaultValue;
#else
        return PlayerPrefs.GetInt(key, defaultValue);
#endif
    }
    public static void SetIntPP(string key, int value)
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        SetStringPP(key, value.ToString(CultureInfo.InvariantCulture));
#else
        PlayerPrefs.SetInt(key, value);
#endif
    }
    public static float GetFloatPP(string key, float defaultValue = 0)
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        return float.TryParse(GetStringPP(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : defaultValue;
#else
        return PlayerPrefs.GetFloat(key, defaultValue);
#endif
    }
    public static void SetFloatPP(string key, float value)
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        SetStringPP(key, value.ToString("R", CultureInfo.InvariantCulture));
#else
        PlayerPrefs.SetFloat(key, value);
#endif
    }
    public static void DeleteKeyPP(string key)
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        Initialise(); store.Delete(key);
#else
        PlayerPrefs.DeleteKey(key);
#endif
    }
    public static void DeleteAllPP()
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        Initialise(); store.Clear();
#else
        PlayerPrefs.DeleteAll();
#endif
        SaveAllData();
    }
    public static void SaveAllData(bool ignoreFrequencyRestrictions = false)
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        Initialise();
        flushRequested = store.Dirty;
        if (flushRequested && (ignoreFrequencyRestrictions || NXSave.CanSave) && store.Flush(NXSave.Save, ignoreFrequencyRestrictions)) flushRequested = false;
#else
        PlayerPrefs.Save();
#endif
    }
    internal static void FlushPending()
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        if (store != null && flushRequested && NXSave.CanSave && store.Flush(NXSave.Save, false)) flushRequested = false;
#endif
    }
    public static void SaveOnQuit()
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        if (exitCaptured) { SaveAllData(true); return; }
        CaptureAndFlush();
        exitCaptured = true;
#else
        PlayerPrefs.Save();
#endif
    }
    public static void SaveOnBackground()
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        CaptureAndFlush();
#endif
    }
#if UNITY_SWITCH && !UNITY_EDITOR
    private static void CaptureAndFlush()
    {
        if (capturingExit) return;
        capturingExit = true;
        try
        {
            // Capture live world/profile state and invalidate any older async save.
            if (SaveSystem.savesLoaded) SaveSystem.SaveData();
            SaveAllData(true);
        }
        finally { capturingExit = false; }
    }
#endif
    public static void LoadAllData() => Initialise();
    public static bool JsonExists(string key) => HasKeyPP(key);
    public static void JsonSave(string key, string json) => SetStringPP(key, json);
    public static string JsonLoad(string key) => GetStringPP(key);
    public static void JsonDeleteKey(string key) => DeleteKeyPP(key);
}
