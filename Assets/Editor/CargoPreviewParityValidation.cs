using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
public static class CargoPreviewParityValidation
{
    [MenuItem("Tools/Vehicles/Validate cargo preview parity")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());
        api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{"CargoPreviewParityTests","VehicleCargoPlacementTests","ContainerPlacementTests"}}){runSynchronously=true});
        Object.DestroyImmediate(api);
    }
    private sealed class Results:ICallbacks
    {
        private readonly System.Text.StringBuilder details=new System.Text.StringBuilder();
        public void RunStarted(ITestAdaptor tests){}
        public void TestStarted(ITestAdaptor test){}
        public void TestFinished(ITestResultAdaptor result){if(result.Test.IsSuite)return;details.AppendLine(result.Test.FullName+": "+result.ResultState+"\n"+result.Message+"\n"+result.StackTrace);}
        public void RunFinished(ITestResultAdaptor result)
        {
            string report=$"Cargo parity: {result.ResultState}; passed={result.PassCount}; failed={result.FailCount}\n"+details;
            Directory.CreateDirectory("output/Cargo");File.WriteAllText("output/Cargo/preview-parity-tests.txt",report);Debug.Log(report);
        }
    }
}
