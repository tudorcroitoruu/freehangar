using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

public static class SaveSystem
{
    public static bool savesLoaded;

    public static PlayerData Data;
    public static SettingsData settingsData;
    public static ActivityData activityData;

    public static event Action<int> MoneyChanged;
    public static event Action OwnedProductsChanged;
    public static event Action HangarProgressChanged;
    public static int Money => Data != null ? Data.cash : 0;
    private static bool upgradePurchaseInProgress;
    private static PlayerData upgradeCooldownProfile;
    private static double nextUpgradeAllowedAt;
    public static bool UpgradePurchaseLocked => upgradePurchaseInProgress ||
        (upgradeCooldownProfile == Data && Time.realtimeSinceStartupAsDouble < nextUpgradeAllowedAt);

    private static bool ApplyUpgradePurchase(Func<PlayerData, bool> purchase)
    {
        if (Data == null || UpgradePurchaseLocked) return false;
        // Lock before changing data or publishing MoneyChanged: event callbacks
        // must not reenter a purchase or spend the same voucher a second time.
        upgradePurchaseInProgress = true;
        try
        {
            if (!purchase(Data)) return false;
            upgradeCooldownProfile = Data;
            nextUpgradeAllowedAt = Time.realtimeSinceStartupAsDouble + .4d;
            MoneyChanged?.Invoke(Money);
            nextUpgradeAllowedAt = Time.realtimeSinceStartupAsDouble + .4d;
            return true;
        }
        finally { upgradePurchaseInProgress = false; }
    }

    public static bool TryOpenHangar(int number)
    {
        if (!savesLoaded || Data == null || !Data.TryOpenHangar(number)) return false;
        GoalChecker.ReportValue(GoalType.HangarUnlocked, 1, number);
        MoneyChanged?.Invoke(Money);
        HangarProgressChanged?.Invoke();
        return true;
    }

    public static bool TryUnlockFirstHangarShopProduct(float clearedPercent)
    {
        if (clearedPercent < 50f || !TryRecordHangarClearance(1, clearedPercent)) return false;
        MoneyChanged?.Invoke(Money);
        return true;
    }

    public static bool TryRecordHangarClearance(int hangar, float clearedPercent)
    {
        if (!savesLoaded || Data == null || !Data.TryRecordHangarClearance(hangar, clearedPercent)) return false;
        GoalChecker.ReportValue(GoalType.HangarCleared, Data.GetHangarClearedPercent(hangar), hangar);
        HangarProgressChanged?.Invoke();
        return true;
    }

    public static bool TryPurchaseShovelUpgrade(int price, int expectedLevel, int maxLevel)
    {
        bool purchased = ApplyUpgradePurchase(data => data.TryUpgradeShovel(price, expectedLevel, maxLevel));
        if (purchased) GoalChecker.ReportValue(GoalType.UpgradePurchased, 1, upgradeType: GoalUpgradeType.Shovel);
        return purchased;
    }

    public static bool TryPurchaseStaminaUpgrade(int price, int expectedLevel, int maxLevel)
    {
        bool purchased = ApplyUpgradePurchase(data => data.TryUpgradeStamina(price, expectedLevel, maxLevel));
        if (purchased) GoalChecker.ReportValue(GoalType.UpgradePurchased, 1, upgradeType: GoalUpgradeType.Stamina);
        return purchased;
    }

    public static bool TryPurchaseVehicleUpgrade(string id, int price, int expectedLevel, int maxLevel)
    {
        return ApplyUpgradePurchase(data => data.TryUpgradeVehicle(id, price, expectedLevel, maxLevel));
    }

    public static bool TryPurchaseVehicle(VehicleUpgradeSettings settings, string currentId)
    {
        bool purchased = settings != null && ApplyUpgradePurchase(data => settings.TryPurchase(data, currentId));
        if (purchased) RecordGoalUpgrade(GoalUpgradeType.LargerTruck);
        return purchased;
    }

