using System;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class SaveCleaner
{
    [MenuItem("Tools/Clean Saves")]
    private static void CleanSaves()
    {
        string saveDirectory = Application.persistentDataPath;
        try
        {
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
            Directory.CreateDirectory(saveDirectory);
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            Debug.Log("[SaveSystem] All local saves and PlayerPrefs were deleted. Directory: " + saveDirectory);
        }
        catch (Exception exception)
        {
            Debug.LogError("[SaveSystem] Failed to clean local saves: " + exception);
        }
    }

    [MenuItem("Tools/Clean Saves", true)]
    private static bool CanCleanSaves() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
