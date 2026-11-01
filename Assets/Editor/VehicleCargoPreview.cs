using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public sealed class VehicleCargoPreview : EditorWindow
{
    private VehicleUpgradeSettings settings;
    private readonly List<GameObject> containers = new List<GameObject>();
    private PreviewRenderUtility preview;
    private GameObject assembly;
    private int vehicleIndex, containerIndex;
    private bool fillRack = true;
    [SerializeField] private bool mixedLoad;
    [SerializeField] private List<string> mixedCargo = new List<string>();
    private readonly List<int> mixedPoseIndices = new List<int>();
    private readonly List<string> mixedStatus = new List<string>();
    private Vector2 mixedScroll;
    private float cargoFill = 1f, cargoClosure = 1f;
    private Vector2 orbit = new Vector2(135, 20), scroll;
    private float zoom = 1f;
    private Bounds bounds;
    private string result;
    private int selectedSlot, previewSlotCount;
    private bool usesRack, usesPickupLayout;
    private readonly HashSet<GrainContainer> editedSpaceAssets=new HashSet<GrainContainer>();
    private readonly List<GrainContainer> placedCargo=new List<GrainContainer>();
    private int slotStride = 1;
    private bool rebuildPending, buildFailed;
    private readonly List<Material> colorMaterials=new List<Material>();

    [MenuItem("Tools/Vehicles/Cargo Compatibility Preview")]
    private static void Open() => GetWindow<VehicleCargoPreview>("Vehicle Cargo Preview");
    private void OnEnable()
    {
        minSize=new Vector2(460,700);
        Undo.undoRedoPerformed += RefreshAfterUndo;
        settings = Resources.Load<VehicleUpgradeSettings>(VehicleUpgradeSettings.ResourcePath);
        containers.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Gameplay/Containers" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.GetComponentInChildren<GrainContainer>(true) != null) containers.Add(prefab);
        }
        containers.Sort((a,b) => a.GetComponentInChildren<GrainContainer>(true).TransportLevel.CompareTo(b.GetComponentInChildren<GrainContainer>(true).TransportLevel));
    }
    private void RefreshAfterUndo() { RequestBuild(); Repaint(); }
    private void OnDisable() { EditorApplication.delayCall -= BuildDeferred; rebuildPending=false; Undo.undoRedoPerformed -= RefreshAfterUndo; preview?.Cleanup(); ReleaseColorMaterials(); preview = null; assembly = null; }
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
        mixedLoad = EditorGUILayout.Toggle("Mixed cargo preview",mixedLoad);
        if(!mixedLoad) fillRack = EditorGUILayout.Toggle("Fill all available slots",fillRack);
        cargoFill = EditorGUILayout.Slider("Container fill",cargoFill,0,1);
        cargoClosure = EditorGUILayout.Slider("Bag closure",cargoClosure,0,1);
        bool changed = EditorGUI.EndChangeCheck();
        if (GUILayout.Button("Build / refresh preview") || changed || (preview == null && !buildFailed)) RequestBuild();
        if(mixedLoad) DrawMixedControls();
        DrawPoseControls();
        EditorGUILayout.HelpBox(result ?? "", MessageType.Info);
        Rect rect = GUILayoutUtility.GetRect(100,150,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
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
                // Enables preview lights and the active SRP, and restores the render target.
                preview.Render(true);
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
    private void DrawMixedControls()
    {
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("Add selected container") && mixedCargo.Count<32)
        {
            mixedCargo.Add(containers[containerIndex].GetComponentInChildren<GrainContainer>(true).CargoTypeId);
            RequestBuild();
        }
        if(GUILayout.Button("Clear mixed load")) { mixedCargo.Clear();RequestBuild(); }
        EditorGUILayout.EndHorizontal();
        mixedScroll=EditorGUILayout.BeginScrollView(mixedScroll,GUILayout.Height(85));
        for(int i=0;i<mixedCargo.Count;i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField((i+1)+". "+mixedCargo[i]+(i<mixedStatus.Count?" — "+mixedStatus[i]:""));
            if(GUILayout.Button("Edit",GUILayout.Width(40)))
            {
                int type=containers.FindIndex(p=>p.GetComponentInChildren<GrainContainer>(true).CargoTypeId==mixedCargo[i]);
                if(type>=0) containerIndex=type;
                selectedSlot=i<mixedPoseIndices.Count?Mathf.Max(0,mixedPoseIndices[i])/(usesRack?slotStride:1):0;
                RequestBuild();
            }
            bool remove=GUILayout.Button("×",GUILayout.Width(25));
            EditorGUILayout.EndHorizontal();
            if(remove) { mixedCargo.RemoveAt(i);RequestBuild();break; }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.LabelField("Add in loading order; Edit adjusts that container type and slot.",EditorStyles.miniLabel);
    }

    private void DrawPoseControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Container slot placement",EditorStyles.boldLabel);
        if(usesPickupLayout)
        {
            var cargo=containers[containerIndex].GetComponentInChildren<GrainContainer>(true);
            int units=EditorGUILayout.Popup("Pickup space",cargo.PickupSpaceUnits-1,new[]{"Half bay (two per bay)","Whole bay (one per bay)"})+1;
            if(units!=cargo.PickupSpaceUnits)
            {
                var data=new SerializedObject(cargo);
                data.FindProperty("pickupSpaceUnits").intValue=units;
                data.ApplyModifiedProperties();
                editedSpaceAssets.Add(cargo);RequestBuild();
            }
        }
        if(previewSlotCount==0) { EditorGUILayout.LabelField("This vehicle has no container slots."); return; }
        int oldSlot=selectedSlot;
        selectedSlot=Mathf.Clamp(EditorGUILayout.IntField("Slot (0 = first)",selectedSlot),0,previewSlotCount-1);
        if(oldSlot!=selectedSlot) RequestBuild();
        var profile=settings.vehicles[vehicleIndex];
        string id=containers[containerIndex].GetComponentInChildren<GrainContainer>(true).CargoTypeId;
        bool share=profile.UsesFirstSlotForAll(id);
        bool nextShare=EditorGUILayout.Toggle("Apply first slot values to all slots",share);
        if(nextShare!=share)
        {
            Undo.RecordObject(settings,"Share first cargo slot placement");
            var first=profile.FindCargoPoseExact(id,0);
            if(first==null)
            {
                first=new VehicleUpgradeSettings.CargoPose{containerId=id,slotIndex=0};
                ArrayUtility.Add(ref profile.cargoPoses,first);
            }
            first.applyToAllSlots=nextShare;
            share=nextShare;
            EditorUtility.SetDirty(settings); RequestBuild();
        }
        int index=share?0:usesRack?selectedSlot*slotStride:selectedSlot;
        if(share) EditorGUILayout.LabelField("Editing first-slot offsets for every slot.",EditorStyles.miniLabel);
        var pose=profile.FindCargoPose(id,index);
        EditorGUI.BeginChangeCheck();
        Vector3 position=DelayedVector("Position offset (slot space)",pose?.position ?? Vector3.zero,"cargo-position");
        Vector3 rotation=DelayedVector("Rotation offset (degrees)",pose?.rotation ?? Vector3.zero,"cargo-rotation");
        Vector3 scale=DelayedVector("Scale multiplier",pose?.EffectiveScale ?? Vector3.one,"cargo-scale");
        bool edited=EditorGUI.EndChangeCheck();
        if(GUILayout.Button("Reset this container / slot")) { position=rotation=Vector3.zero; scale=Vector3.one; edited=true; }
        if(edited)
        {
            Undo.RecordObject(settings,"Adjust vehicle cargo placement");
            if(pose==null)
            {
                pose=new VehicleUpgradeSettings.CargoPose{containerId=id,slotIndex=index};
                ArrayUtility.Add(ref profile.cargoPoses,pose);
            }
            pose.position=position; pose.rotation=rotation; pose.scale=new Vector3(Mathf.Max(.01f,scale.x),Mathf.Max(.01f,scale.y),Mathf.Max(.01f,scale.z));
            EditorUtility.SetDirty(settings); RequestBuild();
        }
        if(GUILayout.Button("Save cargo placements"))
        {
            AssetDatabase.SaveAssetIfDirty(settings);
            foreach(var cargo in editedSpaceAssets) if(cargo!=null) AssetDatabase.SaveAssetIfDirty(cargo);
            editedSpaceAssets.Clear();
        }
        EditorGUILayout.LabelField("Settings apply to this vehicle and container type.",EditorStyles.miniLabel);
    }

    private static Vector3 DelayedVector(string label, Vector3 value, string controlPrefix)
    {
        EditorGUILayout.LabelField(label);
        float previousWidth=EditorGUIUtility.labelWidth;
        EditorGUILayout.BeginHorizontal();
        EditorGUIUtility.labelWidth=14f;
        for(int i=0;i<3;i++)
        {
            GUI.SetNextControlName(controlPrefix+i);
            value[i]=EditorGUILayout.DelayedFloatField(i==0?"X":i==1?"Y":"Z",value[i]);
        }
        EditorGUIUtility.labelWidth=previousWidth;
        EditorGUILayout.EndHorizontal();
        return value;
    }
    private void RequestBuild()
    {
        if(rebuildPending) return;
        rebuildPending=true; buildFailed=false;
        EditorApplication.delayCall+=BuildDeferred;
    }
    private void BuildDeferred()
    {
        rebuildPending=false;
        if(this==null || settings==null || containers.Count==0) return;
        Build();
    }

    private GameObject Copy(GameObject prefab, Transform parent)
    {
        // Instantiate directly in the isolated preview scene, never the user's scene.
        var copy=(GameObject)PrefabUtility.InstantiatePrefab(prefab,preview.camera.scene);
        copy.transform.SetParent(parent,false);
        foreach(var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled=false;
        foreach(var collider in copy.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
        foreach(var body in copy.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic=true;
        UseMaterialColors(copy);
        return copy;
    }
    private void ReleaseColorMaterials()
    {
        foreach(var material in colorMaterials) if(material!=null) DestroyImmediate(material);
        colorMaterials.Clear();
    }
    private void UseMaterialColors(GameObject copy)
    {
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/VehicleCargoPreviewColor.shader");
        if(shader==null) return;
        foreach(var renderer in copy.GetComponentsInChildren<Renderer>(true))
        {
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++)
            {
                Material source=materials[i]; if(source==null) continue;
                var material=new Material(shader){name=source.name+" (preview color)",hideFlags=HideFlags.HideAndDontSave};
                string color=source.HasProperty("_BaseColor")?"_BaseColor":"_Color";
                string map=source.HasProperty("_BaseColorMap")?"_BaseColorMap":"_MainTex";
                material.SetColor("_BaseColor",source.HasProperty(color)?source.GetColor(color):Color.white);
                if(source.HasProperty(map))
                {
                    material.SetTexture("_BaseColorMap",source.GetTexture(map));
                    material.SetTextureScale("_BaseColorMap",source.GetTextureScale(map));
                    material.SetTextureOffset("_BaseColorMap",source.GetTextureOffset(map));
                }
                bool cutout=source.HasProperty("_AlphaCutoffEnable") && source.GetFloat("_AlphaCutoffEnable")>.5f;
                material.SetFloat("_AlphaCutoff",cutout && source.HasProperty("_AlphaCutoff")?source.GetFloat("_AlphaCutoff"):0);
                colorMaterials.Add(material); materials[i]=material;
            }
            renderer.sharedMaterials=materials;
            renderer.SetPropertyBlock(null);
        }
    }
    public static void PrepareContainer(GameObject item,float fill,float closure)
    {
        foreach(var sack in item.GetComponentsInChildren<SackController>(true))
        {
            sack.RefreshBindings();sack.Fill=fill;sack.Close=closure;
        }
        foreach(var cargo in item.GetComponentsInChildren<GrainContainer>(true))
        {
            var data=new SerializedObject(cargo);
            data.FindProperty("kilograms").floatValue=cargo.CapacityKg*Mathf.Clamp01(fill);
            data.ApplyModifiedPropertiesWithoutUndo();
            cargo.RefreshFillPresentation();
        }
    }
    private void Build()
    {
        preview?.Cleanup();
        ReleaseColorMaterials();
        preview=new PreviewRenderUtility();
        preview.camera.fieldOfView=35;
        preview.camera.clearFlags=CameraClearFlags.SolidColor;
        preview.camera.backgroundColor=new Color(.12f,.14f,.17f,1);
        preview.ambientColor=new Color(.15f,.15f,.15f,1);
        var hd=preview.camera.GetComponent<HDAdditionalCameraData>() ?? preview.camera.gameObject.AddComponent<HDAdditionalCameraData>();
        hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;
        hd.backgroundColorHDR=preview.camera.backgroundColor;
        hd.volumeLayerMask=0; // Never inherit Gameplay's sky/exposure volumes.
        hd.customRenderingSettings=true;
        hd.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Postprocess,false);
        hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.Postprocess]=true;
        hd.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.ExposureControl,false);
        hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.ExposureControl]=true;
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
        usesRack=false;usesPickupLayout=false;placedCargo.Clear();
        if(rig!=null)
        {
            var rack=rig.GetComponent<VehicleCargoRack>();
            var pickupLayout=rig.GetComponent<PickupCargoLayout>();
            if(pickupLayout!=null) { usesPickupLayout=true;supports.AddRange(pickupLayout.PreviewSlots(container)); }
            else if(rack!=null && rack.surface!=null)
            {
                usesRack=true;
                slotStride=rack.oneContainerPerSlot?1:2;
                int count=rack.ContainerSlotCount; // GrainContainer loads use two rack slots.
                for(int i=0;i<count;i++)
                {
                    var support=new GameObject("Preview rack bay").transform; support.SetParent(rack.surface,false);
                    support.localPosition=rack.SlotPosition(i);
                    supports.Add(support);
                }
            }
            else if(rig.exclusiveBagSlot!=null) supports.Add(rig.exclusiveBagSlot);
            else if(rig.cargoSlots!=null) foreach(var slot in rig.cargoSlots) if(slot!=null) supports.Add(slot);
        }
        previewSlotCount=supports.Count;
        bool allowed=container.TransportLevel<=profile.maximumCargoLevel;
        int placed=0;
        if(!mixedLoad && allowed) foreach(var support in supports)
        {
            int supportIndex=supports.IndexOf(support);
            if(!fillRack && supportIndex!=Mathf.Clamp(selectedSlot,0,supports.Count-1)) continue;
            var item=Copy(cargoPrefab,assembly.transform);
            PrepareContainer(item,cargoFill,cargoClosure);
            item.transform.position=support.position; item.transform.rotation=Quaternion.AngleAxis(support.eulerAngles.y,Vector3.up)*cargoPrefab.transform.rotation;
            Quaternion baseRotation=Quaternion.Inverse(support.rotation)*item.transform.rotation;
            VehicleCargoPlacement.Place(item.transform,support,profile,container.CargoTypeId,usesRack?supportIndex*slotStride:supportIndex,baseRotation,usesRack,item.transform.localScale);
            placedCargo.Add(item.GetComponentInChildren<GrainContainer>(true));
            placed++; if(!fillRack) break;
        }
        if(mixedLoad)
        {
            mixedStatus.Clear();mixedPoseIndices.Clear();
            var dynamicLayout=rig!=null?rig.GetComponent<PickupCargoLayout>():null;
            int rejected=0;
            foreach(string type in mixedCargo)
            {
                var source=containers.Find(p=>p.GetComponentInChildren<GrainContainer>(true).CargoTypeId==type);
                var cargo=source!=null?source.GetComponentInChildren<GrainContainer>(true):null;
                Transform slot=null;int poseIndex=0;
                if(cargo!=null && cargo.TransportLevel<=profile.maximumCargoLevel)
                {
                    if(dynamicLayout!=null)
                    {
                        slot=dynamicLayout.FreeSlot(cargo);
                        if(slot!=null) poseIndex=dynamicLayout.PoseIndex(slot);
                    }
                    else
                    {
                        slot=supports.Find(t=>t!=null && t.childCount==0);
                        if(slot!=null) poseIndex=supports.IndexOf(slot)*(usesRack?slotStride:1);
                    }
                }
                if(slot==null)
                {
                    mixedStatus.Add(cargo!=null && cargo.TransportLevel>profile.maximumCargoLevel?"level blocked":"no free space");
                    mixedPoseIndices.Add(-1);rejected++;continue;
                }
                var item=Copy(source,assembly.transform);
                PrepareContainer(item,cargoFill,cargoClosure);
                item.transform.SetParent(slot,true); // Reserve the actual bay for the next mixed item.
                item.transform.rotation=Quaternion.AngleAxis(slot.eulerAngles.y,Vector3.up)*source.transform.rotation;
                Quaternion relative=Quaternion.Inverse(slot.rotation)*item.transform.rotation;
                VehicleCargoPlacement.Place(item.transform,slot,profile,cargo.CargoTypeId,poseIndex,relative,usesRack,item.transform.localScale);
                placedCargo.Add(item.GetComponentInChildren<GrainContainer>(true));
                mixedStatus.Add("slot "+poseIndex);mixedPoseIndices.Add(poseIndex);placed++;
            }
            result=$"{profile.displayName}: {placed} mixed containers loaded; {rejected} could not fit / are blocked.";
        }
        else result=$"{profile.displayName}: max level {profile.maximumCargoLevel}. {cargoPrefab.name}: level {container.TransportLevel}. "+(!allowed?"Blocked.":$"{placed} loaded; {supports.Count} container positions.")+" Preview ignores unlocks; gameplay still requires unlocks and carry eligibility.";
        int overlaps=0;
        for(int i=0;i<placedCargo.Count;i++) for(int j=i+1;j<placedCargo.Count;j++)
        {
            if(placedCargo[i]==null || placedCargo[j]==null) continue;
            Bounds a=placedCargo[i].WorldBounds,b=placedCargo[j].WorldBounds;
            a.Expand(-.01f);b.Expand(-.01f);
            if(a.Intersects(b)) overlaps++;
        }
        if(overlaps>0) result+=$" Potential container overlaps: {overlaps}. Adjust position/scale or assign a whole bay. Rotated bounds checks are conservative.";
        bool first=true;
        foreach(var renderer in assembly.GetComponentsInChildren<Renderer>())
        { if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds); }
        if(first) bounds=new Bounds(Vector3.zero,Vector3.one);
        Repaint();
    }
}
