using UnityEngine;
using UnityEngine.AI;

/// <summary>Ground navigation stays on the baked yard; hit hops affect only the model.</summary>
[RequireComponent(typeof(NavMeshAgent))]
public sealed class FarmChicken : MonoBehaviour, IInteractable
{
    public Transform visual;
    public Animator animator;
    public RuntimeAnimatorController idleAnimation, walkAnimation, runAnimation;
    public AudioClip[] idleSounds, hitSounds;
    public AudioClip deathSound;
    public GameObject dropPrefab;
    public Bounds yard;
    public GrainPile feedingPile;
    public Vector3 feedingPoint;
    public RuntimeAnimatorController eatAnimation;
    public event System.Action Died;
    private float nextBite;
    [Min(1)] public float health = 100f;
    [Min(1f)] public float wanderRadius = 14f;
    [Min(1f)] public float idleHearingDistance = 28f;
    private NavMeshAgent agent;
    private Transform threat;
    private Vector3 lastThreat, visualRest, knockDirection;
    private Vector3 originalScale;
    private Renderer[] renderers;
    private MaterialPropertyBlock flash;
    private AudioSource audioSource;
    private AudioSource idleAudioSource;
    private Vector3 homePosition;
    private float decisionAt, fleeUntil, hitAt = -10f, nextHit, deathAt = -1f, nextSound;
    private NavMeshPath path;
    public bool IsDead => deathAt >= 0f;
    public static bool AxeDiscovered => BuriedFind.WasFound(SaveSystem.Data, PlayerCombatTools.AxeFindId) ||
        BuriedFind.WasFound(SaveSystem.Data, PlayerCombatTools.LegacyAxeFindId);

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        homePosition = transform.position;
        path = new NavMeshPath();
        if (!visual) visual = transform.GetChild(0);
        visualRest = visual.localPosition; originalScale = visual.localScale;
        renderers = visual.GetComponentsInChildren<Renderer>(true);
        flash = new MaterialPropertyBlock();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; audioSource.maxDistance = 22f;
        idleAudioSource = gameObject.AddComponent<AudioSource>();
        idleAudioSource.spatialBlend = 1f;
        idleAudioSource.rolloffMode = AudioRolloffMode.Linear;
        idleAudioSource.minDistance = 3f;
        idleAudioSource.maxDistance = idleHearingDistance;
        nextSound = Time.time + Random.Range(4f, 10f);
    }

    private void Update()
    {
        if (IsDead)
        {
            float t = Time.time - deathAt;
            visual.localPosition = visualRest + transform.InverseTransformDirection(knockDirection) * Mathf.Min(t, .5f)
                + Vector3.up * (Mathf.Max(0, 2.4f*t - 4.9f*t*t) + .22f*Mathf.Clamp01(t*2f));
            visual.localRotation = Quaternion.Euler(0, 0, Mathf.Min(90, t*180));
            visual.localScale = originalScale * (1-Mathf.Clamp01((t-.7f)/.45f));
            if (t > 1.2f) Destroy(gameObject);
            return;
        }
        float hop = Time.time-hitAt;
        visual.localPosition = visualRest + Vector3.up * (hop < .5f ? Mathf.Sin(hop/.5f*Mathf.PI)*.35f : 0);
        bool red = hop < .18f;
        flash.Clear();
        if (red) { flash.SetColor("_BaseColor", Color.red); flash.SetColor("_EmissiveColor", Color.red*4); }
        foreach (var r in renderers) r.SetPropertyBlock(flash);
        if (!agent.isOnNavMesh) return;
        bool fleeing = Time.time < fleeUntil;
        if (threat) lastThreat = threat.position;
        // Reject even an intermediate path segment that would bring a fleeing bird closer.
        if (fleeing && Vector3.Dot(agent.desiredVelocity, transform.position-lastThreat) < -.01f)
        { agent.ResetPath(); decisionAt = 0; }
        bool canFeed = AxeDiscovered && feedingPile != null && feedingPile.isActiveAndEnabled;
        bool eating = !fleeing && canFeed &&
            Vector3.ProjectOnPlane(transform.position-homePosition,Vector3.up).sqrMagnitude < 1f;
        if (eating)
        {
            if (agent.hasPath) agent.ResetPath();
            Vector3 toward = Vector3.ProjectOnPlane(feedingPoint-transform.position,Vector3.up);
            if(toward.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(toward);
            if(Time.time>=nextBite)
            {
                var bite=feedingPoint; bite.y=feedingPile.SurfaceHeight(bite);
                feedingPile.Scoop(bite,.35f,.15f,.2f);
                nextBite=Time.time+3f;
            }
        }
        else if (!fleeing && canFeed)
        {
            if(!agent.hasPath)agent.SetDestination(homePosition);
        }
        else if (Time.time >= decisionAt) ChooseDestination(fleeing);
        var animation = eating && eatAnimation != null ? eatAnimation : agent.velocity.sqrMagnitude > .04f ? (fleeing ? runAnimation : walkAnimation) : idleAnimation;
        if (animator && animator.runtimeAnimatorController != animation) animator.runtimeAnimatorController = animation;
        if (animator) animator.speed = animation == walkAnimation ? 1.5f : 1f;
        if (Time.time >= nextSound)
        {
            if (idleSounds != null && idleSounds.Length > 0) idleAudioSource.PlayOneShot(idleSounds[Random.Range(0,idleSounds.Length)]);
            nextSound=Time.time+Random.Range(6f,14f);
        }
    }

    private void ChooseDestination(bool fleeing)
    {
        decisionAt = Time.time + Random.Range(3f, 5f);
        agent.speed = fleeing ? 4f : 1.1f;
        if (!fleeing && Vector3.ProjectOnPlane(transform.position-homePosition,Vector3.up).sqrMagnitude > wanderRadius*wanderRadius)
        {
            if (agent.CalculatePath(homePosition,path) && path.status==NavMeshPathStatus.PathComplete) agent.SetPath(path);
            return;
        }
        if (!fleeing && Random.value < .3f) { agent.ResetPath(); return; }
        Vector3 away = Vector3.ProjectOnPlane(transform.position-lastThreat, Vector3.up).normalized;
        if (away.sqrMagnitude < .1f) away = transform.forward;
        for (int attempt=0; attempt<16; attempt++)
        {
            Vector3 direction = fleeing ? Quaternion.Euler(0,Random.Range(-70f,70f),0)*away
                : Quaternion.Euler(0,Random.Range(0f,360f),0)*Vector3.forward;
            Vector3 candidate=transform.position+direction*Random.Range(3f,10f);
            if (!InsideYard(candidate) || !NavMesh.SamplePosition(candidate,out var hit,2f,agent.areaMask) || !InsideYard(hit.position)) continue;
            if (!fleeing && Vector3.ProjectOnPlane(hit.position-homePosition,Vector3.up).sqrMagnitude > wanderRadius*wanderRadius) continue;
            if (!agent.CalculatePath(hit.position,path) || path.status!=NavMeshPathStatus.PathComplete) continue;
            bool safe=true; Vector3 previous=transform.position;
            foreach(var corner in path.corners)
            {
                if (fleeing && Vector3.Dot(corner-previous,previous-lastThreat)<-.01f) { safe=false;break; }
                previous=corner;
            }
            if (safe) { agent.SetPath(path);return; }
        }
        agent.ResetPath();
    }
    public bool InsideYard(Vector3 point) => point.x>yard.min.x && point.x<yard.max.x && point.z>yard.min.z && point.z<yard.max.z;
    public bool Hit(float damage, Vector3 source, Transform attacker = null)
    {
        if (IsDead || damage<=0 || Time.time<nextHit) return false;
        nextHit=Time.time+.25f; hitAt=Time.time; health-=damage;
        flash.Clear(); flash.SetColor("_BaseColor", Color.red); flash.SetColor("_EmissiveColor", Color.red*4);
        foreach(var r in renderers)r.SetPropertyBlock(flash);
        threat=attacker;lastThreat=source;fleeUntil=Time.time+15f;decisionAt=0;
        knockDirection=Vector3.ProjectOnPlane(transform.position-source,Vector3.up).normalized*2f;
        if (agent.isOnNavMesh)
        {
            var target=transform.position+knockDirection*.4f;
            if(InsideYard(target) && !NavMesh.Raycast(transform.position,target,out _,agent.areaMask)) agent.Move(knockDirection*.4f);
        }
        if (health>0) Play(hitSounds);
        else
        {
            deathAt=Time.time;
            idleAudioSource.Stop();
            if(agent.isOnNavMesh)agent.ResetPath(); agent.enabled=false;
            foreach(var c in GetComponentsInChildren<Collider>())c.enabled=false;
            if(animator)animator.enabled=false;
            if(deathSound)audioSource.PlayOneShot(deathSound);
            if(dropPrefab)Instantiate(dropPrefab,transform.position+Vector3.up*.15f,Quaternion.identity);
            Died?.Invoke();
        }
        return true;
    }
    public bool HitByVehicle(BicycleController vehicle)
    {
        if(!vehicle.motorDriven || Mathf.Abs(vehicle.Speed)<2f)return false;
        float speed=Mathf.Abs(vehicle.Speed);
        float damage=vehicle.GetComponent<PickupController>()!=null && speed>=8f ? 100f : speed*7f;
        return Hit(damage,vehicle.transform.position,vehicle.transform);
    }
    private void Play(AudioClip[] clips) { if(clips!=null && clips.Length>0)audioSource.PlayOneShot(clips[Random.Range(0,clips.Length)]); }
    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt=new InteractionPromptData(transform.position+Vector3.up*.8f,
            AxeDiscovered ? "Use your axe to stop the chicken eating grain" : "Find the axe to stop this chicken",false);
        prompt.ShowPrimaryButton=AxeDiscovered;
        return feedingPile!=null && !IsDead && interactor!=null;
    }
    public bool TryInteract(PlayerInteractor interactor) => false;
}
