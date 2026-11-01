using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
public static class InspectCompilation {
 public static object Main() {
 var type=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.TestTools.TestRunner.EditModeRunner")).FirstOrDefault(t=>t!=null);
 return new { EditorApplication.isCompiling, EditorApplication.isUpdating, runners=type==null?new string[0]:Resources.FindObjectsOfTypeAll(type).Select(o=>string.Join(";",type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(f=>f.FieldType==typeof(bool)).Select(f=>f.Name+"="+f.GetValue(o)))).ToArray() };
 }
}