    private static void RecordGoalUpgrade(GoalUpgradeType type)
    {
        Data.purchasedGoalUpgrades ??= new List<GoalUpgradeType>();
        if (!Data.purchasedGoalUpgrades.Contains(type)) Data.purchasedGoalUpgrades.Add(type);
        GoalChecker.ReportValue(GoalType.UpgradePurchased, 1, upgradeType: type);
    }

    public static bool TryPurchaseUpgrade(int price, Func<PlayerData, bool> apply, out bool usedVoucher)
    {
        usedVoucher = false;
        if (Data == null || price <= 0) return false;
        bool free = Data.freeUpgradeVouchers > 0;
        if (!free && Data.cash < price) return false;
        if (apply == null || !apply(Data)) return false;
        if (free) Data.freeUpgradeVouchers--;
        else Data.cash -= price;
        usedVoucher = free;
        MoneyChanged?.Invoke(Money);
        return true;
    }

    public static bool AddMoney(int amount)
    {
        if (Data == null || amount <= 0 || Data.cash > int.MaxValue - amount) return false;
        Data.cash += amount;
        MoneyChanged?.Invoke(Money);
        return true;
    }

    public static bool SpendMoney(int amount)
    {
        if (Data == null || amount <= 0 || Data.cash < amount) return false;
        Data.cash -= amount;
        MoneyChanged?.Invoke(Money);
        return true;
    }

    public static bool TryBuyProduct(string productID, int price)
    {
        if (Data == null || !Data.TryBuyProduct(productID, price)) return false;
        if (productID == "largeBag") RecordGoalUpgrade(GoalUpgradeType.LargerSack);
        MoneyChanged?.Invoke(Money);
        OwnedProductsChanged?.Invoke();
        return true;
    }

    public static bool TryTakeProduct(string productID)
    {
        if (Data == null || !Data.TryTakeProduct(productID)) return false;
        MoneyChanged?.Invoke(Money);
        OwnedProductsChanged?.Invoke();
        return true;
    }

    /// <summary>Charge for an immediately delivered product without adding pickup stock.</summary>
    public static bool TryBuyProduct(string productID, int price, Func<bool> tryDeliver)
    {
        if (Data == null || string.IsNullOrWhiteSpace(productID) || price < 0 || Data.cash < price ||
            tryDeliver == null || !tryDeliver()) return false;
        Data.cash -= price;
        if (productID == "largeBag") RecordGoalUpgrade(GoalUpgradeType.LargerSack);
        MoneyChanged?.Invoke(Money);
        return true;
    }

    static string savePath = "/save";
    public static bool HasSavedGame => PlayerPrefs.HasKey(savePath);

    static int key = 5;
    
    public static void SaveData()
    {
        WorldSaveRuntime.CaptureIntoPlayerData();
        WriteData();
    }

    internal static void WriteData(bool createSave = false)
    {
        if (Data == null) return;
        string json = JsonUtility.ToJson(Data); //Global Data
        string jsonSettings = JsonUtility.ToJson(settingsData); //Settings Data (on new save it willhave same settings)
        string jsonActivity = JsonUtility.ToJson(activityData); //Activity Data (PS5)
        
        // Opening the menu or changing settings must not create a resumable game.
        if (createSave || HasSavedGame || WorldSaveRuntime.Instance != null)
        {
            if (PlayerPrefs.HasKey(savePath)) PlayerPrefs.SetString(savePath + "_Backup", PlayerPrefs.GetString(savePath));
            PlayerPrefs.SetString(savePath, EncryptDecrypt(json));
        }
        PlayerPrefs.SetString(savePath +"_Settings", EncryptDecrypt(jsonSettings));
        PlayerPrefs.SetString("levelsCompleted", EncryptDecrypt(jsonActivity));
        
        PlayerPrefs.Save();
        MoneyChanged?.Invoke(Money);
    }

