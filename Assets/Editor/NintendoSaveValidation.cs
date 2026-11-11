using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

public static class NintendoSaveValidation
{
    public static void RunFocusedTests()
    {
        var active = typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi).GetMethod("IsRunActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (active != null && (bool)active.Invoke(null, null)) throw new InvalidOperationException("The Unity test runner is still active; refusing to overlap tests.");
        var results = new System.Collections.Generic.List<string>();
        var fixture = new SaveBridgeTests();
        foreach (var method in typeof(SaveBridgeTests).GetMethods())
        {
            var cases = method.GetCustomAttributes(typeof(NUnit.Framework.TestCaseAttribute), false).Cast<NUnit.Framework.TestCaseAttribute>().ToArray();
            if (cases.Length > 0)
                foreach (var test in cases) RunCheck(results, method.Name, () => method.Invoke(fixture, test.Arguments));
            else if (method.IsDefined(typeof(NUnit.Framework.TestAttribute), false)) RunCheck(results, method.Name, () => method.Invoke(fixture, null));
        }
        RunCheck(results, "Finished run integration", () => new WorldSaveTests().FinishingRunRemovesContinueAndBackupButPreservesSettings());
        Directory.CreateDirectory("output/SaveSystem/Switch");
        File.WriteAllLines("output/SaveSystem/Switch/tests.txt", results);
        Debug.Log(string.Join("\n", results));
        if (results.Any(r => r.StartsWith("FAIL"))) throw new Exception("Nintendo save validation failed.");
    }
    static void RunCheck(System.Collections.Generic.List<string> results, string name, Action test)
    {
        try { test(); results.Add("PASS " + name); }
        catch (Exception e) { results.Add("FAIL " + name + ": " + e.GetBaseException().Message); }
    }
    public static void CompileSwitchScripts()
    {
        Directory.CreateDirectory("output/SaveSystem/Switch");
        var settings = new ScriptCompilationSettings { target = BuildTarget.Switch, group = BuildTargetGroup.Switch, options = ScriptCompilationOptions.None };
        var result = PlayerBuildInterface.CompilePlayerScripts(settings, "Temp/SwitchSaveScriptValidation");
        if (result.assemblies == null || !result.assemblies.Any())
            throw new InvalidOperationException("Switch player scripts did not compile; inspect Console errors.");
        string report = "Switch player C# compilation succeeded:\n" + string.Join("\n", result.assemblies);
        File.WriteAllText("output/SaveSystem/Switch/compilation.txt", report);
        Debug.Log(report);
    }
}
