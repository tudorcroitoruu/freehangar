using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public sealed class AchievementDebugWindow : EditorWindow
{
    private Vector2 scroll;
    [MenuItem("Tools/Freehangar/Achievements")]
    public static void Open()=>GetWindow<AchievementDebugWindow>("Achievements");
    private void OnInspectorUpdate()=>Repaint();
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Live saved achievement progress. Completed rows and completion Console logs are green. Steam unlocks stay disabled until a real App ID is configured.",MessageType.Info);
        var settings=Resources.Load<SteamAchievementSettings>("SteamAchievementSettings");
        EditorGUILayout.ObjectField("Steam configuration",settings,typeof(SteamAchievementSettings),false);
        var bridge=FindFirstObjectByType<SteamAchievementSync>();
        EditorGUILayout.LabelField("Steam",bridge!=null?bridge.Status:settings!=null && settings.HasRealAppId?"Waiting for Play mode":"Not configured; local achievements active");
        if(SaveSystem.Data==null)EditorGUILayout.HelpBox("Enter Play mode and load a profile to see its counters.",MessageType.Info);
        scroll=EditorGUILayout.BeginScrollView(scroll);
        var data=SaveSystem.Data;var state=data!=null?AchievementRules.State(data):new AchievementProgress();
        foreach(var definition in AchievementRules.Definitions)
        {
            bool done=state.unlocked.Contains(definition.index);var previous=GUI.contentColor;
            GUI.contentColor=done?new Color(.3f,1f,.45f):previous;
            using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"[{definition.index}] {definition.title}  {(done?"DONE":"IN PROGRESS")}",EditorStyles.boldLabel);
                EditorGUILayout.LabelField(definition.description,EditorStyles.wordWrappedLabel);
                double target=AchievementRules.Target(state,definition.index),current=AchievementRules.Current(data,definition.index);
                EditorGUILayout.LabelField("Progress",target>0?$"{current:N0} / {target:N0}":"Waiting for Gameplay discovery catalog");
                EditorGUILayout.LabelField("Steam API",settings!=null?settings.ApiName(definition.index):definition.SteamApiName);
            }
            GUI.contentColor=previous;
        }
        EditorGUILayout.EndScrollView();
        if(GUILayout.Button("Validate achievement rules"))Validate();
    }
    [MenuItem("Tools/Freehangar/Validate achievements")]
    public static void Validate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("Leave Play mode before running achievement tests.");return;}
        var api=CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());
        api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{"GameAchievementTests"}}){runSynchronously=true});
        DestroyImmediate(api);
    }
    [MenuItem("Tools/Freehangar/Validate achievement catalog")]
    public static void ValidateCatalog()
    {
        var report=new System.Text.StringBuilder();var keys=new System.Collections.Generic.HashSet<string>();var chests=new System.Collections.Generic.HashSet<string>();var hangars=new System.Collections.Generic.HashSet<int>();
        foreach(var find in FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(find.FindKind==BuriedFind.Kind.VehicleKey)keys.Add(find.FindId);
            if(find.FindKind==BuriedFind.Kind.CashChest)chests.Add(find.FindId);
        }
        var resolve=typeof(GameAchievements).GetMethod("Hangar",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        foreach(var pile in FindObjectsByType<GrainPile>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            int number=(int)resolve.Invoke(null,new object[]{pile});if(number>0)hangars.Add(number);
        }
        report.AppendLine($"Live Gameplay catalog: {keys.Count} vehicle keys, {chests.Count} chests, {hangars.Count} hangars.");
        foreach(string key in keys)report.AppendLine("Vehicle key: "+key);foreach(string chest in chests)report.AppendLine("Chest: "+chest);foreach(int hangar in hangars)report.AppendLine("Hangar: "+hangar);
        Directory.CreateDirectory("output/Achievements");File.WriteAllText("output/Achievements/catalog.txt",report.ToString());Debug.Log(report.ToString());
    }    private sealed class Results:ICallbacks
    {
        public void RunStarted(ITestAdaptor test){}public void TestStarted(ITestAdaptor test){}public void TestFinished(ITestResultAdaptor result){}
        public void RunFinished(ITestResultAdaptor result)
        {
            var report=new System.Text.StringBuilder($"Achievement tests: {result.PassCount} passed, {result.FailCount} failed.\n");Append(result,report);
            Directory.CreateDirectory("output/Achievements");File.WriteAllText("output/Achievements/tests.txt",report.ToString());Debug.Log(report.ToString());
        }
        private static void Append(ITestResultAdaptor result,System.Text.StringBuilder report)
        {
            if(result.TestStatus==TestStatus.Failed)report.AppendLine(result.Name+": "+result.Message);
            foreach(var child in result.Children)Append(child,report);
        }
    }
}