    /*public static bool LoadData()
    {
        playerData = new PlayerData();

        bool newSave = false;

        if (File.Exists(savePath))
        {
            try
            {
                byte[] source = File.ReadAllBytes(savePath);
                string json = DecompressString(source);
                playerData = JsonUtility.FromJson<PlayerData>(EncryptDecrypt(json));
            }
            catch
            {
                //NEW
                newSave = true;
            }
        }
        else
        {
            newSave = true;
        }

        if (newSave)
        {
            playerData.quality = 2;
            playerData.fpsLevel = 1;
            playerData.cash = 1500;

            playerData.playerName = "TRUCKER_" + Random.Range(1000, 10000);
            SaveData();
        }

        savesLoaded = true;

        return true;
    }*/
    
    public static async Task<(PlayerData, SettingsData, ActivityData)> LoadData() //for PS
    {
        savesLoaded = false;
        Data = new PlayerData();
        settingsData = new SettingsData();
        activityData = new ActivityData();
        
        bool newSave = false;

        if (PlayerPrefs.HasKey(savePath))
        {
            try
            {
             //   byte[] source = File.ReadAllBytes(savePath);
                string json = PlayerPrefs.GetString(savePath);
                string jsonSettings = PlayerPrefs.GetString(savePath +"_Settings");
                string jsonActivity = PlayerPrefs.GetString("levelsCompleted");
                
                Data = DeserializePlayerData(EncryptDecrypt(json));
                settingsData = JsonUtility.FromJson<SettingsData>(EncryptDecrypt(jsonSettings));
                activityData = JsonUtility.FromJson<ActivityData>(EncryptDecrypt(jsonActivity));
            }
            catch
            {
                // Preserve the previous complete snapshot if the latest write was damaged.
                string backup = PlayerPrefs.GetString(savePath + "_Backup", "");
                if (string.IsNullOrEmpty(backup)) throw;
                Data = DeserializePlayerData(EncryptDecrypt(backup));
                if (Data == null) throw new InvalidDataException("Both save records are unreadable.");
                Debug.LogWarning("Recovered the previous SaveSystem snapshot.");
            }
        }
        else
        {
            newSave = true;
        }

        if (newSave)
        {
            // Bootstrap also loads data before the menu: initialize defaults without
            // creating a game until New Game is confirmed or gameplay saves progress.
            Data.playerName = "PLAYER_" + Random.Range(1000, 10000);
            string savedSettings = PlayerPrefs.GetString(savePath + "_Settings", "");
            if (!string.IsNullOrEmpty(savedSettings))
                settingsData = JsonUtility.FromJson<SettingsData>(EncryptDecrypt(savedSettings));
        }

        if (Data == null)
        {
           await MakeNewSave();
        }

        if (settingsData == null)
        {
            settingsData = new SettingsData();
        }

        if (activityData == null)
        {
            activityData = new ActivityData();
        }

        Data.ownedProducts ??= new List<OwnedProductData>();
        
     //  InitAchievementsList();
        
        savesLoaded = true;

        if (Data.MigrateHangarProgress()) SaveData();
        HangarProgressChanged?.Invoke();

        MoneyChanged?.Invoke(Money);
        OwnedProductsChanged?.Invoke();
        return (Data,settingsData,activityData);
    }


    /*private static void InitAchievementsList()
    {
        for (int i = 0; i < Enum.GetValues(typeof(ETrophey)).Length; i++)
        {
            if (Data.achievements.Count < i)
            {
                Data.achievements.Add(new AchievementStatus((ETrophey) i , 0f));
            }
        }
    }*/
    public static async Task<PlayerData> MakeNewSave()
    {

        Data = new PlayerData();
        Data.playerName = "PLAYER_" + Random.Range(1000,10000);


     //   GVC_Main loadedCar = Resources.Load<GVC_Main>("Cars/" + initialCarData.carName);
        bool gotMainColor = false;
        bool gotRimColor = false;
        Color mainColorCar = Color.black;
        Color mainColorRim = Color.black;

       // List<Renderer> allMeshes = loadedCar.GetComponentsInChildren<Renderer>(true).ToList();
       
        //InitAchievementsList();

        WriteData(createSave: true);
        if (GlobalManager.Instance != null) await GlobalManager.Instance.LoadData();
        Debug.Log("<color=red>New save</color>");
        return Data;
    }
    private static string EncryptDecrypt(string textToEncrypt)
    {
        StringBuilder inSb = new StringBuilder(textToEncrypt);
        StringBuilder outSb = new StringBuilder(textToEncrypt.Length);
        char c;
        for (int i = 0; i < textToEncrypt.Length; i++)
        {
            c = inSb[i];
            c = (char)(c ^ key);
            outSb.Append(c);
        }
        return outSb.ToString();
    }

