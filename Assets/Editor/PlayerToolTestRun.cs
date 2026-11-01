using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class PlayerToolTestRun
{
    private static TestRunnerApi api;
    [MenuItem("Tools/Player/Validate discovered tool interactions")]
    public static void Run()
    {
        api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());
        api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{"PlayerToolInteractionTests","PlayerPerksTests"}}));
    }
    private sealed class Results:ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun){}
        public void TestStarted(ITestAdaptor test){}
        public void TestFinished(ITestResultAdaptor result){}
        public void RunFinished(ITestResultAdaptor result)
        {
            var text=new System.Text.StringBuilder($"Tool and chest perk tests: {result.PassCount} passed, {result.FailCount} failed.\n");Append(result,text);
            Directory.CreateDirectory("output/PlayerTools");File.WriteAllText("output/PlayerTools/tests.txt",text.ToString());Debug.Log(text.ToString());
            EditorApplication.delayCall+=()=>{if(api!=null)Object.DestroyImmediate(api);};
        }
        private static void Append(ITestResultAdaptor result,System.Text.StringBuilder text)
        {
            if(result.TestStatus==TestStatus.Failed)text.AppendLine(result.Name+": "+result.Message);
            foreach(var child in result.Children)Append(child,text);
        }
    }
}
