using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class UIParticleSystem : MonoBehaviour
{
    public enum EmitShape { Point, ScreenEdge, Rect }

    [System.Flags]
    public enum ScreenEdgeFlags
    {
        Top = 1 << 0,
        Right = 1 << 1,
        Bottom = 1 << 2,
        Left = 1 << 3,
        All = Top | Right | Bottom | Left
    }
    
    public enum PointDirection { All, Top, Bottom, Left, Right }

    [Header("Main Settings")]
    public float duration = 5f;
    public bool playOnAwake = true;
    public bool loop = true;
    public int maxParticles = 100;
    public float emissionRate = 20f;
    public List<Sprite> sprites;

    public List<UIParticleSystem> subEmitters;

    [Header("Shape")]
    public EmitShape shape = EmitShape.Point;
    public PointDirection pointDirection = PointDirection.All;
    public Vector2 pointPosition;
    public RectTransform overrideRect;
    public ScreenEdgeFlags emitEdges = ScreenEdgeFlags.All;
    public bool directInwardFromEdges = true;

    [Header("Lifetime & Motion")]
    public Vector2 lifetime = new Vector2(1f, 2f); // Min/Max
    public Vector2 startSpeed = new Vector2(100f, 300f);
    public Vector2 gravity = new Vector2(0, -100f);

    [Header("Size & Rotation")]
    public Vector2 startSize = new Vector2(30f, 50f);
    public AnimationCurve sizeOverLifetime = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    public Vector2 startRotation = new Vector2(0f, 360f);
    public Vector2 rotationSpeed = new Vector2(-90f, 90f);

    [Header("Color")]
    public Gradient colorOverLifetime;

    private class Particle
    {
        public GameObject go;
        public RectTransform rect;
        public Image image;

        public float life;
        public float maxLife;
        public Vector2 velocity;
        public float baseSize;
        public float rotSpeed;
        public float rotation;
        public Vector2 pos;
    }

    private Color[] colorCache = new Color[256];
    private float[] sizeCache = new float[256];
    private bool cachesBuilt = false;

    private void BuildCaches()
    {
        if (cachesBuilt) return;
        for (int i = 0; i < 256; i++)
        {
            float t = i / 255f;
            sizeCache[i] = sizeOverLifetime.Evaluate(t);
            if (colorOverLifetime != null) colorCache[i] = colorOverLifetime.Evaluate(t);
        }
        cachesBuilt = true;
    }

    private List<Particle> pool = new List<Particle>();
    private List<Particle> activeParticles = new List<Particle>();
    private float emitTimer;
    private float playTime;
    private bool isPlaying;
    private RectTransform myRect;
    
    private static readonly int[] validEdgesBuffer = new int[4];


    public void Play()
    {
        if (myRect == null) myRect = GetComponent<RectTransform>();
        cachesBuilt = false; // Reset to allow live edit apply
        BuildCaches();
        isPlaying = true;
        playTime = 0f;

        if (subEmitters != null)
        {
            foreach (var subEmitter in subEmitters)
            {
                subEmitter.Play();
            }
        }
    }

    public void Pause()
    {
        isPlaying = false;
        if (subEmitters != null)
        {
            foreach (var subEmitter in subEmitters)
            {
                subEmitter.Pause();
            }
        }
    }

    public void Stop()
    {
        isPlaying = false;
        Clear();

        if (subEmitters != null)
        {
            foreach (var subEmitter in subEmitters)
            {
                subEmitter.Stop();
            }
        }
    }

    public void Clear()
    {
        foreach (var p in activeParticles)
        {
            if (p.go != null) 
            {
                if (Application.isPlaying) Destroy(p.go);
                else DestroyImmediate(p.go);
            }
        }
        activeParticles.Clear();

        foreach (var p in pool)
        {
            if (p.go != null) 
            {
                if (Application.isPlaying) Destroy(p.go);
                else DestroyImmediate(p.go);
            }
        }
        pool.Clear();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            DoUpdate(Time.deltaTime);
        }
    }

    private void DoUpdate(float dt)
    {
        if (isPlaying)
        {
            if (!loop)
            {
                playTime += dt;
                if (playTime >= duration)
                {
                    isPlaying = false;
                }
            }

            if (isPlaying)
            {
                emitTimer += dt;
                float emitInterval = 1f / emissionRate;
                
                while (emitTimer >= emitInterval && activeParticles.Count < maxParticles)
                {
                    emitTimer -= emitInterval;
                    EmitLocal();
                }
            }
        }

        UpdateParticles(dt);
    }


    private double lastTime;
    
    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
        }
#endif

        if (myRect == null) myRect = GetComponent<RectTransform>();
        if (Application.isPlaying && playOnAwake) Play();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.update -= EditorUpdate;
        }
#endif
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (Application.isPlaying) return;

        if (isPlaying || activeParticles.Count > 0)
        {
            float dt = (float)(EditorApplication.timeSinceStartup - lastTime);
            lastTime = EditorApplication.timeSinceStartup;

            DoUpdate(dt);

            EditorApplication.QueuePlayerLoopUpdate();
        }
        else
        {
            lastTime = EditorApplication.timeSinceStartup;
        }
    }
