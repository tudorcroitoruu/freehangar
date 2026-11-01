using System.IO;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ForkliftSetup
{
    [InitializeOnLoadMethod] private static void AfterImport() => EditorApplication.delayCall+=()=>
    {
        if(!EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().name=="Gameplay")Apply();
    };
    [MenuItem("Tools/Vehicles/Connect forklift driving and big bag handling")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetActiveScene();if(scene.name!="Gameplay")return;
        var existing=UnityEngine.Object.FindFirstObjectByType<ForkliftController>(FindObjectsInactive.Include);
        if(existing!=null){AlignExisting(existing);Report(existing);return;}
        GameObject model=null;
        foreach(var node in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(node.gameObject.scene==scene && (node.name.Equals("Forklift",System.StringComparison.OrdinalIgnoreCase) || node.name=="TFP_Front_Loader_Forklift_01A")){model=node.gameObject;break;}
        if(model==null){Debug.LogWarning("Forklift setup: save or add the ForkLift model to Gameplay first.");return;}
        var renderers=model.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return;
        Bounds world=renderers[0].bounds;foreach(var renderer in renderers)world.Encapsulate(renderer.bounds);
        var host=new GameObject("Forklift");SceneManager.MoveGameObjectToScene(host,scene);Undo.RegisterCreatedObjectUndo(host,"Connect forklift");
        host.transform.SetPositionAndRotation(new Vector3(world.center.x,world.min.y,world.center.z),Quaternion.LookRotation(ModelForward(model),Vector3.up));
        Undo.SetTransformParent(model.transform,host.transform,"Connect forklift model");
        // Original imported meshes remain intact; shared vehicle physics owns the body.
        foreach(var collider in model.GetComponentsInChildren<Collider>(true)){Undo.RecordObject(collider,"Configure forklift body collision");collider.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
        Vector3 min=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity),max=-min;
        foreach(var renderer in renderers)
        {
            var b=renderer.bounds;
            for(int corner=0;corner<8;corner++)
            {
                var p=host.transform.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));
                min=Vector3.Min(min,p);max=Vector3.Max(max,p);
            }
        }
        float width=max.x-min.x,length=max.z-min.z,height=max.y-min.y;
        var motor=host.AddComponent<MotorollerController>();var rig=host.GetComponent<BicycleController>();
        rig.motorDriven=true;rig.cargoSlots=new Transform[0];rig.exitCoastDuration=0;
        rig.steering=Node(host.transform,"Forklift steering",Vector3.up*height*.5f);
        rig.frontWheel=Node(host.transform,"Front axle support",new Vector3(0,.42f,length*.16f));
        rig.rearWheel=Node(host.transform,"Rear axle support",new Vector3(0,.42f,-length*.28f));
        rig.riderView=Node(host.transform,"Forklift driving view",new Vector3(0,height*.76f,-length*.07f));
        rig.dismountSideDistance=width*.5f+.8f;rig.dismountLongitudinalDistance=length*.5f+.9f;
        var data=new SerializedObject(motor);data.FindProperty("vehicleName").stringValue="forklift";data.FindProperty("maximumSpeed").floatValue=5f;
        data.FindProperty("acceleration").floatValue=2.5f;data.FindProperty("wheelRadius").floatValue=.42f;
        data.FindProperty("wheelbase").floatValue=Mathf.Max(.8f,length*.44f);
        data.FindProperty("collisionCenter").vector3Value=new Vector3(0,height*.36f,-length*.1f);
        data.FindProperty("collisionHalfExtents").vector3Value=new Vector3(width*.45f,height*.3f,length*.28f);
        data.ApplyModifiedPropertiesWithoutUndo();motor.Configure();
        var capsule=host.GetComponent<CharacterController>();capsule.center=new Vector3(0,height*.35f,-length*.1f);capsule.height=height*.65f;capsule.radius=width*.38f;capsule.stepOffset=.25f;capsule.slopeLimit=35;
        var body=host.AddComponent<BoxCollider>();body.center=new Vector3(0,height*.36f,-length*.1f);body.size=new Vector3(width*.9f,height*.6f,length*.56f);
        var upgrade=host.AddComponent<VehicleUpgrade>();var upgradeData=new SerializedObject(upgrade);upgradeData.FindProperty("vehicleId").stringValue="forklift";upgradeData.ApplyModifiedPropertiesWithoutUndo();
        var forklift=host.AddComponent<ForkliftController>();
        var anchor=Node(host.transform,"Fork load anchor",new Vector3(0,.22f,max.z-.3f));
        var liftData=new SerializedObject(forklift);liftData.FindProperty("forkAnchor").objectReferenceValue=anchor;liftData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);Report(forklift);
    }
    private static Vector3 ModelForward(GameObject model)
    {
        Vector3 front=Vector3.zero,rear=Vector3.zero;int fronts=0,rears=0;
        foreach(var node in model.GetComponentsInChildren<Transform>(true))
        {
            if(node.name.Contains("Wheel_F.") && !node.name.Contains("LOD")){front+=node.position;fronts++;}
            if(node.name.Contains("Wheel_R.") && !node.name.Contains("LOD")){rear+=node.position;rears++;}
        }
        var direction=fronts>0 && rears>0?front/fronts-rear/rears:model.transform.forward;
        direction=Vector3.ProjectOnPlane(direction,Vector3.up);return direction.sqrMagnitude>.001f?direction.normalized:Vector3.forward;
    }
    private static void AlignExisting(ForkliftController forklift)
    {
        var root=forklift.transform;Transform model=null;
        foreach(var node in root.GetComponentsInChildren<LODGroup>(true))if(node.GetComponentsInChildren<Renderer>(true).Length>0){model=node.transform;break;}
        if(model==null)return;
        Vector3 forward=ModelForward(model.gameObject);
        if(Vector3.Dot(root.forward,forward)>.999f)return;
        Undo.RecordObject(root,"Align forklift driving direction");Undo.RecordObject(model,"Preserve forklift visual pose");
        Vector3 position=model.position;Quaternion rotation=model.rotation;
        root.rotation=Quaternion.LookRotation(forward,Vector3.up);model.SetPositionAndRotation(position,rotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model);
        Vector3 min=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity),max=-min;
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            var bounds=renderer.bounds;
            for(int c=0;c<8;c++)
            {
                Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));
                Vector3 point=root.InverseTransformPoint(corner);min=Vector3.Min(min,point);max=Vector3.Max(max,point);
            }
        }
        float width=max.x-min.x,length=max.z-min.z,height=max.y-min.y;
        var rig=forklift.GetComponent<BicycleController>();Undo.RecordObject(rig,"Align forklift supports");
        rig.frontWheel.localPosition=new Vector3(0,.42f,length*.16f);rig.rearWheel.localPosition=new Vector3(0,.42f,-length*.28f);
        rig.riderView.localPosition=new Vector3(0,height*.76f,-length*.07f);rig.riderView.localRotation=Quaternion.identity;
        rig.dismountSideDistance=width*.5f+.8f;rig.dismountLongitudinalDistance=length*.5f+.9f;
        var liftData=new SerializedObject(forklift);var anchor=(Transform)liftData.FindProperty("forkAnchor").objectReferenceValue;
        Undo.RecordObject(anchor,"Align fork pickup contact");anchor.localPosition=new Vector3(0,.22f,max.z-.3f);
        var capsule=forklift.GetComponent<CharacterController>();Undo.RecordObject(capsule,"Align forklift capsule");
        capsule.center=new Vector3(0,height*.35f,-length*.1f);capsule.height=height*.65f;capsule.radius=width*.38f;
        var body=forklift.GetComponent<BoxCollider>();Undo.RecordObject(body,"Align forklift body");
        body.center=new Vector3(0,height*.36f,-length*.1f);body.size=new Vector3(width*.9f,height*.6f,length*.56f);
        var motor=forklift.GetComponent<MotorollerController>();var data=new SerializedObject(motor);
        data.FindProperty("wheelbase").floatValue=Mathf.Max(.8f,length*.44f);
        data.FindProperty("collisionCenter").vector3Value=body.center;data.FindProperty("collisionHalfExtents").vector3Value=body.size*.5f;
        data.ApplyModifiedProperties();motor.Configure();
        foreach(var item in new UnityEngine.Object[]{root,model,rig,rig.frontWheel,rig.rearWheel,rig.riderView,anchor,capsule,body,motor})EditorUtility.SetDirty(item);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Directory.CreateDirectory("output/Forklift");File.WriteAllText("output/Forklift/direction.txt",$"PASS rig forward aligned to front/rear wheel centers. Direction dot={Vector3.Dot(root.forward,ModelForward(model.gameObject)):F5}. Width={width:F2}, length={length:F2}; camera, fork contact and colliders realigned. Model world pose preserved.");
    }    private static Transform Node(Transform parent,string name,Vector3 local)
    {
        var node=new GameObject(name).transform;node.SetParent(parent,false);node.localPosition=local;return node;
    }
    private static void Report(ForkliftController forklift)
    {
        var rig=forklift.GetComponent<BicycleController>();var data=new SerializedObject(forklift);
        bool valid=rig!=null && rig.riderView!=null && rig.frontWheel!=null && rig.rearWheel!=null && data.FindProperty("forkAnchor").objectReferenceValue!=null;
        Directory.CreateDirectory("output/Forklift");File.WriteAllText("output/Forklift/setup.txt",$"{(valid?"PASS":"FAIL")} forklift shared driving rig, rider view, fork anchor. Engine uses lawnmower/motor clip at pitch multiplier {forklift.EnginePitchMultiplier}. Fork contact automatically lifts full Big Bags; Q drops. Empty Big Bags can be carried by hand.");
    }
    [MenuItem("Tools/Vehicles/Validate forklift cargo")]
    public static void Validate()
    {
        var api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new TestResults());
        api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{"ForkliftCargoTests"}}){runSynchronously=true});
        UnityEngine.Object.DestroyImmediate(api);
    }
    private sealed class TestResults:ICallbacks
    {
        public void RunStarted(ITestAdaptor tests){}
        public void TestStarted(ITestAdaptor test){}
        public void TestFinished(ITestResultAdaptor result){}
        public void RunFinished(ITestResultAdaptor result)
        {
            var report=new System.Text.StringBuilder($"Forklift cargo: {result.PassCount} passed, {result.FailCount} failed.\n");Append(result,report);
            Directory.CreateDirectory("output/Forklift");File.WriteAllText("output/Forklift/tests.txt",report.ToString());Debug.Log(report.ToString());
        }
        private static void Append(ITestResultAdaptor result,System.Text.StringBuilder report)
        {
            if(result.TestStatus==TestStatus.Failed)report.AppendLine(result.Name+": "+result.Message);
            foreach(var child in result.Children)Append(child,report);
        }
    }}
