using UnityEngine;
using UnityEngine.AI;

/// <summary>One home companion: petting, timed chicken hunts and delivery back to its bowl.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
public sealed class FarmDog : MonoBehaviour, IInteractable
{
    public enum DogState { Idle, Sleeping, Petting, Chasing, Attacking, Returning }
    public Animator animator;
    public Transform visual, mouth, deliveryPoint;
    public ParticleSystem sleepVfx;
    public AudioSource strokeAudio;
    public GameObject legPrefab;
    [Range(1, 5)] public int startingLevel = 1;
    [Min(.1f)] public float petDuration = 2f;
    [Min(0)] public float petCooldown = 3f;
    [Min(0)] public float strokeSleepBlendSeconds = 2f;
    [Min(0)] public float nearbyWakeDistance = 4f;
    [Min(1)] public float runSpeed = 5.5f;
    [Min(1)] public float attackDamage = 28f;
    public DogState State { get; private set; }
    public int Level => Mathf.Clamp(SaveSystem.savesLoaded && SaveSystem.Data != null ? SaveSystem.Data.dogLevel : startingLevel, 1, 5);
    public static float HuntInterval(int level) => level <= 1 ? 0 : level == 2 || level == 4 ? 480f : 240f;
    public static bool BringsLeg(int level) => level >= 4;
    public float RestRemaining => Mathf.Max(0f, rest);
    public bool CanPet => initialized && isActiveAndEnabled && (State == DogState.Idle || State == DogState.Sleeping) && Time.time >= nextPet;
    private NavMeshAgent agent;
    private NavMeshPath path;
    private FarmChicken target;
    private ChickenLegPickup cargo;
    private Vector3 home, visualPosition;
    private Quaternion homeRotation;
    private float rest, stateAt, nextPet, nextDecision, huntStarted, nextNavigationRetry;
    private float animationReadyAt, petEndsAt;
    private float nextNearbyWake, awakeUntil;
    private Transform nearbyPlayer;
    private int lastLevel, animationHash;
    private bool initialized, hitApplied;
    private PlayerData profile;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
        home = transform.position;
        homeRotation = transform.rotation;
        if (visual) visualPosition = visual.localPosition;
        if (sleepVfx) sleepVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || !SaveSystem.savesLoaded || SaveSystem.Data == null) return;
        if (!initialized || profile != SaveSystem.Data)
        {
            profile = SaveSystem.Data;
            initialized = true;
            lastLevel = Level;
            rest = float.IsFinite(profile.dogRestSeconds) ? Mathf.Clamp(profile.dogRestSeconds, 0, HuntInterval(Level)) : 0;
            if (profile.dogHasDelivery && legPrefab && mouth)
            {
                cargo = Instantiate(legPrefab).GetComponent<ChickenLegPickup>();
                if (cargo) cargo.Carry(mouth);
                SetState(DogState.Returning);
            }
            else SetState(DogState.Sleeping);
        }
        if (Level != lastLevel)
        {
            rest = Mathf.Min(rest, HuntInterval(Level));
            lastLevel = Level;
        }
        if (rest > 0) rest = Mathf.Max(0, rest - Time.deltaTime);
        profile.dogRestSeconds = rest;
        if (State == DogState.Petting)
        {
            if (Time.time >= petEndsAt) SetState(DogState.Sleeping);
        }
        else if (State == DogState.Chasing) Chase();
        else if (State == DogState.Attacking) Attack();
        else if (State == DogState.Returning) ReturnHome();
        else if (Time.time >= nextDecision)
        {
            if (Level > 1 && rest <= 0 && FindChicken())
            {
                huntStarted = Time.time;
                SetState(DogState.Chasing);
            }
            else
            {
                if (!nearbyPlayer)
                {
                    var player = FindFirstObjectByType<PlayerController>();
                    if (player) nearbyPlayer = player.transform;
                }
                bool nearby = nearbyPlayer && nearbyPlayer.gameObject.activeInHierarchy &&
                    (nearbyPlayer.position - transform.position).sqrMagnitude <= nearbyWakeDistance * nearbyWakeDistance;
                UpdateHomeRest(nearby);
            }
            nextDecision = Time.time + (State == DogState.Chasing ? 0f : 3f);
        }
        UpdatePresentation();
    }

    private void UpdateHomeRest(bool playerNearby)
    {
        if (State == DogState.Idle)
        {
            if (!playerNearby || Time.time >= awakeUntil) SetState(DogState.Sleeping);
        }
        else if (State == DogState.Sleeping && playerNearby && Time.time >= nextNearbyWake)
        {
            awakeUntil = Time.time + Random.Range(4f, 7f);
            SetState(DogState.Idle);
        }
    }

    private bool ReadyToNavigate()
    {
        if (!agent.enabled) return false;
        if (agent.isOnNavMesh) return true;
        if (Time.time < nextNavigationRetry) return false;
        nextNavigationRetry = Time.time + 2f;
        return NavMesh.SamplePosition(transform.position, out var hit, 2f, agent.areaMask) && agent.Warp(hit.position);
    }

    private bool FindChicken()
    {
        if (!ReadyToNavigate()) return false;
        target = null;
        float nearest = float.PositiveInfinity;
        foreach (var chicken in FindObjectsByType<FarmChicken>(FindObjectsSortMode.None))
        {
            // Leave the first grain-eating tutorial encounter for the player.
            if (chicken.IsDead || !chicken.isActiveAndEnabled || chicken.feedingPile != null) continue;
            float distance = (chicken.transform.position - transform.position).sqrMagnitude;
            if (distance >= nearest || !CompletePath(chicken.transform.position)) continue;
            nearest = distance; target = chicken;
        }
        return target != null;
    }

    private bool CompletePath(Vector3 point) => agent.CalculatePath(point, path) && path.status == NavMeshPathStatus.PathComplete;
    private bool MoveTo(Vector3 point)
    {
        if (!ReadyToNavigate() || !CompletePath(point)) return false;
        agent.isStopped = false;
        agent.speed = runSpeed;
        return agent.SetPath(path);
    }

    private void Chase()
    {
        if (!target || target.IsDead || !target.isActiveAndEnabled || Time.time - huntStarted > 90f || Level <= 1)
        { GoHome(); return; }
        if ((target.transform.position - transform.position).sqrMagnitude <= 1.2f * 1.2f)
        { SetState(DogState.Attacking); return; }
        if (Time.time < nextDecision) return;
        nextDecision = Time.time + .3f;
        if (!MoveTo(target.transform.position)) GoHome();
    }

    private void Attack()
    {
        float elapsed = Time.time - stateAt;
        if (!target || target.IsDead) { GoHome(); return; }
        Vector3 toward = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
        if (toward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(toward);
        if (!hitApplied && elapsed >= .3f)
        {
            hitApplied = true;
            // Check contact again at impact, including walls, rather than damaging at animation entry.
            if (toward.sqrMagnitude <= 1.6f * 1.6f && ReadyToNavigate() &&
                !NavMesh.Raycast(transform.position, target.transform.position, out _, agent.areaMask) &&
                target.Hit(attackDamage, transform.position, transform))
            {
                if (BringsLeg(Level) && target.IsDead && target.LastDrop && mouth)
                {
                    var pickup = target.LastDrop.GetComponent<ChickenLegPickup>();
                    if (pickup && pickup.Carry(mouth))
                    { cargo = pickup; profile.dogHasDelivery = true; }
                }
                if (!BringsLeg(Level) || target.IsDead) { GoHome(); return; }
            }
        }
        if (elapsed >= .9f) SetState(DogState.Chasing);
    }

    private void GoHome()
    {
        target = null;
        rest = HuntInterval(Level);
        if (profile != null) profile.dogRestSeconds = rest;
        SetState(DogState.Returning);
    }

    private void ReturnHome()
    {
        if ((transform.position - home).sqrMagnitude < .7f * .7f)
        {
            if (cargo) cargo.PutDown(deliveryPoint ? deliveryPoint.position : home + Vector3.up * .15f);
            cargo = null;
            profile.dogHasDelivery = false;
            transform.rotation = homeRotation;
            SetState(DogState.Sleeping);
            return;
        }
        if (Time.time < nextDecision) return;
        nextDecision = Time.time + 2f;
        MoveTo(home);
    }

    private void SetState(DogState state)
    {
        if (State == state && animationHash != 0) return;
        State = state; stateAt = Time.time; nextDecision = 0;
        if (state == DogState.Sleeping) nextNearbyWake = Time.time + Random.Range(20f, 40f);
        hitApplied = false;
        if (agent && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        if (visual) visual.localPosition = visualPosition;
        string animation = state == DogState.Sleeping ? "Sleep" : state == DogState.Petting ? "Stroke" :
            state == DogState.Attacking ? "Attack" : "Idle";
        Play(animation);
    }

    private void Play(string state)
    {
        int hash = Animator.StringToHash(state);
        if (!animator || !animator.runtimeAnimatorController || animationHash == hash) return;
        int stroke = Animator.StringToHash("Stroke"), sleep = Animator.StringToHash("Sleep");
        int idle = Animator.StringToHash("Idle");
        float blend = (animationHash == stroke && hash == sleep) || (animationHash == sleep && hash == stroke) ||
            (animationHash == idle && hash == sleep) || (animationHash == sleep && hash == idle)
            ? strokeSleepBlendSeconds : .15f;
        animationHash = hash;
        animationReadyAt = Time.time + blend;
        animator.CrossFadeInFixedTime(hash, blend);
    }

    private void UpdatePresentation()
    {
        if (State == DogState.Chasing || State == DogState.Returning)
            Play(agent.enabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > .03f ? "Run" : "Idle");
        if (visual && State == DogState.Attacking)
            visual.localPosition = visualPosition + Vector3.forward * (Mathf.Sin(Mathf.Clamp01((Time.time - stateAt) / .9f) * Mathf.PI) * .18f);
        if (!sleepVfx) return;
        bool sleeping = State == DogState.Sleeping && Time.time >= animationReadyAt;
        if (sleeping && !sleepVfx.isPlaying) sleepVfx.Play(true);
        else if (!sleeping && (sleepVfx.isPlaying || sleepVfx.particleCount > 0))
            sleepVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = default;
        if (State == DogState.Petting) return false;
        prompt = new InteractionPromptData(transform.position + Vector3.up,
            CanPet ? "Stroke dog" :
            State == DogState.Chasing || State == DogState.Attacking || State == DogState.Returning ? "Dog is hunting" : "Let the dog rest", CanPet);
        if (interactor != null) prompt.RequiresFreeHands = interactor.HandsOccupied;
        return isActiveAndEnabled && interactor != null;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (interactor == null || interactor.HandsOccupied || Time.timeScale <= 0 || !CanPet) return false;
        SetState(DogState.Petting);
        // Let the wake-up blend finish before counting the actual stroke time.
        petEndsAt = Mathf.Max(Time.time, animationReadyAt) + petDuration;
        nextPet = petEndsAt + petCooldown;
        Vector3 toward = Vector3.ProjectOnPlane(interactor.transform.position - transform.position, Vector3.up);
        if (toward.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(toward);
        if (strokeAudio) strokeAudio.Play();
        return true;
    }

    private void OnDisable()
    {
        if (agent && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        if (strokeAudio) strokeAudio.Stop();
        if (sleepVfx) sleepVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (visual) visual.localPosition = visualPosition;
        if (State == DogState.Petting) State = DogState.Idle;
    }
}
