using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class FarmChickenSetup
{
    private const string Folder = "Assets/Prefabs/Gameplay/Chickens";
    public static void ExcludeGrainFromNavigation()
    {
        foreach(var pile in Object.FindObjectsByType<GrainPile>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var modifier=pile.GetComponent<NavMeshModifier>();
            if(!modifier)modifier=Undo.AddComponent<NavMeshModifier>(pile.gameObject);
            Undo.RecordObject(modifier,"Exclude dynamic grain from navigation");
            modifier.ignoreFromBuild=true;modifier.applyToChildren=true;
            EditorUtility.SetDirty(modifier);
            PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);
        }
    }
    [MenuItem("Tools/Animals/Bake yard and set up chickens")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before baking.");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Gameplay")
            throw new System.InvalidOperationException("Open Gameplay before baking.");
        if(GameObject.Find("Farm chickens")) throw new System.InvalidOperationException("Chickens already configured; rebake the existing NavMeshSurface instead.");
        var fence=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(x=>x.name=="Boundary split rail fences and hedgerow");
        var bounds=new Bounds();bool first=true;
        foreach(var r in fence.GetComponentsInChildren<Renderer>()) { if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds); }
        bounds.Expand(new Vector3(-3f,40f,-3f));
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Prefabs/Gameplay","Chickens");
        var root=new GameObject("Farm chickens");
        var surface=root.AddComponent<NavMeshSurface>();
        surface.collectObjects=CollectObjects.Volume;
        surface.center=bounds.center;surface.size=bounds.size;
        surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize=true;surface.voxelSize=.15f;
        ExcludeGrainFromNavigation();
        surface.BuildNavMesh();
        if(surface.navMeshData==null)throw new System.InvalidOperationException("Navigation bake produced no data.");
        AssetDatabase.CreateAsset(surface.navMeshData,Folder+"/ChickenYardNavMesh.asset");
        var drop=CreateDrop();
        var habitats=new[]{new Vector3(0,0,-10),new Vector3(-55,0,125),new Vector3(-25,0,65),new Vector3(30,0,105)};
        for(int i=0;i<4;i++)
        {
            Vector3 desired=habitats[i];
            var terrain=Terrain.activeTerrain;
            desired.y=terrain.SampleHeight(desired)+terrain.transform.position.y;
            if(!NavMesh.SamplePosition(desired,out var hit,2,NavMesh.AllAreas))throw new System.InvalidOperationException("No ground navigation near player.");
            var go=new GameObject("Adult chicken "+(i+1));go.transform.SetParent(root.transform);go.transform.position=hit.position;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Imported/Toon Series/Toon Farm Pack/Prefabs/Animals/Chicken/TFP_Chicken_01"+(char)('A'+i*2)+".prefab"),go.transform);
            foreach(var c in model.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
            var capsule=go.AddComponent<CapsuleCollider>();capsule.radius=.23f;capsule.height=.65f;capsule.center=new Vector3(0,.3f,0);
            var agent=go.AddComponent<NavMeshAgent>();agent.radius=.25f;agent.height=.7f;agent.speed=1.1f;agent.angularSpeed=360;agent.acceleration=12;agent.stoppingDistance=.15f;
            var chicken=go.AddComponent<FarmChicken>();chicken.visual=model.transform;chicken.animator=model.GetComponentInChildren<Animator>();
            chicken.animator.applyRootMotion=false;
            string anim="Assets/Imported/Toon Series/Toon Farm Pack/Animations/Animal Animations/Chicken/TFP_Chicken_";
            chicken.idleAnimation=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(anim+"Idle_01_Regular.controller");
            chicken.walkAnimation=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(anim+"Walk_Regular.controller");
            chicken.runAnimation=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(anim+"RunWingFlap_Regular.controller");
            chicken.animator.runtimeAnimatorController=chicken.idleAnimation;
            chicken.yard=bounds;chicken.dropPrefab=drop;
            chicken.wanderRadius=i==0?8f:18f;
            var clips=AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Audio/Gameplay/Animals/Chicken"}).Select(g=>AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            chicken.hitSounds=clips.Where(x=>x.name.Contains("Hit")).ToArray();
            chicken.idleSounds=clips.Where(x=>x.name.Contains("Idle")).ToArray();
            chicken.deathSound=clips.FirstOrDefault(x=>x.name.Contains("Killed"));
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Baked fenced chicken yard and placed four adult variants.");
    }
    private static GameObject CreateDrop()
    {
        var root=new GameObject("Chicken leg");
        var meat=new Material(Shader.Find("HDRP/Lit"));meat.SetColor("_BaseColor",new Color(.5f,.22f,.06f));
        var bone=new Material(Shader.Find("HDRP/Lit"));bone.SetColor("_BaseColor",new Color(.95f,.85f,.65f));
        AssetDatabase.CreateAsset(meat,Folder+"/ChickenMeat.mat");AssetDatabase.CreateAsset(bone,Folder+"/ChickenBone.mat");
        var body=GameObject.CreatePrimitive(PrimitiveType.Sphere);body.transform.SetParent(root.transform,false);body.transform.localScale=new Vector3(.24f,.17f,.3f);body.GetComponent<Renderer>().sharedMaterial=meat;
        var tip=GameObject.CreatePrimitive(PrimitiveType.Capsule);tip.transform.SetParent(root.transform,false);tip.transform.localPosition=new Vector3(0,0,.21f);tip.transform.localRotation=Quaternion.Euler(90,0,0);tip.transform.localScale=new Vector3(.07f,.11f,.07f);tip.GetComponent<Renderer>().sharedMaterial=bone;
        root.AddComponent<ChickenLegPickup>();
        var result=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/ChickenLeg.prefab");Object.DestroyImmediate(root);return result;
    }
}
