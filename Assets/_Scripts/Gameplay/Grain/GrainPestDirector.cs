using System.Collections;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(500)]
public sealed class GrainPest : MonoBehaviour
{
    public GrainContainer Target { get; private set; }
    public bool IsCrow { get; private set; }
    private float nextBite;
    private float speed;
    private Animator animator;
    private RuntimeAnimatorController flying, feeding;
    private bool fleeing, feedingNow;
    private float fleeUntil;
    private Vector3 fleeDirection;
    private bool hasSlot, wantsToClimb, climbing, onBag;
    private float slotAngle, climbStart, climbDelay, biteAmount = 1f, fleeGroundY;
    private Vector3 climbFrom;
    private float beginAt, climbDuration, nextShuffle, restUntil;
    private bool crowApproaching;
    private Vector3 crowWaypoint, crowLandingOffset, previousTargetPosition;
    private bool crowPerched;
    private float targetSpeed;
    private Vector3 CrowPerchPoint()
    {
        Bounds bounds = Target.WorldBounds;
        Vector3 point = bounds.center + Quaternion.Euler(0,Target.transform.eulerAngles.y,0) * crowLandingOffset;
        point.y = bounds.max.y - .05f;
        return point;
    }
    private void LateUpdate()
    {
        // Follow the container after vehicle motion and cargo seating have been applied.
        if (IsCrow && crowPerched && !fleeing && Target != null && Target.isActiveAndEnabled)
        {
            transform.position = CrowPerchPoint();
            transform.rotation = Quaternion.Euler(0, Target.transform.eulerAngles.y, 0);
        }
    }
    private void EatFromTarget()
    {
        if (Time.time < nextBite) return;
        Target.ConsumeByPest(biteAmount);
        if (!IsCrow && animator != null) animator.SetTrigger("IdleBreak");
        nextBite = Time.time + 3f;
        if (!IsCrow) restUntil = Time.time + Random.Range(.15f, .75f);
    }
    public void ConfigureCrowArrival(Vector3 start, Vector3 doorway, int index)
    {
        transform.position = start;
        crowWaypoint = doorway;
        crowApproaching = true;
        float angle = index * Mathf.PI * 2f / 3f;
        crowLandingOffset = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)) * .35f;
        biteAmount = 1f / 3f;
    }
    public bool IsClimbing => climbing;
    public bool IsOnBag => onBag;
    public void ConfigureBagSlot(int index, int count, bool climb)
    {
        hasSlot = true; wantsToClimb = climb;
        slotAngle = index * Mathf.PI * 2f / count;
        beginAt = Time.time + Random.Range(.15f, 2.8f);
        climbDelay = beginAt + Random.Range(.8f, 4.5f);
        climbDuration = Random.Range(1.2f, 1.9f);
        nextShuffle = Time.time + Random.Range(5f, 12f);
        nextBite = beginAt + Random.Range(1.5f, 3f);
        biteAmount = 1f / Mathf.Max(1, count);
    }
    private Vector3 SlotPoint(bool upper)
    {
        Bounds b = Target.WorldBounds;
        Vector3 radial = new Vector3(Mathf.Cos(slotAngle), 0, Mathf.Sin(slotAngle));
        float radius = upper ? Mathf.Min(b.extents.x, b.extents.z) * .4f : Mathf.Max(b.extents.x,b.extents.z) + .45f;
        Vector3 result = b.center + radial * radius;
        result.y = upper ? b.max.y + .08f : Target.transform.position.y + .1f;
        return result;
    }
    public void Initialize(GrainContainer target, bool crow, RuntimeAnimatorController fly, RuntimeAnimatorController eat)
    {
        Target = target; previousTargetPosition = target.transform.position; IsCrow = crow; speed = crow ? 7f : Random.Range(1.8f, 2.6f);
        beginAt = Time.time + Random.Range(.1f, 1.5f);
        climbDuration = 1.45f;
        animator = GetComponentInChildren<Animator>(); flying = fly; feeding = eat;
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = Random.Range(.88f, 1.12f);
            if (flying != null) animator.runtimeAnimatorController = flying;
        }
        nextBite = Time.time + 3f;
        var collider = gameObject.AddComponent<SphereCollider>(); collider.radius = .55f;
    }
    private void Update()
    {
        if (fleeing)
        {
            Vector3 flat = Vector3.ProjectOnPlane(fleeDirection, Vector3.up);
            if (flat.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 540f * Time.deltaTime);
            float acceleration = Mathf.Clamp01((Time.time - (fleeUntil - 3f)) / .3f);
            transform.position += (IsCrow ? fleeDirection : transform.forward) * speed * acceleration * Time.deltaTime;
            if (!IsCrow) { Vector3 p = transform.position; p.y = Mathf.MoveTowards(p.y, fleeGroundY, 2.5f * Time.deltaTime); transform.position = p; }
            if (Time.time >= fleeUntil) Destroy(gameObject);
            return;
        }
        if (Target == null || !Target.isActiveAndEnabled || Target.Kilograms <= .001f) { ScaredAway(); return; }
        float dt = Time.deltaTime;
        if (dt > .0001f)
            targetSpeed = Mathf.Min(100f, Vector3.Distance(Target.transform.position,previousTargetPosition) / dt);
        previousTargetPosition = Target.transform.position;
        if (IsCrow && crowPerched)
        {
            EatFromTarget();
            return;
        }
        if (!IsCrow && (Time.time < beginAt || Time.time < restUntil))
        {
            if (animator != null) animator.SetFloat("Speed", 0f);
            return;
        }
        if (hasSlot && !climbing && Time.time >= nextShuffle)
        {
            slotAngle += Random.Range(-.4f, .4f);
            nextShuffle = Time.time + Random.Range(5f, 12f);
        }
        Vector3 destination = Target.WorldBounds.center + Vector3.up * (IsCrow ? Target.WorldBounds.extents.y - .05f : -Target.WorldBounds.extents.y + .15f);
        if (!IsCrow) destination.y = Target.transform.position.y + .1f;
        if (hasSlot && !IsCrow)
        {
            destination = SlotPoint(onBag);
            if (climbing)
            {
                float t = Mathf.Clamp01((Time.time - climbStart) / climbDuration);
                Vector3 end = SlotPoint(true);
                Vector3 p = Vector3.Lerp(climbFrom, end, Mathf.SmoothStep(0,1,t));
                p.y += Mathf.Sin(t * Mathf.PI) * .18f;
                transform.position = p;
                Vector3 inward = Target.WorldBounds.center - p; inward.y = 0;
                if (inward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(inward) * Quaternion.Euler(-35f * Mathf.Sin(Mathf.PI*t),0,0);
                if (t >= 1) { climbing = false; onBag = true; feedingNow = false; nextBite = Time.time + 3f; }
                return;
            }
            if (wantsToClimb && !onBag && Time.time >= climbDelay && Vector3.Distance(transform.position,destination) < .2f)
            {
                climbing = true; climbStart = Time.time; climbFrom = transform.position;
                if (animator != null) { animator.ResetTrigger("IdleBreak"); animator.SetFloat("Speed", .65f); animator.CrossFadeInFixedTime("Blend Tree", .12f); }
                return;
            }
        }
        if (IsCrow)
        {
            destination = CrowPerchPoint();
            if (crowApproaching)
            {
                if (Vector3.Distance(transform.position,crowWaypoint) > .3f) destination = crowWaypoint;
                else crowApproaching = false;
            }
        }
        Vector3 delta = destination - transform.position;
        if (delta.magnitude > (crowApproaching ? .15f : hasSlot ? .18f : IsCrow ? .08f : 1.3f))
        {
            feedingNow = false;
            if (IsCrow && animator != null && flying != null && animator.runtimeAnimatorController != flying) animator.runtimeAnimatorController = flying;
            if (!IsCrow && animator != null) animator.SetFloat("Speed", 1f);
            Vector3 step = Vector3.MoveTowards(transform.position, destination, (IsCrow ? Mathf.Max(speed,targetSpeed + 5f) : speed) * Time.deltaTime);
            Vector3 dir = step - transform.position;
            if (dir.sqrMagnitude > .0001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.ProjectOnPlane(dir, Vector3.up).sqrMagnitude > .001f ? Vector3.ProjectOnPlane(dir, Vector3.up) : transform.forward), Time.deltaTime * 5f);
            transform.position = step;
            nextBite = Time.time + 3f;
        }
        else
        {
            if (hasSlot)
            {
                Vector3 inward = Target.WorldBounds.center - transform.position; inward.y = 0;
                if (inward.sqrMagnitude > .001f) transform.rotation = Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(inward),Time.deltaTime*5f);
            }
            if (!feedingNow)
            {
                feedingNow = true;
                if (IsCrow) { crowPerched = true; crowApproaching = false; nextBite = Time.time + 3f; }
                if (animator != null)
                {
                    if (IsCrow && feeding != null) animator.runtimeAnimatorController = feeding;
                    else if (!IsCrow) { animator.SetFloat("Speed", 0f); animator.SetTrigger("IdleBreak"); }
                }
            }
            EatFromTarget();
        }
    }
    public void ScaredAway(Vector3? threatPosition = null)
    {
        if (fleeing) return;
        if (IsCrow && animator != null && flying != null) animator.runtimeAnimatorController = flying;
        if (!IsCrow && animator != null)
        {
            animator.ResetTrigger("IdleBreak");
            animator.SetFloat("Speed", 1f);
            animator.CrossFadeInFixedTime("Blend Tree", .12f);
        }
        fleeGroundY = Target != null ? Target.transform.position.y + .1f : transform.position.y;
        climbing = false; onBag = false; crowPerched = false;
        fleeing = true; Target = null; fleeUntil = Time.time + 3f;
        Vector3 away = threatPosition.HasValue ? transform.position - threatPosition.Value : -transform.forward;
        away.y = 0;
        if (away.sqrMagnitude < .001f) away = -transform.forward;
        fleeDirection = (away.normalized + (IsCrow ? Vector3.up : Vector3.zero)).normalized;
        GetComponent<Collider>().enabled = false;
    }
}

