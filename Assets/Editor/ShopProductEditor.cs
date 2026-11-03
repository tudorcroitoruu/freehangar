using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(ShopProduct))]
public sealed class ShopProductEditor : Editor
{
    private void OnSceneGUI()
    {
        serializedObject.Update();
        if (serializedObject.FindProperty("deliveryMode").enumValueIndex != (int)ShopProduct.DeliveryMode.AtSpawnPoint) return;
        var point = serializedObject.FindProperty("spawnPoint").objectReferenceValue as Transform;
        if (point == null)
        {
            Handles.Label(((ShopProduct)target).transform.position, "Assign Spawn Point to display the delivery area");
            return;
        }

        Vector3 size = serializedObject.FindProperty("spawnAreaSize").vector3Value;
        size = new Vector3(Mathf.Max(.01f, size.x), Mathf.Max(.01f, size.y), Mathf.Max(.01f, size.z));
        Vector3 offset = serializedObject.FindProperty("spawnAreaOffset").vector3Value;
        Vector3 center = point.position + point.rotation * offset;
        CompareFunction previousDepth = Handles.zTest;
        try
        {
            // Keep the selected delivery volume visible even behind the shop's meshes.
            Handles.zTest = CompareFunction.Always;
            using (new Handles.DrawingScope(Color.cyan, Matrix4x4.TRS(center, point.rotation, Vector3.one)))
                Handles.DrawWireCube(Vector3.zero, size);
            using (new Handles.DrawingScope(Color.yellow, Matrix4x4.identity))
            {
                Handles.DrawLine(point.position, center);
                Handles.SphereHandleCap(0, point.position, Quaternion.identity, .15f, EventType.Repaint);
                Handles.Label(center + Vector3.up * (size.y * .5f + .15f),
                    $"{serializedObject.FindProperty("productName").stringValue} - Spawn area ({size.x:0.##}, {size.y:0.##}, {size.z:0.##})");
            }
        }
        finally { Handles.zTest = previousDepth; }
    }
}
