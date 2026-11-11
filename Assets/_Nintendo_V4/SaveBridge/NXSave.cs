using System;
using System.IO;
using UnityEngine;

/// <summary>Raw byte storage in the selected Nintendo user's save archive.</summary>
public class NXSave : MonoBehaviour
{
#if UNITY_SWITCH && !UNITY_EDITOR
    private const string MountName = "datafarm";
    private const string FilePath = MountName + ":/savefiledatafarm";
    private const double MinimumDelayBetweenSaves = 15;
    private static bool mounted;
    private static double lastWriteAttempt = double.NegativeInfinity;
    public static bool CanSave => Time.realtimeSinceStartupAsDouble - lastWriteAttempt >= MinimumDelayBetweenSaves;

    public static void Initialize()
    {
        if (mounted) return;
        NXScript.Initialize();
        var user = NXScript.GetUserID();
        var result = nn.fs.SaveData.Ensure(user);
        if (!result.IsSuccess()) throw new IOException("Could not prepare Nintendo save archive: " + result);
        result = nn.fs.SaveData.Mount(MountName, user);
        if (!result.IsSuccess()) throw new IOException("Could not mount Nintendo save archive: " + result);
        mounted = true;
    }

    public static byte[] Load(ref bool successful)
    {
        successful = false;
        Initialize();
        var handle = new nn.fs.FileHandle();
        var result = nn.fs.File.Open(ref handle, FilePath, nn.fs.OpenFileMode.Read);
        if (!result.IsSuccess())
        {
            if (nn.fs.FileSystem.ResultPathNotFound.Includes(result))
            {
                successful = true; // New profile, distinct from a failed or empty read.
                return null;
            }
            throw new IOException("Could not open Nintendo save: " + result);
        }
        try
        {
            long size = 0;
            result = nn.fs.File.GetSize(ref size, handle);
            if (!result.IsSuccess()) throw new IOException("Could not size Nintendo save: " + result);
            if (size < 0 || size > int.MaxValue) throw new InvalidDataException("Invalid Nintendo save size.");
            var bytes = new byte[(int)size];
            result = nn.fs.File.Read(handle, 0, bytes, size);
            if (!result.IsSuccess()) throw new IOException("Could not read Nintendo save: " + result);
            successful = true;
            return bytes;
        }
        finally { nn.fs.File.Close(handle); }
    }

    public static bool Save(byte[] saveData, bool ignoreFrequencyLimit)
    {
        if (saveData == null) throw new ArgumentNullException(nameof(saveData));
        if (saveData.LongLength > 32L * 1024 * 1024)
            throw new InvalidDataException("Save archive exceeds the 32 MiB budget. Existing save has not been overwritten.");
        Initialize();
        double now = Time.realtimeSinceStartupAsDouble;
        if (!ignoreFrequencyLimit && now - lastWriteAttempt < MinimumDelayBetweenSaves) return false;
        lastWriteAttempt = now;
        UnityEngine.Switch.Notification.EnterExitRequestHandlingSection();
        try { return WriteAndCommit(saveData); }
        finally { UnityEngine.Switch.Notification.LeaveExitRequestHandlingSection(); }
    }

    private static bool WriteAndCommit(byte[] bytes)
    {
        var handle = new nn.fs.FileHandle();
        var result = nn.fs.File.Open(ref handle, FilePath, nn.fs.OpenFileMode.Write);
        if (nn.fs.FileSystem.ResultPathNotFound.Includes(result))
        {
            result = nn.fs.File.Create(FilePath, bytes.LongLength);
            if (!Succeeded(result, "create")) return false;
            result = nn.fs.File.Open(ref handle, FilePath, nn.fs.OpenFileMode.Write);
        }
        if (!Succeeded(result, "open for writing")) return false;
        try
        {
            result = nn.fs.File.SetSize(handle, bytes.LongLength);
            if (!Succeeded(result, "resize")) return false;
            result = nn.fs.File.Write(handle, 0, bytes, bytes.LongLength, nn.fs.WriteOption.Flush);
            if (!Succeeded(result, "write")) return false;
        }
        finally { nn.fs.File.Close(handle); }
        // Closing the file and successfully committing are both required for durability.
        return Succeeded(nn.fs.FileSystem.Commit(MountName), "commit");
    }

    private static bool Succeeded(nn.Result result, string operation)
    {
        if (result.IsSuccess()) return true;
        Debug.LogError("Nintendo save failed to " + operation + ": " + result);
        return false;
    }
#endif
}
