using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

public class StartupSaveTests
{
    [Test]
    public async Task UnreadableSaveReportsStartupFailureWithoutOverwritingIt()
    {
        var path = typeof(SaveSystem).GetField("savePath", BindingFlags.NonPublic | BindingFlags.Static);
        var previousPath = path.GetValue(null);
        var previousData = SaveSystem.Data;
        var previousSettings = SaveSystem.settingsData;
        var previousActivity = SaveSystem.activityData;
        bool previousLoaded = SaveSystem.savesLoaded;
        string key = "startup-test-" + Guid.NewGuid();
        var root = new GameObject("Startup failure test");
        root.SetActive(false);
        try
        {
            path.SetValue(null, key);
            PlayerPrefs.SetString(key, "invalid save record");
            var manager = root.AddComponent<GlobalManager>();
            LogAssert.Expect(LogType.Error, "Startup stopped while loading saved data. Existing saves have not been reset.");
            LogAssert.Expect(LogType.Exception, new Regex(".*", RegexOptions.Singleline));
            await manager.LoadStartupData();
            Assert.IsNotNull(manager.StartupError);
            Assert.IsFalse(manager.IsLoadingSave);
            Assert.IsFalse(manager.savesLoaded);
            Assert.IsFalse(SaveSystem.savesLoaded);
            Assert.AreEqual("invalid save record", PlayerPrefs.GetString(key));
        }
        finally
        {
            PlayerPrefs.DeleteKey(key);
            path.SetValue(null, previousPath);
            SaveSystem.Data = previousData;
            SaveSystem.settingsData = previousSettings;
            SaveSystem.activityData = previousActivity;
            SaveSystem.savesLoaded = previousLoaded;
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
