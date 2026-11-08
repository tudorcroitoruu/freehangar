using UnityEditor;
using UnityEngine;

/// <summary>Shows the same world-space anchor + offset used by interaction prompts.</summary>
public static class InteractionPromptGizmos
{
    [DrawGizmo(GizmoType.Selected)]
    private static void DrawShovel(ShovelUpgradeInteractable target, GizmoType _) => Draw(target, "Shovel upgrade prompt");

    [DrawGizmo(GizmoType.Selected)]
    private static void DrawStamina(StaminaUpgradeInteractable target, GizmoType _) => Draw(target, "Stamina upgrade prompt");

    [DrawGizmo(GizmoType.Selected)]
    private static void DrawVehicle(VehicleUpgradeInteractable target, GizmoType _) => Draw(target, "Vehicle upgrade prompt");

    [DrawGizmo(GizmoType.Selected)]
    private static void DrawEvent(EventInteractable target, GizmoType _) => Draw(target, "Event prompt");

    [DrawGizmo(GizmoType.Selected)]
    private static void DrawContainer(GrainContainer target, GizmoType _) => Draw(target, "Container prompt");

    [DrawGizmo(GizmoType.Selected)]
    private static void DrawInventoryProduct(InventoryProduct target, GizmoType _) => Draw(target, "Inventory product prompt");

    [DrawGizmo(GizmoType.Selected)]
    private static void DrawShopProduct(ShopProduct target, GizmoType _) => Draw(target, "Shop product prompt");

    private static void Draw(MonoBehaviour target, string label)
    {
        using (var data = new SerializedObject(target))
        {
            var anchor = data.FindProperty("promptAnchor").objectReferenceValue as Transform;
            Vector3 origin = anchor != null ? anchor.position : target.transform.position;
            Vector3 position = origin + data.FindProperty("promptOffset").vector3Value;
            if (target is GrainContainer container)
            {
                Bounds bounds = container.WorldBounds;
                origin = container.IsCarried ? bounds.center : anchor != null ? anchor.position : new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                position = container.PromptPosition;
            }
            Color previous = Gizmos.color;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin, position);
            Gizmos.DrawWireSphere(position, .06f);
            Gizmos.DrawLine(position - Vector3.right * .09f, position + Vector3.right * .09f);
            Gizmos.DrawLine(position - Vector3.up * .09f, position + Vector3.up * .09f);
            Gizmos.DrawLine(position - Vector3.forward * .09f, position + Vector3.forward * .09f);
            Gizmos.color = previous;
            Handles.Label(position + Vector3.up * .12f, label + " (promptOffset)");
        }
    }
}
