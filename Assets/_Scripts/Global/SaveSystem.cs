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
    public static int Money => Data != null ? Data.cash : 0;

    public static bool TryPurchaseShovelUpgrade(int price, int expectedLevel, int maxLevel)
    {
        if (Data == null || !Data.TryUpgradeShovel(price, expectedLevel, maxLevel)) return false;
        SaveData();
        return true;
    }

    public static bool TryPurchaseStaminaUpgrade(int price, int expectedLevel, int maxLevel)
    {
        if (Data == null || !Data.TryUpgradeStamina(price, expectedLevel, maxLevel)) return false;
        SaveData();
        return true;
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
        SaveData();
        return true;
    }

    public static bool AddMoney(int amount)
    {
        if (Data == null || amount <= 0 || Data.cash > int.MaxValue - amount) return false;
        Data.cash += amount;
        SaveData();
        return true;
    }

    public static bool SpendMoney(int amount)
    {
        if (Data == null || amount <= 0 || Data.cash < amount) return false;
        Data.cash -= amount;
        SaveData();
        return true;
    }

    public static bool TryBuyProduct(string productID, int price)
    {
        if (Data == null || !Data.TryBuyProduct(productID, price)) return false;
        SaveData();
        OwnedProductsChanged?.Invoke();
        return true;
    }

    public static bool TryTakeProduct(string productID)
    {
        if (Data == null || !Data.TryTakeProduct(productID)) return false;
        SaveData();
        OwnedProductsChanged?.Invoke();
        return true;
    }

    static string savePath = "/save";

    static int key = 5;
    
    public static void SaveData()
    {
        string json = JsonUtility.ToJson(Data); //Global Data
        string jsonSettings = JsonUtility.ToJson(settingsData); //Settings Data (on new save it willhave same settings)
        string jsonActivity = JsonUtility.ToJson(activityData); //Activity Data (PS5)
        
        PlayerPrefs.SetString(savePath, EncryptDecrypt(json));
        PlayerPrefs.SetString(savePath +"_Settings", EncryptDecrypt(jsonSettings));
        PlayerPrefs.SetString("levelsCompleted", EncryptDecrypt(jsonActivity));
        
        PlayerPrefs.Save();
        MoneyChanged?.Invoke(Money);
        Debug.Log("Saved Data");
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
                
                Data = JsonUtility.FromJson<PlayerData>(EncryptDecrypt(json));
                settingsData = JsonUtility.FromJson<SettingsData>(EncryptDecrypt(jsonSettings));
                activityData = JsonUtility.FromJson<ActivityData>(EncryptDecrypt(jsonActivity));
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
            await MakeNewSave();
            SaveData();
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

        SaveData();
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
    [Header("Grain progression")]
    public double grainBaggedKilograms;
    public string[] foundSecrets = Array.Empty<string>();
    public int freeUpgradeVouchers;
    public long infiniteStaminaUntilUtc;
    public int[] wheelbarrowUpgradeLevels = new int[3];
    public int shovelUpgradeLevel;
    public int staminaUpgradeLevel;

    public bool TryUpgradeStamina(int price, int expectedLevel, int maxLevel)
    {
        if (price < 0 || cash < price || expectedLevel < 0 || staminaUpgradeLevel != expectedLevel ||
            staminaUpgradeLevel >= maxLevel) return false;
        cash -= price;
        staminaUpgradeLevel++;
        return true;
    }

    public bool TryUpgradeShovel(int price, int expectedLevel, int maxLevel)
    {
        if (price < 0 || cash < price || expectedLevel < 0 || shovelUpgradeLevel != expectedLevel ||
            shovelUpgradeLevel >= maxLevel) return false;
        cash -= price;
        shovelUpgradeLevel++;
        return true;
    }
    [Header("Data")]
    public string playerName;
    public bool tutorialPassed;
    public bool newGameDone;
    [Space, Header("Career")]
    public int cash = 150000;
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
    public float musicVolume = 1f;  
    public float effectsVolume = 1f;
    public float masterVolume = 1f;
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
