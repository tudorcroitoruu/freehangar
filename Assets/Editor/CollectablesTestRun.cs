using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class CollectablesTestRun
{
    private static TestRunnerApi api;
    [InitializeOnLoadMethod]
    private static void CheckRequest() => EditorApplication.delayCall += () =>
    {
        const string request = "output/Collectables/tests.request";
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        Run();
    };

    [MenuItem("Tools/Grain/Validate collectable progress")]
    public static void Run()
    {
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Results());
        api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            testNames = new[] { "CollectablesManagerTests", "WarningPopupTests", "MoneyChangePopupTests", "PlayerPerksTests" }
        }));
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            Directory.CreateDirectory("output/Collectables");
            TestRunnerApi.SaveResultToFile(result, "output/Collectables/tests.xml");
            Debug.Log($"Collectable progress tests: {result.PassCount} passed, {result.FailCount} failed.");
            EditorApplication.delayCall += () => { if (api != null) Object.DestroyImmediate(api); };
        }
    }
}
