using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class OpeningPresentationValidation
{
    [MenuItem("Tools/Audio/Validate opening presentation")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Results());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] { "OpeningPresentationTests" } }) { runSynchronously = true });
        Object.DestroyImmediate(api);
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            string report = $"Opening presentation: {result.ResultState}; passed={result.PassCount}; failed={result.FailCount}\n{result.Message}\n{result.StackTrace}";
            Directory.CreateDirectory("output/Audio");
            File.WriteAllText("output/Audio/opening-presentation-tests.txt", report);
            Debug.Log(report);
        }
    }
}
