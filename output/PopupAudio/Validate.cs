var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/AudioManager.prefab");
var data=new UnityEditor.SerializedObject(prefab.GetComponent<AudioManager>());
var result=new System.Text.StringBuilder();
foreach(string name in new[]{"confirmClips","cancelClips"}){
var array=data.FindProperty(name);
if(array.arraySize==0 || array.GetArrayElementAtIndex(0).objectReferenceValue==null)throw new System.Exception("Missing "+name);
result.AppendLine(name+": "+UnityEditor.AssetDatabase.GetAssetPath(array.GetArrayElementAtIndex(0).objectReferenceValue));
}
if(data.FindProperty("otherAudioSource").objectReferenceValue==null)throw new System.Exception("Missing UI audio source");
result.AppendLine("UI audio source assigned.");
System.IO.File.WriteAllText("output/PopupAudio/validation.txt",result.ToString());
return result.ToString();