    public static byte[] CompressString(string jsonString)
    {
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonString);
        using (var memoryStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
            {
                gzipStream.Write(jsonBytes, 0, jsonBytes.Length);
            }
            return memoryStream.ToArray();
        }
    }

    [Serializable] private sealed class LegacyPlayerData { public string worldSnapshot; }
    [Serializable] private sealed class LegacyWorldData { public List<WorldObjectSave> objects; }

    public static PlayerData DeserializePlayerData(string json)
    {
        var data = JsonUtility.FromJson<PlayerData>(json);
        if (data == null) throw new InvalidDataException("Unreadable player save.");
        var legacy = JsonUtility.FromJson<LegacyPlayerData>(json);
        if (!string.IsNullOrEmpty(legacy.worldSnapshot))
        {
            string worldJson = DecompressString(Convert.FromBase64String(legacy.worldSnapshot));
            data.world = JsonUtility.FromJson<WorldSaveData>(worldJson);
            var records = JsonUtility.FromJson<LegacyWorldData>(worldJson).objects;
            if (data.world == null || data.world.version != 1 || records == null)
                throw new InvalidDataException("Unsupported legacy world save.");
            data.world.bags = new List<BagSave>(); data.world.vehicles = new List<VehicleSave>();
            data.world.wheelbarrows = new List<WheelbarrowSave>(); data.world.items = new List<WorldObjectSave>();
            foreach (var record in records)
            {
                string recordJson = JsonUtility.ToJson(record);
                if (record.kind == "container") data.world.bags.Add(JsonUtility.FromJson<BagSave>(recordJson));
                else if (record.kind == "vehicle" && record.id.IndexOf("wheelbarrow", StringComparison.OrdinalIgnoreCase) >= 0)
                    data.world.wheelbarrows.Add(JsonUtility.FromJson<WheelbarrowSave>(recordJson));
                else if (record.kind == "vehicle") data.world.vehicles.Add(JsonUtility.FromJson<VehicleSave>(recordJson));
                else data.world.items.Add(record);
            }
            data.world.version = 2; data.world.NestCargo();
        }
        return data;
    }

    // Decompress byte array back to JSON string
    public static string DecompressString(byte[] compressedBytes)
    {
        using (var memoryStream = new MemoryStream(compressedBytes))
        {
            using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
            {
                using (var outputStream = new MemoryStream())
                {
                    gzipStream.CopyTo(outputStream);
                    byte[] outputBytes = outputStream.ToArray();
                    return Encoding.UTF8.GetString(outputBytes);
                }
            }
        }
    }
}


[Serializable]
public class PlayerData
{
    public WorldSaveData world;
    public bool introductionCompleted;
    [Header("Hangar progression")]
    public List<int> openedHangars = new List<int> { 1 };
    public bool firstHangarHalfCleared;
    public bool hangar2ChickenDefeated;
    public List<float> hangarClearedPercent = new List<float>();

    public bool IsHangarOpen(int number) => number >= 1 && number <= 5 &&
        (number == 1 || (openedHangars != null && openedHangars.Contains(number)) ||
         Array.IndexOf(foundSecrets ?? Array.Empty<string>(), "farm.hangar.open." + number) >= 0);

    public bool TryOpenHangar(int number)
    {
        if (number < 2 || number > 5 || IsHangarOpen(number)) return false;
        openedHangars ??= new List<int> { 1 };
        openedHangars.Add(number);
        return true;
    }

