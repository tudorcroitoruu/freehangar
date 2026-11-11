using System.IO;
using UnityEngine;

public static class StartupSaveValidation
{
    public static void Run()
    {
        var runner = ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
        runner.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(new UnityEditor.TestTools.TestRunner.Api.Filter
        {
            testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
            testNames = new[] { "StartupSaveTests.UnreadableSaveReportsStartupFailureWithoutOverwritingIt" }
        }));
    }
}