public sealed class GrainPestDirector : MonoBehaviour
{
    public GameObject ratPrefab, crowPrefab;
    public GrainContainer tutorialBagTemplate;
    public Material tutorialBagMaterial;
    private Texture2D markerTexture;
    public RuntimeAnimatorController ratAnimation, crowFlyAnimation, crowEatAnimation;
    public HangarKeyDoor hangar3, hangar4;
    public Camera playerCamera;
    public PlayerInteractor player;
    [SerializeField, Min(1f)] private float minInterval = 60f, maxInterval = 180f;
    [SerializeField, Min(1f)] private float minAttackDuration = 20f, maxAttackDuration = 35f;
    private float nextAttack, attackEndsAt;
    private bool encounterActive;
    private readonly System.Collections.Generic.List<GrainPest> encounterPests = new System.Collections.Generic.List<GrainPest>();
    public float LastAttackEnded { get; private set; } = -1f;
    public float SecondsUntilNextAttack => encounterActive ? 0f : Mathf.Max(0f, nextAttack - Time.time);
    private const string IntroId = "farm.pests.intro";
    private const string CrowIntroId = "farm.pests.crow.intro";
    private bool crowIntroDone, crowLesson;
    private bool introRunning, introDone, showBroomHint;
    private PlayerController introController;
    private bool introControllerWasEnabled;
#if UNITY_EDITOR
    private bool replayIntroForTesting, replayCrowForTesting;
    public void ReplayCrowIntroForTesting() { if (introRunning) return; replayCrowForTesting = true; crowIntroDone = false; }
    public void ReplayIntroForTesting() { if (introRunning) return; replayIntroForTesting = true; introDone = false; }
#endif
    private void Start() { Schedule(); CreateMarker(); }
    private void CreateMarker()
    {
        markerTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        markerTexture.name = "Pulsing red attack marker";
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float r = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
            float alpha = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.70f, 1f, r));
            markerTexture.SetPixel(x, y, new Color(1f, .04f, .025f, alpha));
        }
        markerTexture.Apply(); markerTexture.hideFlags = HideFlags.HideAndDontSave;
    }
    private void OnDestroy() { if (markerTexture != null) Destroy(markerTexture); }
    private void Schedule() => nextAttack = Time.time + Random.Range(Mathf.Max(1f,minInterval), Mathf.Max(minInterval,maxInterval));
    private void EndEncounter()
    {
        foreach (var pest in encounterPests) if (pest != null) pest.ScaredAway();
        encounterPests.Clear();
        encounterActive = false;
        LastAttackEnded = Time.time;
        Schedule();
    }
    private void Update()
    {
        if (!SaveSystem.savesLoaded || SaveSystem.Data == null) return;
        if (player == null) player = FindFirstObjectByType<PlayerInteractor>();
        if (playerCamera == null && player != null)
        {
            var controller = player.GetComponent<PlayerController>();
            if (controller != null) playerCamera = controller.characterCamera;
        }
        bool introAlreadySeen = BuriedFind.WasFound(SaveSystem.Data, IntroId);
#if UNITY_EDITOR
        if (replayIntroForTesting) introAlreadySeen = false;
#endif
        if (!introDone && !introRunning && hangar3 != null && hangar3.IsUnlocked && !introAlreadySeen &&
            player != null && Vector3.Distance(player.transform.position, hangar3.transform.position) < 12f)
        { StartCoroutine(Introduce()); return; }
        bool crowSeen = BuriedFind.WasFound(SaveSystem.Data, CrowIntroId);
#if UNITY_EDITOR
        if (replayCrowForTesting) crowSeen = false;
#endif
        if (!crowIntroDone && !introRunning && !crowSeen && hangar4 != null && hangar4.IsUnlocked &&
            player != null && Vector3.Distance(player.transform.position,hangar4.transform.position) < 14f)
        { StartCoroutine(IntroduceCrows()); return; }
        if (introRunning) return;
        if (encounterActive)
        {
            encounterPests.RemoveAll(p => p == null || p.Target == null);
            if (Time.time >= attackEndsAt || encounterPests.Count == 0) EndEncounter();
            return;
        }
        if (Time.time < nextAttack) return;
        Schedule();
        bool ratsAvailable = hangar3 != null && hangar3.IsUnlocked;
        bool crowsAvailable = hangar4 != null && hangar4.IsUnlocked;
        if (!ratsAvailable && !crowsAvailable) return;
        var targets = FindObjectsByType<GrainContainer>(FindObjectsSortMode.None)
            .Where(x => x.isActiveAndEnabled && x.Kilograms > .01f).ToArray();
        if (targets.Length == 0) return;
        var vehicleCargo = crowsAvailable ? targets.Where(x =>
        {
            var vehicle = x.GetComponentInParent<BicycleController>();
            return vehicle != null && vehicle.IsRidden;
        }).ToArray() : System.Array.Empty<GrainContainer>();
        // Driving cargo remains eligible even when its colliders are disabled or it is parented to a rack.
        bool attackVehicle = vehicleCargo.Length > 0;
        var candidates = attackVehicle ? vehicleCargo : targets;
        var target = candidates[Random.Range(0, candidates.Length)];
        bool crow = attackVehicle || (crowsAvailable && (!ratsAvailable || Random.value < .5f));
        if (crow) Spawn(target, true);
        else SpawnRatGroup(target, false);
    }
    private void SpawnRatGroup(GrainContainer bag, bool nearby)
    {
        const int count = 6;
        for (int i = 0; i < count; i++)
        {
            var rat = Spawn(bag, false);
            if (rat == null) continue;
            rat.ConfigureBagSlot(i, count, i == 1 || i == 4);
            if (nearby)
            {
                float angle = i * Mathf.PI * 2f / count;
                Bounds b = bag.WorldBounds;
                float radius = Mathf.Max(b.extents.x,b.extents.z) + .45f;
                Vector3 point = b.center + new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                point.y = bag.transform.position.y + .1f;
                rat.transform.position = point;
            }
        }
    }
    private GrainPest Spawn(GrainContainer target, bool crow)
    {
        var prefab = crow ? crowPrefab : ratPrefab;
        if (prefab == null || target == null) return null;
        Vector3 direction = Random.insideUnitSphere; direction.y = 0; direction = direction.sqrMagnitude < .01f ? Vector3.forward : direction.normalized;
        Vector3 pos = target.transform.position + direction * (crow ? 14f : 8f) + Vector3.up * (crow ? 9f : .15f);
        var go = Instantiate(prefab, pos, Quaternion.identity);
        if (!crow) go.transform.localScale *= 1.5f;
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        var pest = go.AddComponent<GrainPest>();
        pest.Initialize(target, crow, crow ? crowFlyAnimation : ratAnimation, crow ? crowEatAnimation : ratAnimation);
        if (!encounterActive)
        {
            encounterActive = true;
            attackEndsAt = Time.time + Random.Range(Mathf.Max(1f,minAttackDuration),Mathf.Max(minAttackDuration,maxAttackDuration));
        }
        encounterPests.Add(pest);
        return pest;
    }
    private IEnumerator Introduce()
    {
        if (encounterActive) EndEncounter();
        introRunning = true;
        crowLesson = false;
        if (player == null || playerCamera == null || tutorialBagTemplate == null || !player.BeginDiscovery(this))
        { introRunning = false; yield break; }
        AudioManager.Instance?.PlayAttackMusic();
        introController = player.GetComponent<PlayerController>();
        introControllerWasEnabled = introController != null && introController.enabled;
        if (introControllerWasEnabled) introController.enabled = false;
        yield return new WaitForSeconds(1.7f);
        var building = hangar3.GetComponentInParent<HangarGrainVisibility>();
        if (building == null) { RestoreIntroController(); player.EndDiscovery(this); introRunning = false; yield break; }
        var bags = new GrainContainer[2];
        for (int i = 0; i < bags.Length; i++)
        {
            var go = Instantiate(tutorialBagTemplate.gameObject);
            go.name = "Rat tutorial grain bag";
            go.transform.position = building.transform.position + building.transform.forward * 19.5f +
                building.transform.right * (i == 0 ? -2f : 2f);
            go.SetActive(true);
            bags[i] = go.GetComponent<GrainContainer>();
            bags[i].SetDemonstrationFill(bags[i].CapacityKg * .5f);
        }
        foreach (var bag in bags) SpawnRatGroup(bag, true);
        for (int i = 0; i < bags.Length; i++)
        {
            Vector3 delta = bags[i].WorldBounds.center - playerCamera.transform.position;
            Quaternion start = playerCamera.transform.rotation;
            Quaternion end = delta.sqrMagnitude > .01f ? Quaternion.LookRotation(delta) : start;
            float t = 0;
            while (t < 1f) { t += Time.deltaTime; playerCamera.transform.rotation = Quaternion.Slerp(start,end,Mathf.SmoothStep(0,1,t)); yield return null; }
            yield return new WaitForSeconds(1.2f);
        }
        Vector3 midpoint = (bags[0].WorldBounds.center + bags[1].WorldBounds.center) * .5f;
        Quaternion finish = Quaternion.LookRotation(midpoint - playerCamera.transform.position);
        float back = 0; Quaternion from = playerCamera.transform.rotation;
        while (back < .7f) { back += Time.deltaTime; playerCamera.transform.rotation = Quaternion.Slerp(from,finish,Mathf.SmoothStep(0,1,back/.7f)); yield return null; }
        Vector3 euler = finish.eulerAngles;
        player.transform.rotation = Quaternion.Euler(0, euler.y, 0);
        playerCamera.transform.rotation = finish;
        var controller = player.GetComponent<PlayerController>();
        if (controller != null) controller.ResetLook();
        RestoreIntroController();
        player.EndDiscovery(this);
        bool saveIntro = true;
#if UNITY_EDITOR
        if (replayIntroForTesting) { saveIntro = false; replayIntroForTesting = false; }
#endif
        if (saveIntro && SaveSystem.Data != null && !BuriedFind.WasFound(SaveSystem.Data, IntroId))
        {
            var data = SaveSystem.Data; var ids = data.foundSecrets ?? System.Array.Empty<string>();
            System.Array.Resize(ref ids, ids.Length + 1); ids[ids.Length-1] = IntroId; data.foundSecrets = ids; SaveSystem.SaveData();
        }
        introDone = true; introRunning = false; showBroomHint = true; Schedule();
    }
    private IEnumerator IntroduceCrows()
    {
        if (encounterActive) EndEncounter();
        introRunning = true; crowLesson = true;
        if (player == null || playerCamera == null || tutorialBagTemplate == null || !player.BeginDiscovery(this))
        { introRunning = false; yield break; }
        AudioManager.Instance?.PlayAttackMusic();
        introController = player.GetComponent<PlayerController>();
        introControllerWasEnabled = introController != null && introController.enabled;
        if (introControllerWasEnabled) introController.enabled = false;
        yield return new WaitForSeconds(1.7f);
        var building = hangar4.GetComponentInParent<HangarGrainVisibility>();
        if (building == null) { RestoreIntroController(); player.EndDiscovery(this); introRunning = false; yield break; }
        var root = building.transform;
        var go = Instantiate(tutorialBagTemplate.gameObject);
        go.name = "Crow tutorial grain bag";
        go.transform.position = root.position + root.forward * 28f;
        go.SetActive(true);
        var bag = go.GetComponent<GrainContainer>();
        bag.SetDemonstrationFill(bag.CapacityKg * .5f);
        Quaternion startLook = playerCamera.transform.rotation;
        Quaternion targetLook = Quaternion.LookRotation(bag.WorldBounds.center - playerCamera.transform.position);
        float turn = 0;
        while (turn < .8f) { turn += Time.deltaTime; playerCamera.transform.rotation = Quaternion.Slerp(startLook,targetLook,Mathf.SmoothStep(0,1,turn/.8f)); yield return null; }
        for (int i = 0; i < 3; i++)
        {
            var crow = Spawn(bag, true);
            if (crow != null)
            {
                Vector3 arrival = root.position + root.forward * (43f+i*3f) + root.right*(i-1)*2f + Vector3.up*(8f+i);
                Vector3 doorway = root.position + root.forward*29f + root.right*(i-1)*.7f + Vector3.up*3.8f;
                crow.ConfigureCrowArrival(arrival,doorway,i);
            }
            yield return new WaitForSeconds(.7f);
        }
        yield return new WaitForSeconds(3f);
        player.transform.rotation = Quaternion.Euler(0,targetLook.eulerAngles.y,0);
        playerCamera.transform.rotation = targetLook;
        if (introController != null) introController.ResetLook();
        RestoreIntroController(); player.EndDiscovery(this);
        bool save = true;
#if UNITY_EDITOR
        if (replayCrowForTesting) { save = false; replayCrowForTesting = false; }
#endif
        if (save && SaveSystem.Data != null && !BuriedFind.WasFound(SaveSystem.Data,CrowIntroId))
        {
            var data = SaveSystem.Data; var ids = data.foundSecrets ?? System.Array.Empty<string>();
            System.Array.Resize(ref ids,ids.Length+1); ids[ids.Length-1]=CrowIntroId; data.foundSecrets=ids; SaveSystem.SaveData();
        }
        crowIntroDone = true; introRunning = false; showBroomHint = true; Schedule();
    }
    private void RestoreIntroController()
    {
        if (introController != null && introControllerWasEnabled) introController.enabled = true;
        introController = null; introControllerWasEnabled = false;
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        if (encounterActive) EndEncounter();
        RestoreIntroController();
        introRunning = false;
        if (player != null) player.EndDiscovery(this);
    }
    private void OnGUI()
    {
        if (showBroomHint && player != null)
        {
            var tools = player.GetComponent<PlayerCombatTools>();
            if (tools != null && tools.Equipped == PlayerCombatTools.Tool.Broom) showBroomHint = false;
        }
        if (introRunning || showBroomHint)
        {
            var hintStyle = new GUIStyle(GUI.skin.box) { fontSize = Mathf.Clamp(Screen.height / 35, 16, 28), wordWrap = true, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width*.15f, Screen.height*.76f, Screen.width*.7f, 75), (crowLesson ? "Crows are stealing your grain! Press 2 for the broom, then left-click to scare them away." : "Rats are eating your grain! Press 2 to equip the broom, then left-click to scare them away."), hintStyle);
        }
        if (playerCamera == null) return;
        if (markerTexture == null) return;
        var marked = new System.Collections.Generic.HashSet<GrainContainer>();
        foreach (var pest in FindObjectsByType<GrainPest>(FindObjectsSortMode.None))
        {
            if (pest.Target == null || !marked.Add(pest.Target)) continue;
            Vector3 p = playerCamera.WorldToViewportPoint(pest.Target.WorldBounds.center + Vector3.up * (pest.Target.WorldBounds.extents.y + 1f));
            Vector2 direction = new Vector2(p.x - .5f, p.y - .5f);
            if (p.z < 0f) direction = -direction;
            if (direction.sqrMagnitude < .0001f && p.z < 0f) direction = Vector2.down;
            float edge = Mathf.Max(Mathf.Abs(direction.x) / .46f, Mathf.Abs(direction.y) / .44f);
            if (p.z < 0f || edge > 1f) direction /= Mathf.Max(edge, .0001f);
            Vector2 screen = new Vector2((.5f + direction.x) * Screen.width, (.5f - direction.y) * Screen.height);
            float size = 20f + 7f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 5f));
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, .3f);
            GUI.DrawTexture(new Rect(screen.x-size*.5f, screen.y-size*.5f, size, size), markerTexture);
            GUI.color = previous;
        }
    }
}
