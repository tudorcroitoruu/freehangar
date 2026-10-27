using UnityEngine;using UnityEditor;using System.Text;
public static class InspectEntrance {public static string Main(){var c=SceneView.lastActiveSceneView.camera;var s=new StringBuilder();s.AppendLine("camera "+c.pixelWidth+"x"+c.pixelHeight);
foreach(var uv in new[]{new Vector2(.465f,.31f),new Vector2(.43f,.33f),new Vector2(.49f,.29f)}){var r=c.ViewportPointToRay(uv);foreach(var h in Physics.RaycastAll(r,300,~0,QueryTriggerInteraction.Ignore))s.AppendLine(uv+" "+h.collider.name+" "+h.point);}
return s.ToString();}}

