using System.Collections;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class GlobalManager : MonoBehaviour
{
    [Header("Current Player Data")] 
    [SerializeField] private PlayerData currentPlayerData;
    [SerializeField] private SettingsData currentSettingsData;
    [Space]
    [SerializeField] private GameObject noInternetConnection;
    public bool noInternet;
    [Header("Player Data")]
    public bool savesLoaded;
    [Header("Gameplay Introduction")]
    [SerializeField, Min(1)] private int introduceInGameplayUntilLevel = 5;
    
    public static GlobalManager Instance;


    private async void Awake()
    {
        if (!Instance)
        {
            Instance = this;
            
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        await LoadData();
        savesLoaded = true;
        await Task.Delay(1);
        Settings settings = FindAnyObjectByType<Settings>(FindObjectsInactive.Include);
        if (settings)
        {
            settings.Init();
        }
    }
    
    public async Task<Task> LoadData()
    {
        (PlayerData, SettingsData, ActivityData) data = await SaveSystem.LoadData();
        currentPlayerData = data.Item1;
        currentSettingsData = data.Item2;
        
        return Task.CompletedTask;
    }

    public static void RefreshAllDataHolders(bool animateCoins = false)
    {
        DataHolderUI[] dataHolders = FindDataHolders();
        for (int i = 0; i < dataHolders.Length; i++)
        {
            if (dataHolders[i] == null)
                continue;

            if (animateCoins)
                dataHolders[i].RefreshDataPlayer();
            else
                dataHolders[i].RefreshDataPlayerInstant();
        }
        
    }
    
    private static DataHolderUI[] FindDataHolders()
    {
#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<DataHolderUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return FindObjectsOfType<DataHolderUI>(true);
#endif
    }
    

    [Space, Header("For devs")]
    [SerializeField] private Text text;
    private float threshold;
    private int frameCount;
    private float deltaTime;

    private bool shouldPause;
    private void Update()
    {
        threshold += Time.deltaTime;
        frameCount++;
        deltaTime += Time.deltaTime;

        if (threshold >= 1f)
        {
            threshold = 0f;
            /*if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                if (!noInternetConnection.activeSelf)
                {
                    noInternetConnection.SetActive(true);
                    noInternet = true;
                }
            }
            else
            {
                if (noInternetConnection.activeSelf)
                {
                    noInternetConnection.SetActive(false);
                    noInternet = false;
                }
            }*/

            /*float fps = frameCount / deltaTime;
            float cpuUsage = (Time.unscaledDeltaTime * 1000); // CPU time in milliseconds per frame
     
            // RAM usage
            float totalMemory = SystemInfo.systemMemorySize; // Total RAM in MB
            float allocatedMemory =
                Profiler.GetAllocatedMemoryForGraphicsDriver() /
                (1024 * 1024); // Memory allocated to the graphics driver in MB
            float usedMemory = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024); // Used memory in MB
            float reservedMemory = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024); // Reserved memory in MB
            string debugText = $"CPU Usage: {cpuUsage} ms/frame\n" + $"FPS: {fps:F2}\n" + $"RAM Usage: {usedMemory} MB / {totalMemory} MB (Total Memory)\n" +
                               $"Graphics Driver Allocated Memory: {allocatedMemory} MB\n" +
                               $"Reserved Memory: {reservedMemory} MB";
     
            text.text = debugText;

            // Reset FPS counter
            frameCount = 0;
            deltaTime = 0;*/
        }
    }
    public static void PlaySound3D(AudioClip clip, Vector3 position, float volume = 1f, float spatialBlend = 1f, AudioMixerGroup outputGroup = null)
    {
        if (clip == null) return;

        GameObject tempGO = new GameObject("TempAudio_" + clip.name);
        tempGO.transform.position = position;

        AudioSource source = tempGO.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.outputAudioMixerGroup = outputGroup;

        source.Play();
        Destroy(tempGO, clip.length + 2f);
    }
    public void ChangeByAspectRatio()
    {
        float value = 800;
        if (Camera.main.aspect >= 1.8)
        {
            Debug.Log("19.5:9");
        }
        else if (Camera.main.aspect >= 1.7)
        {
            value = 1200;
            Debug.Log("16:9");
        }
        else if (Camera.main.aspect >= 1.5)
        {
            value = 1200;
            Debug.Log("3:2");
        }
        else
        {
            value = 1200;

            Debug.Log("4:3");
        }

        // objectsToChangeScale[i].localScale = new Vector3(value, value, value);
        //canvas.referenceResolution = new Vector2(value, 600);
    }
    private IEnumerator Time_0()
    {

        yield return new WaitForSecondsRealtime(0.1f);
        if (SceneManager.GetActiveScene().name.ToLower().Contains("boot") || SceneManager.GetActiveScene().name.ToLower().Contains("menu"))
        {
            Time.timeScale = 1f;
        }
        else
        {
            Time.timeScale = 0f;
        }
    }

    public static T CloneObject<T>(object obj)
    {
        string json = JsonUtility.ToJson(obj);
        return JsonUtility.FromJson<T>(json);
    }



    public float2 CalculateHoursAndMinutes(float time)
    {
        int totalSeconds = (int)time;

        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        //int seconds = totalSeconds & 60;

        return new float2(hours, minutes);
    }


    // -------------------------------------------------------------------------
    // Shared renderer color helpers.
    // Use these instead of renderer.material / renderer.materials when only color
    // changes are needed. MaterialPropertyBlock changes per-renderer color without
    // cloning the material, so batching/instancing stays much cleaner.
    // -------------------------------------------------------------------------
    private static MaterialPropertyBlock sharedColorBlock;
    private static readonly int[] ColorPropertyIds =
    {
        Shader.PropertyToID("_BaseColor"),
        Shader.PropertyToID("_Color"),
        Shader.PropertyToID("_Tint"),
        Shader.PropertyToID("_TintColor"),
        Shader.PropertyToID("_MainColor"),
        Shader.PropertyToID("_Base_Color")
    };

    public static void ApplySharedMaterialAndColor(Renderer renderer, Material sharedMaterial, Color color)
    {
        if (renderer == null)
            return;

        if (sharedMaterial != null && renderer.sharedMaterial != sharedMaterial)
            renderer.sharedMaterial = sharedMaterial;

        ApplyColorPropertyBlock(renderer, color);
    }

    public static void ApplyColorPropertyBlock(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        if (sharedColorBlock == null)
            sharedColorBlock = new MaterialPropertyBlock();

        renderer.GetPropertyBlock(sharedColorBlock);

        Material mat = renderer.sharedMaterial;
        SetColorProperties(sharedColorBlock, mat, color);

        renderer.SetPropertyBlock(sharedColorBlock);
        sharedColorBlock.Clear();
    }
    
    public static void ApplyColorOnlyToMaterial(Renderer renderer, string materialNameContains, Material sharedMaterial, Color color)
    {
        if (renderer == null)
            return;

        Material[] sharedMaterials = renderer.sharedMaterials;
        if (sharedMaterials == null || sharedMaterials.Length == 0)
            return;

        bool anyNamedMatch = false;
        bool materialsChanged = false;
        int fallbackIndex = -1;

        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material mat = sharedMaterials[i];
            if (mat == null)
                continue;

            if (!string.IsNullOrEmpty(materialNameContains) &&
                !mat.name.Contains(materialNameContains))
                continue;

            anyNamedMatch = true;
            if (sharedMaterial != null && mat != sharedMaterial)
            {
                sharedMaterials[i] = sharedMaterial;
                materialsChanged = true;
            }
        }

        if (!anyNamedMatch && sharedMaterials.Length > 0 && sharedMaterials[0] != null)
        {
            fallbackIndex = 0;
            if (sharedMaterial != null && sharedMaterials[0] != sharedMaterial)
            {
                sharedMaterials[0] = sharedMaterial;
                materialsChanged = true;
            }
        }

        if (materialsChanged)
            renderer.sharedMaterials = sharedMaterials;

        if (sharedColorBlock == null)
            sharedColorBlock = new MaterialPropertyBlock();

        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material mat = sharedMaterials[i];
            if (mat == null)
                continue;

            bool isMatch = string.IsNullOrEmpty(materialNameContains) ||
                           mat.name.Contains(materialNameContains) ||
                           i == fallbackIndex;

            if (!isMatch)
                continue;

            renderer.GetPropertyBlock(sharedColorBlock, i);
            SetColorProperties(sharedColorBlock, mat, color);

            renderer.SetPropertyBlock(sharedColorBlock, i);
            sharedColorBlock.Clear();
        }

    }

    private static void SetColorProperties(MaterialPropertyBlock propertyBlock, Material material, Color color)
    {
        for (int i = 0; i < ColorPropertyIds.Length; i++)
        {
            int propertyId = ColorPropertyIds[i];
            if (material == null || material.HasProperty(propertyId))
                propertyBlock.SetColor(propertyId, color);
        }
    }

    public static void ClearColorPropertyBlock(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (sharedColorBlock == null)
            sharedColorBlock = new MaterialPropertyBlock();

        sharedColorBlock.Clear();
        renderer.SetPropertyBlock(sharedColorBlock);
    }
    
}
