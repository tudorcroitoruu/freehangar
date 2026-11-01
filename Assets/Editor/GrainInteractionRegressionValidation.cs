using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
public static class GrainInteractionRegressionValidation
{
    [MenuItem("Tools/Grain/Validate interaction performance behavior")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());
        api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{"GrainInteractionPerformanceTests"}}){runSynchronously=true});Object.DestroyImmediate(api);
    }
    private sealed class Results:ICallbacks
    {
        private readonly System.Text.StringBuilder details=new System.Text.StringBuilder();
        public void RunStarted(ITestAdaptor test){}public void TestStarted(ITestAdaptor test){}
        public void TestFinished(ITestResultAdaptor result){if(!result.Test.IsSuite)details.AppendLine(result.Test.FullName+": "+result.ResultState+"\n"+result.Message+"\n"+result.StackTrace);}
        public void RunFinished(ITestResultAdaptor result){string report=$"Grain interaction tests: {result.ResultState}; passed={result.PassCount}; failed={result.FailCount}\n"+details;Directory.CreateDirectory("output/GrainPerformance");File.WriteAllText("output/GrainPerformance/tests.txt",report);Debug.Log(report);}
    }
}
