using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class VehicleCargoPreview : EditorWindow
{
    private VehicleUpgradeSettings settings;
    private readonly List<GameObject> containers = new List<GameObject>();
    private PreviewRenderUtility preview;
    private GameObject assembly;
    private int vehicleIndex, containerIndex;
    private bool fillRack = true;
    private float cargoFill = 1f, cargoClosure = 1f;
    private Vector2 orbit = new Vector2(135, 20), scroll;
    private float zoom = 1f;
    private Bounds bounds;
    private string result;

    [MenuItem("Tools/Vehicles/Cargo Compatibility Preview")]
    private static void Open() => GetWindow<VehicleCargoPreview>("Vehicle Cargo Preview");
    private void OnEnable()
    {
        settings = Resources.Load<VehicleUpgradeSettings>(VehicleUpgradeSettings.ResourcePath);
        containers.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Gameplay/Containers" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.GetComponentInChildren<GrainContainer>(true) != null) containers.Add(prefab);
        }
        containers.Sort((a,b) => a.GetComponentInChildren<GrainContainer>(true).TransportLevel.CompareTo(b.GetComponentInChildren<GrainContainer>(true).TransportLevel));
    }
    private void OnDisable() { preview?.Cleanup(); preview = null; assembly = null; }
    private void OnGUI()
    {
        settings = (VehicleUpgradeSettings)EditorGUILayout.ObjectField("Vehicle settings", settings, typeof(VehicleUpgradeSettings), false);
        if (settings == null || settings.vehicles.Length == 0 || containers.Count == 0) { EditorGUILayout.HelpBox("Vehicle settings and container prefabs are required.", MessageType.Warning); return; }
        var names = new string[settings.vehicles.Length];
        for (int i=0;i<names.Length;i++) names[i]=settings.vehicles[i].displayName;
        vehicleIndex = Mathf.Clamp(vehicleIndex,0,names.Length-1);
        var cargoNames = containers.ConvertAll(p => p.name).ToArray();
        EditorGUI.BeginChangeCheck();
        vehicleIndex = EditorGUILayout.Popup("Vehicle",vehicleIndex,names);
        containerIndex = EditorGUILayout.Popup("Container",Mathf.Clamp(containerIndex,0,containers.Count-1),cargoNames);
        fillRack = EditorGUILayout.Toggle("Fill all available slots",fillRack);
        cargoFill = EditorGUILayout.Slider("Container fill",cargoFill,0,1);
        cargoClosure = EditorGUILayout.Slider("Bag closure",cargoClosure,0,1);
        bool changed = EditorGUI.EndChangeCheck();
        if (GUILayout.Button("Build / refresh preview") || changed || preview == null) Build();
        EditorGUILayout.HelpBox(result ?? "", MessageType.Info);
        Rect rect = GUILayoutUtility.GetRect(100,300,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
        if (preview != null)
        {
            Event e=Event.current;
            if (rect.Contains(e.mousePosition) && e.type==EventType.MouseDrag) { orbit+=e.delta*.4f; e.Use(); Repaint(); }
            if (rect.Contains(e.mousePosition) && e.type==EventType.ScrollWheel) { zoom=Mathf.Clamp(zoom*(1+e.delta.y*.05f),.25f,4f); e.Use(); Repaint(); }
            if (e.type==EventType.Repaint)
            {
                preview.BeginPreview(rect,GUIStyle.none);
                var rotation=Quaternion.Euler(orbit.y,orbit.x,0);
                float distance=Mathf.Max(1,bounds.extents.magnitude)*2.8f*zoom;
                preview.camera.transform.SetPositionAndRotation(bounds.center-rotation*Vector3.forward*distance,rotation);
                preview.camera.nearClipPlane=.01f; preview.camera.farClipPlane=distance+200;
                preview.camera.Render();
                GUI.DrawTexture(rect,preview.EndPreview(),ScaleMode.StretchToFill,false);
            }
        }
        EditorGUILayout.LabelField("Drag to orbit; mouse wheel to zoom. Preview copies never modify scene/prefabs.");
        scroll=EditorGUILayout.BeginScrollView(scroll,GUILayout.Height(130));
        var profile=settings.vehicles[vehicleIndex];
        foreach(var prefab in containers)
        {
            var cargo=prefab.GetComponentInChildren<GrainContainer>(true);
            EditorGUILayout.LabelField(prefab.name+" (level "+cargo.TransportLevel+")",cargo.TransportLevel<=profile.maximumCargoLevel ? "Level allowed — inspect fit in preview" : "Blocked by vehicle level");
        }
        EditorGUILayout.EndScrollView();
        if(GUILayout.Button("Select cargo / vehicle settings to edit levels")) Selection.objects=new Object[]{containers[containerIndex],settings};
    }
    private GameObject Copy(GameObject prefab, Transform parent)
    {
        // Instantiate directly in the isolated preview scene, never the user's scene.
        var copy=(GameObject)PrefabUtility.InstantiatePrefab(prefab,preview.camera.scene);
        copy.transform.SetParent(parent,false);
        foreach(var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled=false;
        foreach(var collider in copy.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
        foreach(var body in copy.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic=true;
        return copy;
    }
    private void Build()
    {
        preview?.Cleanup();
        preview=new PreviewRenderUtility();
        preview.camera.fieldOfView=35;
        preview.lights[0].intensity=1.4f; preview.lights[0].transform.rotation=Quaternion.Euler(40,40,0);
        preview.lights[1].intensity=.7f;
        assembly=new GameObject("Cargo preview"); preview.AddSingleGO(assembly);
        var profile=settings.vehicles[vehicleIndex];
        var prefab=Resources.Load<GameObject>(profile.PrefabResourcePath);
        if(prefab==null) { result="Missing vehicle prefab: "+profile.PrefabResourcePath; return; }
        var vehicle=Copy(prefab,assembly.transform);
        var rig=vehicle.GetComponentInChildren<BicycleController>(true);
        var cargoPrefab=containers[containerIndex];
        var container=cargoPrefab.GetComponentInChildren<GrainContainer>(true);
        var supports=new List<Transform>();
        if(rig!=null)
        {
            var rack=rig.GetComponent<VehicleCargoRack>();
            if(rack!=null && rack.surface!=null)
            {
                int count=Mathf.Max(1,rack.slotCount)/2; // GrainContainer loads use two rack slots.
                for(int i=0;i<count;i++)
                {
                    var support=new GameObject("Preview rack bay").transform; support.SetParent(rack.surface,false);
                    support.localPosition=new Vector3(i==0?0:(i%2==1?1:-1)*((i+1)/2)*rack.baySpacing,0,0);
                    supports.Add(support);
                }
            }
            else if(rig.exclusiveBagSlot!=null) supports.Add(rig.exclusiveBagSlot);
            else if(rig.cargoSlots!=null) foreach(var slot in rig.cargoSlots) if(slot!=null) supports.Add(slot);
        }
        bool allowed=container.TransportLevel<=profile.maximumCargoLevel;
        int placed=0;
        if(allowed) foreach(var support in supports)
        {
            var item=Copy(cargoPrefab,assembly.transform);
            foreach(var sack in item.GetComponentsInChildren<SackController>(true)) { sack.RefreshBindings(); sack.Fill=cargoFill; sack.Close=cargoClosure; }
            item.transform.position=support.position; item.transform.rotation=Quaternion.AngleAxis(support.eulerAngles.y,Vector3.up)*cargoPrefab.transform.rotation;
            if(rig.GetComponent<VehicleCargoRack>()!=null) VehicleCargoRack.Seat(item.transform,support);
            else item.transform.position+=support.position-item.GetComponentInChildren<GrainContainer>(true).WorldBounds.center;
            placed++; if(!fillRack) break;
        }
        result=$"{profile.displayName}: max level {profile.maximumCargoLevel}. {cargoPrefab.name}: level {container.TransportLevel}. "+(!allowed?"Blocked.":$"{placed} loaded; {supports.Count} container positions.")+" Preview ignores unlocks; gameplay still requires unlocks and carry eligibility.";
        bool first=true;
        foreach(var renderer in assembly.GetComponentsInChildren<Renderer>())
        { if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds); }
        if(first) bounds=new Bounds(Vector3.zero,Vector3.one);
        Repaint();
    }
}
