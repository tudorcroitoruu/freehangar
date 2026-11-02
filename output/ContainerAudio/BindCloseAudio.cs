var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>("Assets/Audio/Gameplay/Controller/Close Bag.wav");
if (clip == null) throw new System.Exception("Close Bag audio missing");
var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/Prefabs/AudioManager.prefab");
try {
 var audio = root.GetComponentInChildren<GameplayAudio>(true);
 var serialized = new UnityEditor.SerializedObject(audio);
 var property = serialized.FindProperty("closeBag");
 if (property == null) throw new System.Exception("Waiting for updated audio script compilation");
 property.objectReferenceValue = clip;
 serialized.ApplyModifiedPropertiesWithoutUndo();
 UnityEditor.PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/AudioManager.prefab");
} finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
var saved = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/AudioManager.prefab");
var assigned = new UnityEditor.SerializedObject(saved.GetComponentInChildren<GameplayAudio>(true)).FindProperty("closeBag").objectReferenceValue == clip;
return new { assigned, compilationFailed = UnityEditor.EditorUtility.scriptCompilationFailed };
