using System;using UnityEngine;using UnityEditor;
public static class SharedMenuSaveSnapshot {
 [Serializable]public class Snapshot { public string[] keys;public string[] values;public bool[] exists; }
 public static object Main(){
 if(Application.isPlaying)throw new Exception("Snapshot only outside Play Mode");
 const string slot="SharedMenuSmoke.SaveBackup";
 string backup=SessionState.GetString(slot,"");
 if(string.IsNullOrEmpty(backup)){
 var snapshot=new Snapshot{keys=new[]{"/save","/save_Settings","levelsCompleted"},values=new string[3],exists=new bool[3]};
 for(int i=0;i<3;i++){snapshot.exists[i]=PlayerPrefs.HasKey(snapshot.keys[i]);snapshot.values[i]=PlayerPrefs.GetString(snapshot.keys[i],"");}
 SessionState.SetString(slot,JsonUtility.ToJson(snapshot));return "Save snapshot stored";
 }
 var saved=JsonUtility.FromJson<Snapshot>(backup);for(int i=0;i<saved.keys.Length;i++){if(saved.exists[i])PlayerPrefs.SetString(saved.keys[i],saved.values[i]);else PlayerPrefs.DeleteKey(saved.keys[i]);}PlayerPrefs.Save();SessionState.EraseString(slot);return "Original save restored";
 }
}
