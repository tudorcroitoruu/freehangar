var previousData = SaveSystem.Data;
var previousLoaded = SaveSystem.savesLoaded;
var results = new System.Collections.Generic.List<string>();
try {
 SaveSystem.Data = new PlayerData(); SaveSystem.savesLoaded = false;
 var type = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("WorldSaveTests")).First(t => t != null);
 foreach (var method in type.GetMethods().Where(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "TestAttribute"))) {
  try { method.Invoke(System.Activator.CreateInstance(type), null); results.Add("PASS " + method.Name); }
  catch (System.Exception e) { results.Add("FAIL " + method.Name + ": " + (e.InnerException ?? e).ToString()); }
 }
} finally { SaveSystem.Data = previousData; SaveSystem.savesLoaded = previousLoaded; }
System.IO.File.WriteAllLines("output/SaveSystem/roundtrip-tests.txt", results);
return results;
