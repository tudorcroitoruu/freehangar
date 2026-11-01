using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
public static class ForkliftMovementAudit
{
    private static double nextSample;
    [InitializeOnLoadMethod] private static void AfterImport()
    {
        EditorApplication.delayCall+=Run;
        EditorApplication.update-=SampleRuntime;EditorApplication.update+=SampleRuntime;
    }
    private static void SampleRuntime()
    {
        if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<nextSample)return;
        nextSample=EditorApplication.timeSinceStartup+.25;
        foreach(var rider in Object.FindObjectsByType<PlayerBicycleInteraction>(FindObjectsSortMode.None))
        {
            if(rider.bicycle==null || rider.bicycle.GetComponent<ForkliftController>()==null)continue;
            var rig=rider.bicycle;var player=rider.GetComponent<PlayerController>();var motor=rig.GetComponent<MotorollerController>();
            var report=new StringBuilder();
            report.AppendLine($"time={Time.time:F2} throttle={Input.GetAxisRaw("Vertical")} speed={rig.Speed} riderMounted={rider.IsRiding} ridden={rig.IsRidden} position={rig.transform.position} focus={Application.isFocused} cursor={Cursor.lockState} dt={Time.deltaTime} scale={Time.timeScale} loading={LoadingScreen.IsLoading} upgrade={UpgradeManager.BlocksGameplayInput} popup={UIPopups.BasePopupUI.HasActivePopup} playerEnabled={player.enabled} discovery={player.GetComponent<PlayerInteractor>().DiscoveryBusy}");
            report.AppendLine("motor cache="+typeof(PlayerBicycleInteraction).GetField("motoroller",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(rider));
            var data=new SerializedObject(motor);var center=data.FindProperty("collisionCenter").vector3Value;var half=data.FindProperty("collisionHalfExtents").vector3Value;
            var align=rig.GetComponent<VehicleGroundAlignment>();var pose=align!=null&&align.Pose!=null?align.Pose:rig.transform;
            foreach(var hit in Physics.BoxCastAll(pose.TransformPoint(center),half,rig.transform.forward,pose.rotation,.2f,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(rig.transform))report.AppendLine($"BLOCKER {hit.collider.name} distance={hit.distance} normal={hit.normal} center={pose.TransformPoint(center)} half={half}");
            Directory.CreateDirectory("output/Forklift");File.AppendAllText("output/Forklift/runtime-movement.txt",report.ToString());
        }
    }
    [MenuItem("Tools/Vehicles/Audit forklift movement")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Physics.SyncTransforms();var report=new StringBuilder();
        foreach(var fork in Object.FindObjectsByType<ForkliftController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var rig=fork.GetComponent<BicycleController>();var motor=fork.GetComponent<MotorollerController>();var cc=fork.GetComponent<CharacterController>();
            var data=new SerializedObject(motor);var center=data.FindProperty("collisionCenter").vector3Value;var half=data.FindProperty("collisionHalfExtents").vector3Value;
            var alignment=fork.GetComponent<VehicleGroundAlignment>();var pose=alignment!=null&&alignment.Pose!=null?alignment.Pose:fork.transform;
            report.AppendLine($"{fork.name}: ridden={rig.IsRidden}, speed={rig.Speed}, scale={fork.transform.lossyScale}, capsule center={cc.center} radius={cc.radius} height={cc.height}, bounds={cc.bounds}, box={pose.TransformPoint(center)} half={half}");
            foreach(float sign in new[]{1f,-1f})
            {
                report.AppendLine("direction="+sign+", hangar guard="+HangarVehicleAccess.Blocks(fork.transform.position,fork.transform.position+fork.transform.forward*sign,.95f,rig));
                foreach(var hit in Physics.BoxCastAll(pose.TransformPoint(center),half,fork.transform.forward*sign,pose.rotation,.2f,~0,QueryTriggerInteraction.Ignore))
                    report.AppendLine($"  {hit.collider.name}: distance={hit.distance}, normal={hit.normal}, self={hit.transform.IsChildOf(fork.transform)}");
            }
            Probe(fork,report);
            foreach(var hit in Physics.RaycastAll(fork.transform.position+Vector3.up*3,Vector3.down,6,~0,QueryTriggerInteraction.Ignore))
                report.AppendLine($"ground: {hit.collider.name} at {hit.point}, normal={hit.normal}, self={hit.transform.IsChildOf(fork.transform)}");
        }
        Directory.CreateDirectory("output/Forklift");File.WriteAllText("output/Forklift/movement-audit.txt",report.ToString());
    }
    private static void Probe(ForkliftController source,StringBuilder report)
    {
        foreach(bool isolated in new[]{true,false})
        {
            var own=source.GetComponentsInChildren<Collider>(true);var enabled=new bool[own.Length];
            GameObject clone=null,floor=null;
            try
            {
                for(int i=0;i<own.Length;i++){enabled[i]=own[i].enabled;own[i].enabled=false;}
                clone=Object.Instantiate(source.gameObject);clone.name="Forklift movement probe";
                if(isolated)
                {
                    clone.transform.position=new Vector3(24000,1000,24000);
                    floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(24000,999.9f,24000);floor.transform.localScale=new Vector3(40,.2f,40);
                }
                var rig=clone.GetComponent<BicycleController>();var capsule=clone.GetComponent<CharacterController>();capsule.enabled=true;
                Physics.SyncTransforms();rig.SetRidden(true);Vector3 start=clone.transform.position;
                for(int i=0;i<90;i++){rig.Drive(1,0,false,1f/60f);Physics.SyncTransforms();}
                report.AppendLine($"PROBE isolated={isolated}: position delta={clone.transform.position-start}, speed={rig.Speed}, grounded={capsule.isGrounded}, active={rig.IsRidden}");
            }
            finally
            {
                if(clone!=null)Object.DestroyImmediate(clone);if(floor!=null)Object.DestroyImmediate(floor);
                for(int i=0;i<own.Length;i++)if(own[i]!=null)own[i].enabled=enabled[i];Physics.SyncTransforms();
            }
        }
    }}
