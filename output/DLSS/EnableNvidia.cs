using UnityEditor;
using UnityEditor.PackageManager;
public static class EnableNvidia
{
    public static string Main()
    {
        var request = Client.Add("com.unity.modules.nvidia@1.0.0");
        EditorApplication.CallbackFunction poll = null;
        poll = () => {
            if (!request.IsCompleted) return;
            EditorApplication.update -= poll;
            System.IO.File.WriteAllText("Library/dlss-package-result.txt", request.Status == StatusCode.Success
                ? request.Result.name + "@" + request.Result.version : request.Error.message);
        };
        EditorApplication.update += poll;
        return "NVIDIA module installation requested";
    }
}