#endif

    private void EmitLocal()
    {
        BuildCaches();
        Particle p = GetFromPool();
        
        if (sprites != null && sprites.Count > 0)
            p.image.sprite = sprites[Random.Range(0, sprites.Count)];

        p.maxLife = Random.Range(lifetime.x, lifetime.y);
        p.life = p.maxLife;
        p.baseSize = Random.Range(startSize.x, startSize.y);
        
        p.rotation = Random.Range(startRotation.x, startRotation.y);
        p.rect.localEulerAngles = new Vector3(0, 0, p.rotation);
        p.rotSpeed = Random.Range(rotationSpeed.x, rotationSpeed.y);

        p.pos = GetEmitPosition();
        p.rect.anchoredPosition = p.pos;
        p.velocity = GetInitialVelocity(p.pos);

        p.go.SetActive(true);
        activeParticles.Add(p);
        
        UpdateSingleParticleState(p);
    }

    private Vector2 GetEmitPosition()
    {
        switch (shape)
        {
            case EmitShape.Point:
                return pointPosition;
            case EmitShape.Rect:
                Rect r = overrideRect != null ? overrideRect.rect : myRect.rect;
                return new Vector2(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin, r.yMax));
            case EmitShape.ScreenEdge:
                Rect br = myRect.rect;
                
                int validCount = 0;
                if ((emitEdges & ScreenEdgeFlags.Top) != 0) validEdgesBuffer[validCount++] = 0;
                if ((emitEdges & ScreenEdgeFlags.Right) != 0) validEdgesBuffer[validCount++] = 1;
                if ((emitEdges & ScreenEdgeFlags.Bottom) != 0) validEdgesBuffer[validCount++] = 2;
                if ((emitEdges & ScreenEdgeFlags.Left) != 0) validEdgesBuffer[validCount++] = 3;

                if (validCount == 0) return pointPosition;

                int edge = validEdgesBuffer[Random.Range(0, validCount)];
                if (edge == 0) return new Vector2(Random.Range(br.xMin, br.xMax), br.yMax);
                if (edge == 1) return new Vector2(br.xMax, Random.Range(br.yMin, br.yMax));
                if (edge == 2) return new Vector2(Random.Range(br.xMin, br.xMax), br.yMin);
                return new Vector2(br.xMin, Random.Range(br.yMin, br.yMax));
        }
        return pointPosition;
    }

    private Vector2 GetInitialVelocity(Vector2 pos)
    {
        float speed = Random.Range(startSpeed.x, startSpeed.y);
        
        if (shape == EmitShape.Point && pointDirection != PointDirection.All)
        {
            float angle = 0f;
            switch (pointDirection)
            {
                case PointDirection.Top: angle = Random.Range(45f, 135f); break;
                case PointDirection.Bottom: angle = Random.Range(225f, 315f); break;
                case PointDirection.Right: angle = Random.Range(-45f, 45f); break;
                case PointDirection.Left: angle = Random.Range(135f, 225f); break;
            }
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            return dir * speed;
        }

        if (shape == EmitShape.ScreenEdge && directInwardFromEdges)
        {
            Vector2 dir = (Vector2.zero - pos).normalized;
            float angleOffset = Random.Range(-30f, 30f);
            float currentAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float finalAngle = currentAngle + angleOffset;
            
            return new Vector2(Mathf.Cos(finalAngle * Mathf.Deg2Rad), Mathf.Sin(finalAngle * Mathf.Deg2Rad)) * speed;
        }

        return Random.insideUnitCircle.normalized * speed;
    }

    private void UpdateParticles(float dt)
    {
        for (int i = activeParticles.Count - 1; i >= 0; i--)
        {
            Particle p = activeParticles[i];
            p.life -= dt;

            if (p.life <= 0)
            {
                p.go.SetActive(false);
                pool.Add(p);
                
                int lastIdx = activeParticles.Count - 1;
                activeParticles[i] = activeParticles[lastIdx];
                activeParticles.RemoveAt(lastIdx);
                continue;
            }

            p.velocity += gravity * dt;
            p.pos += p.velocity * dt;
            p.rotation += p.rotSpeed * dt;
            
            p.rect.anchoredPosition = p.pos;
            p.rect.localEulerAngles = new Vector3(0, 0, p.rotation);

            UpdateSingleParticleState(p);
        }
    }

    private void UpdateSingleParticleState(Particle p)
    {
        float t = 1f - (p.life / p.maxLife);
        int cacheIdx = (int)(t * 255f);
        if (cacheIdx < 0) cacheIdx = 0;
        else if (cacheIdx > 255) cacheIdx = 255;
        
        float currSize = p.baseSize * sizeCache[cacheIdx];
        p.rect.sizeDelta = new Vector2(currSize, currSize);

        if (colorOverLifetime != null)
            p.image.color = colorCache[cacheIdx];
    }

    private Particle GetFromPool()
    {
        if (pool.Count > 0)
        {
            Particle p = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            return p;
        }

        GameObject go = new GameObject("UIParticle");
        go.transform.SetParent(transform, false);

        Particle newP = new Particle
        {
            go = go,
            rect = go.AddComponent<RectTransform>(),
            image = go.AddComponent<Image>()
        };
        newP.image.raycastTarget = false;
        return newP;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(UIParticleSystem))]
public class UIParticleSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Отрисовка стандартных полей
        DrawDefaultInspector();

        UIParticleSystem ps = (UIParticleSystem)target;

        GUILayout.Space(10);
        
        // Кнопки управления
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Play", GUILayout.Height(30))) ps.Play();
        if (GUILayout.Button("Pause", GUILayout.Height(30))) ps.Pause();
        if (GUILayout.Button("Stop / Clear", GUILayout.Height(30))) ps.Stop();
        GUILayout.EndHorizontal();
        
        GUILayout.Space(5);
    }
}
#endif