    public bool TryRecordHangarClearance(int number, float clearedPercent)
    {
        if (number < 1 || number > 5 || !float.IsFinite(clearedPercent) || clearedPercent < 0f ||
            clearedPercent > 100f || clearedPercent <= GetHangarClearedPercent(number)) return false;
        hangarClearedPercent ??= new List<float>();
        while (hangarClearedPercent.Count < number) hangarClearedPercent.Add(0f);
        hangarClearedPercent[number - 1] = clearedPercent;
        if (number == 1 && clearedPercent >= 50f) firstHangarHalfCleared = true;
        return true;
    }

    public float GetHangarClearedPercent(int number)
    {
        if (number < 1 || number > 5) return 0f;
        float value = hangarClearedPercent != null && hangarClearedPercent.Count >= number
            ? hangarClearedPercent[number - 1] : 0f;
        if (!float.IsFinite(value)) value = 0f;
        value = Mathf.Clamp(value, 0f, 100f);
        // Old saves only stored this milestone; retain already unlocked products.
        return number == 1 && firstHangarHalfCleared ? Mathf.Max(50f, value) : value;
    }

    /// <summary>Import hangars opened by older saves without treating a collected key as an open door.</summary>
    public bool MigrateHangarProgress()
    {
        bool changed = false;
        if (openedHangars == null) { openedHangars = new List<int>(); changed = true; }
        for (int number = 1; number <= 5; number++)
        {
            if (!IsHangarOpen(number) || openedHangars.Contains(number)) continue;
            openedHangars.Add(number);
            changed = true;
        }
        return changed;
    }

    [Header("Grain progression")]
    public AchievementProgress achievementProgress = new AchievementProgress();
    public double grainBaggedKilograms;
    public string[] foundSecrets = Array.Empty<string>();
    public int freeUpgradeVouchers;
    public long infiniteStaminaUntilUtc;
    public long noAttacksUntilUtc;
    public long mobilityUntilUtc;
    public long doubleSellCashUntilUtc;
    public int wheelbarrowTier;
    public int[] wheelbarrowUpgradeLevels = new int[3];
    public int shovelUpgradeLevel;
    public int staminaUpgradeLevel;
    public List<VehicleUpgradeSave> vehicleUpgrades = new List<VehicleUpgradeSave>();
    public List<string> unlockedVehicles = new List<string>();
    public List<GoalUpgradeType> purchasedGoalUpgrades = new List<GoalUpgradeType>();

    public int GetVehicleUpgradeLevel(string id)
    {
        var saved = vehicleUpgrades?.Find(entry => entry != null && entry.id == id);
        if (saved != null) return Math.Max(0, saved.level);
        // Preserve upgrades purchased with the former wheelbarrow U shortcut.
        const string prefix = "wheelbarrow.";
        if (id != null && id.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(id.Substring(prefix.Length), out int index) && index >= 0 &&
            wheelbarrowUpgradeLevels != null && index < wheelbarrowUpgradeLevels.Length)
            return Math.Max(0, wheelbarrowUpgradeLevels[index]);
        return 0;
    }

    public bool TryUpgradeVehicle(string id, int price, int expectedLevel, int maxLevel)
    {
        bool voucher = price > 0 && freeUpgradeVouchers > 0;
        if (string.IsNullOrWhiteSpace(id) || price < 0 || (!voucher && cash < price) ||
            expectedLevel < 0 || expectedLevel >= maxLevel || GetVehicleUpgradeLevel(id) != expectedLevel) return false;
        vehicleUpgrades ??= new List<VehicleUpgradeSave>();
        var saved = vehicleUpgrades.Find(entry => entry != null && entry.id == id);
        if (saved == null) { saved = new VehicleUpgradeSave { id = id }; vehicleUpgrades.Add(saved); }
        saved.level = expectedLevel + 1;
        if (voucher) freeUpgradeVouchers--;
        else cash -= price;
        return true;
    }

    public bool TryUpgradeStamina(int price, int expectedLevel, int maxLevel)
    {
        bool voucher = price > 0 && freeUpgradeVouchers > 0;
        if (price < 0 || (!voucher && cash < price) || expectedLevel < 0 || staminaUpgradeLevel != expectedLevel ||
            staminaUpgradeLevel >= maxLevel) return false;
        if (voucher) freeUpgradeVouchers--;
        else cash -= price;
        staminaUpgradeLevel++;
        return true;
    }

