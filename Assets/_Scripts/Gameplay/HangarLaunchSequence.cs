using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering.HighDefinition;

public sealed class HangarLaunchSequence : MonoBehaviour
{
    public HangarKeyDoor hangar;
    public GrainPile grain;
    public Transform clearancePoint, roomDoor, switchHandle, revealDoor, rocketDoor;
    public Transform switchView, revealView, approachView, cabinView, lookBackView, cockpitRevealView;
    public float revealLift = 11f;
    public float hatchOpenAngle = 105f;
    public Light roomLight;
    public Renderer lampGlow;
    public AudioClip readyVoice, threeVoice, twoVoice, oneVoice, protestVoice;
    public string endingScene = "Last Scene";
    public bool DoorOpen { get; private set; }
    public bool Running { get; private set; }
    private SavedCutsceneClock saveClock;
    public float SavedTime => saveClock?.Elapsed ?? 0;
    public bool IsSeeking => saveClock?.IsSeeking ?? false;
    public void RestoreDoor(bool open)
    {
        DoorOpen = open;
        if (roomDoor != null) roomDoor.localRotation = doorClosed * Quaternion.Euler(0, open ? -100 : 0, 0);
    }
    public bool ResumeSaved(PlayerInteractor player, float time)
    {
        if (Running || player == null || !player.BeginDiscovery(this)) return false;
        DoorOpen = true; roomDoor.localRotation = doorClosed * Quaternion.Euler(0,-100,0);
        actor = player; Running = true; saveClock = new SavedCutsceneClock(time);
        StartCoroutine(saveClock.Run(Launch())); return true;
    }
    private readonly HashSet<GameplayCanvas> hud = new HashSet<GameplayCanvas>();
    private void LateUpdate() { if (overlay != null) overlay.GetComponent<Canvas>().enabled = Pause.Instance == null || !Pause.Instance.paused; }
    private Quaternion doorClosed, leverOff, hatchClosed;
    private Vector3 revealClosed;
    private Camera cinematic, original;
    private GameObject overlay;
    private Text subtitle;
    private Image fade;
    private AudioSource voice;
    private PlayerInteractor actor;
    private readonly Dictionary<Behaviour,bool> suspended = new Dictionary<Behaviour,bool>();
    private readonly Dictionary<GameObject,bool> hidden = new Dictionary<GameObject,bool>();
    private bool cursorVisible;
    private CursorLockMode cursorLock;
    private void Awake()
    {
        doorClosed=roomDoor.localRotation;leverOff=switchHandle.localRotation;
        hatchClosed=rocketDoor.localRotation;revealClosed=revealDoor.localPosition;
        roomLight.enabled=false;if(lampGlow!=null)lampGlow.enabled=false;
    }
    public bool BypassRestrictions => GameManager.Instance != null && GameManager.Instance.bypassFinalRoomRestrictions;
    public bool CanOpen
    {
        get
        {
            if(BypassRestrictions || DoorOpen)return true;
            if(hangar==null||!hangar.IsUnlocked||clearancePoint==null)return false;
            foreach(var pile in FindObjectsByType<GrainPile>(FindObjectsSortMode.None))
                if (GrainBlocksDoorway(pile, clearancePoint.position)) return false;
            return true;
        }
    }
    public static bool GrainBlocksDoorway(GrainPile pile, Vector3 point)
    {
        if (pile == null || !pile.isActiveAndEnabled) return false;
        pile.Initialize();
        Vector3 local = pile.transform.InverseTransformPoint(point);
        float half = pile.Size * .5f;
        if (Mathf.Abs(local.x) > half || Mathf.Abs(local.z) > half || !pile.IsInsideBoundary(point)) return false;
        // SurfaceHeight includes the pile's world base and underlying floor, even
        // for an empty cell. Only actual grain can obstruct the doorway.
        float thickness = pile.SampleLocalHeight(local.x, local.z) * Mathf.Abs(pile.WorldScale.y);
        return thickness > .01f && pile.SurfaceHeight(point) > point.y + .15f;
    }
    public bool UseDoor()
    {
        if(Running||DoorOpen||!CanOpen||Time.timeScale<=0)return false;
        DoorOpen=true;GameplayAudio.Instance?.OpenElectricalDoor();StartCoroutine(OpenRoom());return true;
    }
    private IEnumerator OpenRoom(){yield return Animate(.8f,t=>roomDoor.localRotation=doorClosed*Quaternion.Euler(0,-100*t,0));}
    public bool UseSwitch(PlayerInteractor interactor)
    {
        if(Running||(!DoorOpen && !BypassRestrictions)||interactor==null||interactor.HandsOccupied||Time.timeScale<=0)return false;
        if(!Application.CanStreamedLevelBeLoaded(endingScene)){Debug.LogError("Launch destination is missing from Build Settings: "+endingScene,this);return false;}
        if(!interactor.BeginDiscovery(this))return false;
        actor=interactor;Running=true;saveClock=new SavedCutsceneClock();StartCoroutine(saveClock.Run(Launch()));return true;
    }
    private void Suspend(Behaviour b){if(b==null||suspended.ContainsKey(b))return;suspended[b]=b.enabled;b.enabled=false;}
    private void Hide(GameObject go){if(go==null||hidden.ContainsKey(go))return;hidden[go]=go.activeSelf;go.SetActive(false);}
    private IEnumerator Launch()
    {
        var player=actor.GetComponent<PlayerController>();original=player.characterCamera;
        cursorLock=Cursor.lockState;cursorVisible=Cursor.visible;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        Suspend(player);Suspend(actor);Suspend(player.GetComponent<PlayerBicycleInteraction>());Suspend(player.GetComponent<PlayerCombatTools>());Suspend(player.Stamina);
        if(player.HeldInstrument!=null)Hide(player.HeldInstrument.gameObject);
        foreach(var canvas in FindObjectsByType<GameplayCanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { hud.Add(canvas); canvas.SetCutsceneHudHidden(this, true); }
        foreach(var sign in GetComponentsInChildren<TextMesh>())if(sign.text.StartsWith("ELECTRICAL"))Hide(sign.gameObject);
        var cameraObject=new GameObject("Electrical room cinematic camera");cinematic=cameraObject.AddComponent<Camera>();cinematic.CopyFrom(original);cinematic.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);
        cinematic.nearClipPlane=.03f;var hd=cameraObject.AddComponent<HDAdditionalCameraData>();var originalHD=original.GetComponent<HDAdditionalCameraData>();if(originalHD!=null)originalHD.CopyTo(hd);
        Suspend(original);voice=cameraObject.AddComponent<AudioSource>();voice.spatialBlend=0;voice.playOnAwake=false;CreateOverlay();
        yield return MoveCamera(switchView,.75f);
        yield return Animate(.55f,t=>switchHandle.localRotation=leverOff*Quaternion.Euler(125*t,0,0));
        GameplayAudio.Instance?.SwitchElectrical();
        roomLight.enabled=true;if(lampGlow!=null)lampGlow.enabled=true;
        yield return Animate(.7f,t=>roomLight.intensity=Mathf.Lerp(0,180,t));
        GameAchievements.ElectricalRoomPowered();
        yield return TurnToward(revealView,.8f);
        yield return saveClock.Animate(.15f, _ => { });
        GameplayAudio.Instance?.OpenRocketDoor();
        yield return Animate(1.4f,t=>revealDoor.localPosition=revealClosed+Vector3.up*revealLift*t);
        // Begin walking as the hatch opens, keeping the movement and door in sync.
        yield return WalkToOpening(revealView,2.1f);
        yield return LookAroundOpening();
        yield return WalkTo(approachView,2.2f);
        yield return EnterCabin(cabinView,2.5f);
        // Inspect the controls while the hatch closes behind the player.
        Quaternion cabinLook = cinematic.transform.rotation;
        GameplayAudio.Instance?.CloseRocketDoor();
        yield return Animate(1.2f,t=>{
            rocketDoor.localRotation=hatchClosed*Quaternion.Euler(0,hatchOpenAngle*(1-t),0);
            cinematic.transform.rotation=cabinLook*Quaternion.Euler(-2f*Mathf.Sin(t*Mathf.PI), -8f*Mathf.Sin(t*Mathf.PI), 0);
        });
        // The latch catches their attention: turn sharply toward the door.
        if(lookBackView!=null)yield return MoveCamera(lookBackView,.48f);
        yield return saveClock.Animate(.3f, _ => { });
        subtitle.text="GET READY FOR LAUNCH";Play(readyVoice);
        yield return saveClock.Animate(.5f, _ => { });
        subtitle.text="GET READY FOR LAUNCH\n3...";Play(threeVoice);
        Vector3 windowStart=cinematic.transform.position;
        Quaternion windowRotation=cinematic.transform.rotation;
        yield return Animate(1.15f,t=>{
            cinematic.transform.position=windowStart+windowRotation*Vector3.forward*(.32f*t);
            cinematic.transform.rotation=windowRotation*Quaternion.Euler(-1.5f*t,2.5f*t,0);
        });
        subtitle.text="2...\n<size=25>— No, no, let me out!</size>";Play(twoVoice);if(protestVoice!=null)voice.PlayOneShot(protestVoice);
        GameplayAudio.Instance?.StartRocket();
        Vector3 closePosition=cinematic.transform.position;
        Quaternion closeRotation=cinematic.transform.rotation;
        yield return Animate(2.2f,t=>{
            cinematic.transform.position=Vector3.Lerp(closePosition,windowStart,t);
            cinematic.transform.rotation=Quaternion.Slerp(closeRotation,windowRotation*Quaternion.Euler(3,-4,0),t);
        });
        subtitle.text="1...\n<size=25>— No, no, let me out!</size>";Play(oneVoice);
        Quaternion lastLook=cinematic.transform.rotation;
        yield return Animate(1.1f,t=>cinematic.transform.rotation=Quaternion.Slerp(lastLook,windowRotation,t));
        var rest=cinematic.transform.localPosition;
        yield return Animate(.8f,t=>{cinematic.transform.localPosition=rest+new Vector3(Mathf.Sin(t*89),Mathf.Sin(t*117),0)*.025f*t;fade.color=new Color(0,0,0,t);});
        subtitle.text="";
        if (WorldSaveRuntime.Instance != null) WorldSaveRuntime.Instance.DestinationScene = endingScene;
        SaveSystem.SaveData();
        var load=SceneManager.LoadSceneAsync(endingScene,LoadSceneMode.Single);if(load!=null)yield return load;
    }
    private IEnumerator TurnToward(Transform target, float duration)
    {
        Quaternion start = cinematic.transform.rotation;
        Vector3 direction = target.position - cinematic.transform.position;
        Quaternion end = direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : target.rotation;
        yield return Animate(duration, t => cinematic.transform.rotation = Quaternion.Slerp(start, end, t));
    }
    private IEnumerator WalkToOpening(Transform target, float duration)
    {
        Vector3 start = cinematic.transform.position;
        Quaternion startRotation = cinematic.transform.rotation;
        Vector3 right = cinematic.transform.right;
        yield return Animate(duration, t =>
        {
            float envelope = Mathf.Sin(Mathf.PI * t);
            float steps = t * Mathf.PI * 8f;
            Vector3 sway = right * (Mathf.Sin(steps) * .018f * envelope)
                + Vector3.up * (Mathf.Abs(Mathf.Sin(steps)) * .035f * envelope);
            cinematic.transform.SetPositionAndRotation(Vector3.Lerp(start, target.position, t) + sway,
                Quaternion.Slerp(startRotation, target.rotation, t) * Quaternion.Euler(0, 0, Mathf.Sin(steps) * .2f * envelope));
            float doorProgress = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t * 2.5f));
            rocketDoor.localRotation = hatchClosed * Quaternion.Euler(0, hatchOpenAngle * doorProgress, 0);
        });
    }
    private IEnumerator WalkTo(Transform target, float duration)
    {
        Vector3 start = cinematic.transform.position;
        Quaternion startRotation = cinematic.transform.rotation;
        Vector3 right = cinematic.transform.right;
        yield return Animate(duration, t =>
        {
            float envelope = Mathf.Sin(Mathf.PI * t);
            float steps = t * Mathf.PI * 8f;
            Vector3 sway = right * (Mathf.Sin(steps) * .018f * envelope)
                + Vector3.up * (Mathf.Abs(Mathf.Sin(steps)) * .035f * envelope);
            cinematic.transform.SetPositionAndRotation(Vector3.Lerp(start, target.position, t) + sway,
                Quaternion.Slerp(startRotation, target.rotation, t) * Quaternion.Euler(0, 0, Mathf.Sin(steps) * .2f * envelope));
        });
    }
    private IEnumerator EnterCabin(Transform target, float duration)
    {
        Vector3 start = cinematic.transform.position;
        Quaternion startRotation = cinematic.transform.rotation;
        Vector3 right = cinematic.transform.right;
        yield return Animate(duration, t =>
        {
            float envelope = Mathf.Sin(Mathf.PI * t);
            float steps = t * Mathf.PI * 7f;
            Vector3 sway = right * (Mathf.Sin(steps) * .012f * envelope)
                + Vector3.up * (Mathf.Abs(Mathf.Sin(steps)) * .025f * envelope);
            // One glance to each side, returning naturally to the forward view.
            float glance = Mathf.Sin(t * Mathf.PI * 2f) * envelope;
            Quaternion look = Quaternion.Euler(-2.5f * envelope, 7f * glance, .25f * glance);
            cinematic.transform.SetPositionAndRotation(Vector3.Lerp(start, target.position, t) + sway,
                Quaternion.Slerp(startRotation, target.rotation, t) * look);
        });
    }
    private IEnumerator LookAroundCabin()
    {
        Quaternion center = cinematic.transform.rotation;
        yield return Animate(.6f, t => cinematic.transform.rotation = center * Quaternion.Euler(-3*t, -8*t, -t));
        yield return saveClock.Animate(.18f, _ => { });
        Quaternion left = cinematic.transform.rotation;
        Quaternion right = center * Quaternion.Euler(2, 9, 1);
        yield return Animate(.85f, t => cinematic.transform.rotation = Quaternion.Slerp(left, right, t));
        yield return saveClock.Animate(.22f, _ => { });
        Quaternion from = cinematic.transform.rotation;
        yield return Animate(.5f, t => cinematic.transform.rotation = Quaternion.Slerp(from, center, t));
    }
    private IEnumerator LookAroundOpening()
    {
        Quaternion center = cinematic.transform.rotation;
        yield return Animate(.45f, t => cinematic.transform.rotation = center * Quaternion.Euler(-2*t, -7*t, 0));
        yield return saveClock.Animate(.2f, _ => { });
        yield return Animate(.7f, t => cinematic.transform.rotation = center * Quaternion.Euler(-2 + 3*t, -7 + 14*t, 0));
        yield return saveClock.Animate(.2f, _ => { });
        Quaternion from = cinematic.transform.rotation;
        yield return Animate(.45f, t => cinematic.transform.rotation = Quaternion.Slerp(from, center, t));
    }
    private void Play(AudioClip clip){if(clip!=null && !IsSeeking)voice.PlayOneShot(clip);}
    private IEnumerator MoveCamera(Transform target,float duration){var p=cinematic.transform.position;var r=cinematic.transform.rotation;yield return Animate(duration,t=>cinematic.transform.SetPositionAndRotation(Vector3.Lerp(p,target.position,t),Quaternion.Slerp(r,target.rotation,t)));}
    private IEnumerator Animate(float duration,System.Action<float> action) => saveClock.Animate(duration, t => action(Mathf.SmoothStep(0,1,t)));
    private void CreateOverlay()
    {
        overlay=new GameObject("Launch subtitles",typeof(Canvas),typeof(CanvasScaler));var canvas=overlay.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
        var scaler=overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        var panel=new GameObject("Fade",typeof(RectTransform),typeof(Image));panel.transform.SetParent(overlay.transform,false);fade=panel.GetComponent<Image>();fade.color=Color.clear;fade.raycastTarget=false;var rect=panel.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var text=new GameObject("Subtitle",typeof(RectTransform),typeof(Text),typeof(Outline));text.transform.SetParent(overlay.transform,false);subtitle=text.GetComponent<Text>();subtitle.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");subtitle.fontSize=36;subtitle.alignment=TextAnchor.MiddleCenter;subtitle.color=Color.white;subtitle.supportRichText=true;subtitle.raycastTarget=false;rect=text.GetComponent<RectTransform>();rect.anchorMin=new Vector2(.1f,.04f);rect.anchorMax=new Vector2(.9f,.2f);rect.offsetMin=rect.offsetMax=Vector2.zero;
    }
    private void OnDisable()
    {
        StopAllCoroutines();if(!Running)return;Running=false;
        foreach(var pair in suspended)if(pair.Key!=null)pair.Key.enabled=pair.Value;
        foreach(var pair in hidden)if(pair.Key!=null)pair.Key.SetActive(pair.Value);
        foreach(var canvas in hud) if(canvas != null) canvas.SetCutsceneHudHidden(this, false);
        hud.Clear();suspended.Clear();hidden.Clear();if(actor!=null)actor.EndDiscovery(this);
        if(cinematic!=null)Destroy(cinematic.gameObject);if(overlay!=null)Destroy(overlay);
        Cursor.lockState=cursorLock;Cursor.visible=cursorVisible;
    }
}



