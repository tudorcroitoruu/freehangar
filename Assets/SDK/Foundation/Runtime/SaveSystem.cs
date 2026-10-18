using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class SaveSystem
{
    [Serializable]
    private sealed class EncryptedEnvelope
    {
        public int version;
        public string iv;
        public string ciphertext;
        public string authentication;
    }

    [Serializable]
    private sealed class LegacyEnvelope
    {
        public string payload;
        public string checksum;
    }

    [Serializable]
    private sealed class SavePayload
    {
        public PlayerData player;
        public SDKInternalState sdk;
    }

    private const int EncryptionVersion = 2;
    private const string FileName = "player_data.json";
    private const string KeyPurpose = "SDKFoundation.SaveSystem.AES256.HMACSHA256.v2";
    public static PlayerData Data { get; private set; } = new();
    internal static SDKInternalState State { get; private set; } = new();
    private static string PathName => Path.Combine(Application.persistentDataPath, FileName);
    private static string BackupPath => PathName + ".bak";

    public static PlayerData Load()
    {
        bool loadedLegacy;
        if (TryLoad(PathName, out var player, out var state, out loadedLegacy) ||
            TryLoad(BackupPath, out player, out state, out loadedLegacy))
        {
            Data = player;
            State = state;
            Normalize();
            if (loadedLegacy) MigrateLegacySave();
            return Data;
        }

        Data = new PlayerData();
        State = new SDKInternalState { firstLaunchUtc = DateTime.UtcNow.ToString("O") };
        Save();
        return Data;
    }

    public static bool Save()
    {
        try
        {
            string json = CreateEncryptedJson();
            string temporary = PathName + ".tmp";
            File.WriteAllText(temporary, json, Encoding.UTF8);
            if (File.Exists(PathName)) File.Copy(PathName, BackupPath, true);
            File.Copy(temporary, PathName, true);
            File.Delete(temporary);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("[SaveSystem] Save failed: " + exception);
            return false;
        }
    }

    private static bool TryLoad(
        string path,
        out PlayerData player,
        out SDKInternalState state,
        out bool loadedLegacy)
    {
        player = null;
        state = null;
        loadedLegacy = false;
        if (!File.Exists(path)) return false;

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            var envelope = JsonUtility.FromJson<EncryptedEnvelope>(json);

            string payload;
            if (envelope != null &&
                envelope.version == EncryptionVersion &&
                !string.IsNullOrEmpty(envelope.iv) &&
                !string.IsNullOrEmpty(envelope.ciphertext) &&
                !string.IsNullOrEmpty(envelope.authentication))
            {
                payload = Decrypt(envelope);
            }
            else
            {
                var legacy = JsonUtility.FromJson<LegacyEnvelope>(json);
                if (legacy == null ||
                    string.IsNullOrEmpty(legacy.payload) ||
                    !ConstantTimeEquals(legacy.checksum, LegacyHash(legacy.payload)))
                    return false;
                payload = legacy.payload;
                loadedLegacy = true;
            }

            if (payload.Contains("\"player\""))
            {
                var savePayload = JsonUtility.FromJson<SavePayload>(payload);
                player = savePayload?.player;
                state = savePayload?.sdk;
            }
            else
            {
                player = JsonUtility.FromJson<PlayerData>(payload);
                state = JsonUtility.FromJson<SDKInternalState>(payload);
                loadedLegacy = true;
            }

            return player != null && state != null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[SaveSystem] Ignoring invalid or unreadable save " + path + ": " + exception.Message);
            return false;
        }
    }

    private static string CreateEncryptedJson()
    {
        var savePayload = new SavePayload { player = Data, sdk = State };
        byte[] plaintext = Encoding.UTF8.GetBytes(JsonUtility.ToJson(savePayload));
        DeriveKeys(out byte[] encryptionKey, out byte[] authenticationKey);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = encryptionKey;
        aes.GenerateIV();
        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        var envelope = new EncryptedEnvelope
        {
            version = EncryptionVersion,
            iv = Convert.ToBase64String(aes.IV),
            ciphertext = Convert.ToBase64String(ciphertext),
            authentication = Convert.ToBase64String(Authenticate(authenticationKey, aes.IV, ciphertext))
        };
        return JsonUtility.ToJson(envelope, true);
    }

    private static string Decrypt(EncryptedEnvelope envelope)
    {
        byte[] iv = Convert.FromBase64String(envelope.iv);
        byte[] ciphertext = Convert.FromBase64String(envelope.ciphertext);
        byte[] suppliedAuthentication = Convert.FromBase64String(envelope.authentication);
        DeriveKeys(out byte[] encryptionKey, out byte[] authenticationKey);
        byte[] expectedAuthentication = Authenticate(authenticationKey, iv, ciphertext);
        if (!ConstantTimeEquals(suppliedAuthentication, expectedAuthentication))
            throw new CryptographicException("Save authentication failed.");

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = encryptionKey;
        aes.IV = iv;
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        byte[] plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return Encoding.UTF8.GetString(plaintext);
    }

    private static void DeriveKeys(out byte[] encryptionKey, out byte[] authenticationKey)
    {
        string material = KeyPurpose + "|" + Application.identifier + "|" + SystemInfo.deviceUniqueIdentifier;
        using var sha = SHA512.Create();
        byte[] keyMaterial = sha.ComputeHash(Encoding.UTF8.GetBytes(material));
        encryptionKey = new byte[32];
        authenticationKey = new byte[32];
        Buffer.BlockCopy(keyMaterial, 0, encryptionKey, 0, 32);
        Buffer.BlockCopy(keyMaterial, 32, authenticationKey, 0, 32);
    }

    private static byte[] Authenticate(byte[] key, byte[] iv, byte[] ciphertext)
    {
        byte[] data = new byte[sizeof(int) + iv.Length + ciphertext.Length];
        Buffer.BlockCopy(BitConverter.GetBytes(EncryptionVersion), 0, data, 0, sizeof(int));
        Buffer.BlockCopy(iv, 0, data, sizeof(int), iv.Length);
        Buffer.BlockCopy(ciphertext, 0, data, sizeof(int) + iv.Length, ciphertext.Length);
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    private static void MigrateLegacySave()
    {
        try
        {
            string encrypted = CreateEncryptedJson();
            string temporary = PathName + ".tmp";
            File.WriteAllText(temporary, encrypted, Encoding.UTF8);
            File.Copy(temporary, PathName, true);
            File.Copy(temporary, BackupPath, true);
            File.Delete(temporary);
            Debug.Log("[SaveSystem] Legacy plaintext save migrated to encrypted format.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[SaveSystem] Save encryption migration failed: " + exception);
        }
    }

    private static void Normalize()
    {
        Data ??= new PlayerData();
        State ??= new SDKInternalState();
        State.processedTransactions ??= new List<string>();
        State.progressGates ??= new List<string>();
        if (string.IsNullOrEmpty(State.firstLaunchUtc)) State.firstLaunchUtc = DateTime.UtcNow.ToString("O");
    }

    private static string LegacyHash(string value)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static bool ConstantTimeEquals(string left, string right)
    {
        if (left == null || right == null) return false;
        return ConstantTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    }

    private static bool ConstantTimeEquals(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length) return false;
        int difference = 0;
        for (int index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
        return difference == 0;
    }
}

[Serializable]
internal sealed class SDKInternalState
{
    public int schemaVersion = 1;
    public string firstLaunchUtc;
    public int launchCount;
    public int highestCompletedLevel;
    public int currentLevel;
    public bool firstLoseTracked;
    public bool firstPurchaseTracked;
    public bool adsRemoved;
    public bool hapticsDisabled;
    public double totalIapSpendUsd;
    public List<string> processedTransactions = new();
    public List<string> progressGates = new();
}
