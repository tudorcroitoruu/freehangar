using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

public class SaveBridgeTests
{
    [Test]
    public void LegacyRawValuesPreserveNullCharactersAndMigrateOnNextWrite()
    {
        string raw = "gzip1:legacy\u0000tail";
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(new { format = "FreehangarSaveBridge", version = 1,
            entries = new[] { new { key = "/save", value = raw }, new { key = "counter", value = "MQ==" } } });
        var store = SaveBridgeStore.Decode(Encoding.UTF8.GetBytes(json));
        Assert.AreEqual(raw, store.Get("/save"));
        Assert.AreEqual("MQ==", store.Get("counter"), "Raw documents must not be partly decoded.");
        Assert.IsFalse(store.Dirty);
        var restored = SaveBridgeStore.Decode(store.Encode());
        Assert.AreEqual(raw, restored.Get("/save"));
    }
    [Test]
    public void EarlierBase64VersionOneStillLoads()
    {
        var store = new SaveBridgeStore(); store.Set("/save", "gzip1:old\u0000tail");
        string json = Encoding.UTF8.GetString(store.Encode()).Replace("\"version\":2", "\"version\":1");
        Assert.AreEqual(store.Get("/save"), SaveBridgeStore.Decode(Encoding.UTF8.GetBytes(json)).Get("/save"));
    }
    [Test]
    public void ExistingCompressedPlayerSaveSurvivesNativeContainer()
    {
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var encode = typeof(SaveSystem).GetMethod("EncodePlayerSave", flags);
        var decode = typeof(SaveSystem).GetMethod("DecodePlayerSave", flags);
        string encoded = (string)encode.Invoke(null, new object[] { JsonUtility.ToJson(new PlayerData { cash = 12345, playerName = "Save test 雪" }) });
        var store = new SaveBridgeStore(); store.Set("/save", encoded);
        var restored = SaveBridgeStore.Decode(store.Encode());
        var player = SaveSystem.DeserializePlayerData((string)decode.Invoke(null, new object[] { restored.Get("/save") }));
        string legacy = Newtonsoft.Json.JsonConvert.SerializeObject(new { format = "FreehangarSaveBridge", version = 1,
            entries = new[] { new { key = "/save", value = encoded } } });
        var migrated = SaveBridgeStore.Decode(Encoding.UTF8.GetBytes(legacy));
        var legacyPlayer = SaveSystem.DeserializePlayerData((string)decode.Invoke(null, new object[] { migrated.Get("/save") }));
        Assert.AreEqual(12345, legacyPlayer.cash);
        Assert.AreEqual(player.playerName, legacyPlayer.playerName);
        Assert.AreEqual(12345, player.cash); Assert.AreEqual("Save test 雪", player.playerName);
    }
    [Test]
    public void SnapshotRoundTripsEverySaveKeyAndUnicode()
    {
        var store = new SaveBridgeStore();
        store.Set("/save", "gzip1:encoded-current\u0000\n雪");
        store.Set("/save_Backup", "previous"); store.Set("/save_Settings", "settings");
        store.Set("levelsCompleted", "activity"); store.Set("bicycle", "camera");
        var restored = SaveBridgeStore.Decode(store.Encode());
        foreach (string key in new[] { "/save", "/save_Backup", "/save_Settings", "levelsCompleted", "bicycle" })
            Assert.AreEqual(store.Get(key), restored.Get(key));
        Assert.IsFalse(restored.Dirty);
    }
    [Test]
    public void ThrottledWritesKeepLatestSnapshotPending()
    {
        var store = new SaveBridgeStore(); store.Set("/save", "first");
        Assert.IsFalse(store.Flush((bytes, force) => false, false));
        Assert.IsTrue(store.Dirty);
        store.Set("/save", "latest"); byte[] committed = null;
        Assert.IsTrue(store.Flush((bytes, force) => { committed = bytes; return true; }, false));
        Assert.AreEqual("latest", SaveBridgeStore.Decode(committed).Get("/save"));
        Assert.IsFalse(store.Dirty);
    }
    [Test]
    public void FailedCommitRemainsDirtyAndForcedFlushReachesWriter()
    {
        var store = new SaveBridgeStore(); store.Set("/save", "snapshot");
        Assert.Throws<IOException>(() => store.Flush((bytes, force) => throw new IOException("simulated write failure"), false));
        Assert.IsTrue(store.Dirty);
        bool forced = false;
        Assert.IsTrue(store.Flush((bytes, force) => { forced = force; return true; }, true));
        Assert.IsTrue(forced);
        int writes = 0; store.Flush((bytes, force) => { writes++; return true; }, true);
        Assert.Zero(writes, "An unchanged store should not write on another quit callback.");
    }
    [Test]
    public void CompletedRunDeletionPreservesSettingsAfterReload()
    {
        var store = new SaveBridgeStore();
        store.Set("/save", "current"); store.Set("/save_Backup", "backup"); store.Set("/save_Settings", "keep");
        store.Delete("/save"); store.Delete("/save_Backup");
        var restored = SaveBridgeStore.Decode(store.Encode());
        Assert.IsFalse(restored.HasKey("/save")); Assert.IsFalse(restored.HasKey("/save_Backup"));
        Assert.AreEqual("keep", restored.Get("/save_Settings"));
    }
    [TestCase("")]
    [TestCase("{}")] 
    [TestCase("{\"format\":\"FreehangarSaveBridge\",\"version\":3,\"entries\":[]}")]
    [TestCase("{\"format\":\"FreehangarSaveBridge\",\"version\":2,\"entries\":[{\"key\":\"a\",\"value\":\"bad!\"}]}")]
    [TestCase("{\"format\":\"FreehangarSaveBridge\",\"version\":1,\"entries\":[{\"key\":\"a\",\"value\":\"MQ==\"},{\"key\":\"a\",\"value\":\"Mg==\"}]}")]
    public void InvalidFilesAreRejectedInsteadOfReplacingExistingData(string json)
    { Assert.Throws<InvalidDataException>(() => SaveBridgeStore.Decode(Encoding.UTF8.GetBytes(json))); }
    [Test]
    public void MissingFileCreatesEmptyProfileWithoutWriting()
    {
        var store = SaveBridgeStore.Decode(null);
        Assert.IsFalse(store.HasKey("/save")); Assert.IsFalse(store.Dirty);
    }
    [Test]
    public void DesktopBridgeUsesExistingPlayerPrefsWithoutChangingKeys()
    {
        string key = "bridge-test-" + Guid.NewGuid();
        try
        {
            PlayerPrefs.SetString(key, "existing-save");
            Assert.AreEqual("existing-save", SaveBridge.GetStringPP(key));
            SaveBridge.SetStringPP(key, "new-save"); Assert.AreEqual("new-save", PlayerPrefs.GetString(key));
            SaveBridge.SetIntPP(key + "i", 42); Assert.AreEqual(42, SaveBridge.GetIntPP(key + "i"));
            SaveBridge.SetFloatPP(key + "f", .125f); Assert.AreEqual(.125f, SaveBridge.GetFloatPP(key + "f"));
            SaveBridge.DeleteKeyPP(key); Assert.IsFalse(PlayerPrefs.HasKey(key));
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + "i"); PlayerPrefs.DeleteKey(key + "f"); }
    }
}