    public bool TryUpgradeShovel(int price, int expectedLevel, int maxLevel)
    {
        bool voucher = price > 0 && freeUpgradeVouchers > 0;
        if (price < 0 || (!voucher && cash < price) || expectedLevel < 0 || shovelUpgradeLevel != expectedLevel ||
            shovelUpgradeLevel >= maxLevel) return false;
        if (voucher) freeUpgradeVouchers--;
        else cash -= price;
        shovelUpgradeLevel++;
        return true;
    }
    [Header("Data")]
    public string playerName;
    public bool tutorialPassed;
    public bool newGameDone;
    [Space, Header("Career")]
    public int cash = 0;
    public int currentCarFromOwnedSelected;

    [Space, Header("Owned products")]
    public List<OwnedProductData> ownedProducts = new List<OwnedProductData>();

    public int GetOwnedProductCount(string productID)
    {
        if (string.IsNullOrWhiteSpace(productID)) return 0;
        var owned = ownedProducts?.Find(product => product != null && product.productID == productID);
        return Math.Max(0, owned?.ownCount ?? 0);
    }

    public bool TryTakeProduct(string productID)
    {
        if (string.IsNullOrWhiteSpace(productID)) return false;
        var owned = ownedProducts?.Find(product => product != null && product.productID == productID);
        if (owned == null || owned.ownCount <= 0) return false;
        owned.ownCount--;
        return true;
    }

    public bool TryBuyProduct(string productID, int price)
    {
        if (string.IsNullOrWhiteSpace(productID) || price < 0 || cash < price) return false;
        var owned = ownedProducts?.Find(product => product != null && product.productID == productID);
        if (owned != null && (owned.ownCount < 0 || owned.ownCount == int.MaxValue)) return false;

        ownedProducts ??= new List<OwnedProductData>();
        if (owned == null)
        {
            owned = new OwnedProductData { productID = productID };
            ownedProducts.Add(owned);
        }
        owned.ownCount++;
        cash -= price;
        return true;
    }

    [Space, Header("Goals")]
    public GoalSettingsSave[] goals = Array.Empty<GoalSettingsSave>();

    [Space, Header("Settings")]
    public bool haptics = true;
    public bool autoGear = true;
    public bool dlss = false;
    
    
   // [Space, Header("Achievements")] 
    //public List<AchievementStatus> achievements = new List<AchievementStatus>();
}

[Serializable]
public class OwnedProductData
{
    public string productID;
    public int ownCount;
}

[Serializable]
public class VehicleUpgradeSave
{
    public string id;
    public int level;
}

[Serializable]
public class ActivityData
{
    public int progress;
    #if UNITY_PS5
    public SerializableDictionary<ActivityType, bool> levelsCompleted = new SerializableDictionary<ActivityType, bool>();
    [Space] 
    public bool allActivitiesCompleted;
    public bool AllActivitiesCompleted()
    {
        foreach (var kvp in levelsCompleted)
        {
            if (!kvp.Value) return false;
        }

        allActivitiesCompleted = true;
        return true;
    }
#endif

}

[Serializable]
public class SettingsData
{
    public int resolutionIndex = -1;
    public int resolutionWidth;
    public int resolutionHeight;
    public int windowModeIndex;
    public int displayIndex = -1;
    public string displayName;
    public bool vSync;
    public int frameRateIndex = 3;
    public int qualityIndex = -1;

    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float effectsVolume = 1f;

    public float aimSensitivity = 1f;
    public string inputBindingOverrides;
}


[Serializable]
public class NeonData
{
    public bool enabledNeon = true;
    public SerializableColor color;
    public bool blinked;
}
[Serializable]
public class WheelTuningCarData
{
    public float suspensionDistance = 0.1f;
    public float camberAngle = 0f;
    public float widthDistance = 0;

}

[Serializable]
public class SerializableColor
{
    public float r, g, b;
}
public enum PaintMaterialType
{
    Gloss,
    Matte,
    Pearlescent
}
