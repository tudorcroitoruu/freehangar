using System.Linq;
using UnityEngine;
using UnityEditor;
public static class InspectDrivingHints
{
    public static object Main()
    {
        return Object.FindObjectsByType<GameplayCanvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(canvas => {
            var so = new SerializedObject(canvas);
            return new { canvas.name, playing=Application.isPlaying,
                walking=Names(so.FindProperty("walkingHints")), driving=Names(so.FindProperty("drivingHints")) };
        }).ToArray();
    }
    static string[] Names(SerializedProperty array) => Enumerable.Range(0,array.arraySize)
        .Select(i=>array.GetArrayElementAtIndex(i).objectReferenceValue?.name ?? "MISSING").ToArray();
}
