using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(RectTransform))]
public class UIRewardFly : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform spawnPoint;
    [SerializeField] private RectTransform target;

    [Header("Visual")]
    [SerializeField] private Sprite rewardSprite;
    [SerializeField] private Vector2 itemSize = new Vector2(50f, 50f);

    [Header("Spawn")]
    [SerializeField] private int defaultAmount = 8;
    [SerializeField] private float delayBetweenItems = 0.04f;

    [Header("Burst")]
    [SerializeField] private float burstDuration = 0.15f;
    [SerializeField] private Vector2 horizontalSpread = new Vector2(-2f, 2f);
    [SerializeField] private Vector2 verticalSpread = new Vector2(5f, 0f);

    [Header("Fly")]
    [SerializeField] private float flyDuration = 0.5f;

    [Header("Animation")]
    [SerializeField] private AnimationCurve burstCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField] private AnimationCurve flyCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Scale during the initial spawn/burst phase. Example: 0 -> 1.")]
    [SerializeField] private AnimationCurve spawnScaleCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Scale during the fly-to-target phase. Example: 1 -> 0.5.")]
    [SerializeField] private AnimationCurve flyScaleCurve =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 0.5f);

    [Header("Editor Simulation")]
    [SerializeField] private int simulationAmount = 8;

    private readonly Queue<RectTransform> pool =
        new Queue<RectTransform>();


    // =========================================================
    // Public API
    // =========================================================

    public void Play()
    {
        Play(defaultAmount);
    }

    public void Play(int amount)
    {
        if (!CanPlay(amount, spawnPoint != null))
            return;

        StartCoroutine(SpawnRoutine(amount, spawnPoint.position));
    }

    public void PlayFrom(RectTransform spawnOverride)
    {
        PlayFrom(spawnOverride, defaultAmount);
    }

    public void PlayFrom(RectTransform spawnOverride, int amount)
    {
        if (spawnOverride == null)
        {
            Play(amount);
            return;
        }

        if (!CanPlay(amount, true))
            return;

        StartCoroutine(SpawnRoutine(amount, spawnOverride.position));
    }

    public float GetFirstItemNearTargetDelay(float flyProgress = 0.8f)
    {
        flyProgress = Mathf.Clamp01(flyProgress);
        return Mathf.Max(0f, burstDuration) + Mathf.Max(0f, flyDuration) * flyProgress;
    }


    // =========================================================
    // Spawn
    // =========================================================

    private bool CanPlay(int amount, bool hasSpawnPosition)
    {
        if (!hasSpawnPosition)
        {
            Debug.LogWarning(
                "UIRewardFly: Spawn Point is not assigned.",
                this
            );

            return false;
        }

        if (target == null)
        {
            Debug.LogWarning(
                "UIRewardFly: Target is not assigned.",
                this
            );

            return false;
        }

        if (rewardSprite == null)
        {
            Debug.LogWarning(
                "UIRewardFly: Reward Sprite is not assigned.",
                this
            );

            return false;
        }

        return amount > 0;
    }

    private IEnumerator SpawnRoutine(int amount, Vector3 spawnPosition)
    {
        for (int i = 0; i < amount; i++)
        {
            RectTransform item = GetItem();

            item.gameObject.SetActive(true);

            item.position = spawnPosition;

            float initialScale =
                spawnScaleCurve.Evaluate(0f);

            item.localScale =
                Vector3.one * initialScale;

            StartCoroutine(
                AnimateItem(item)
            );

            if (delayBetweenItems > 0f)
            {
                yield return new WaitForSeconds(
                    delayBetweenItems
                );
            }
        }
    }


    // =========================================================
    // Animation
    // =========================================================

    private IEnumerator AnimateItem(
        RectTransform item)
    {
        Vector3 startPosition =
            item.position;

        Vector3 burstOffset =
            new Vector3(
                Random.Range(
                    horizontalSpread.x,
                    horizontalSpread.y
                ),
                Random.Range(
                    verticalSpread.x,
                    verticalSpread.y
                ),
                0f
            );

        Vector3 burstPosition =
            startPosition + burstOffset;


        // =====================================================
        // Spawn / Burst Phase
        // =====================================================

        if (burstDuration > 0f)
        {
            float timer = 0f;

            while (timer < burstDuration)
            {
                timer += Time.deltaTime;

                float t = Mathf.Clamp01(
                    timer / burstDuration
                );

                float positionT =
                    burstCurve.Evaluate(t);

                item.position =
                    Vector3.Lerp(
                        startPosition,
                        burstPosition,
                        positionT
                    );

                float scale =
                    spawnScaleCurve.Evaluate(t);

                item.localScale =
                    Vector3.one * scale;

                yield return null;
            }
        }

        item.position = burstPosition;

        item.localScale =
            Vector3.one *
            spawnScaleCurve.Evaluate(1f);


        // =====================================================
        // Fly To Target Phase
        // =====================================================

        Vector3 flyStartPosition =
            item.position;

        if (flyDuration > 0f)
        {
            float timer = 0f;

            while (timer < flyDuration)
            {
                timer += Time.deltaTime;

                float t = Mathf.Clamp01(
                    timer / flyDuration
                );

                float positionT =
                    flyCurve.Evaluate(t);

                // Target position is read every frame,
                // so moving UI targets still work.
                item.position =
                    Vector3.Lerp(
                        flyStartPosition,
                        target.position,
                        positionT
                    );

                float scale =
                    flyScaleCurve.Evaluate(t);

                item.localScale =
                    Vector3.one * scale;

                yield return null;
            }
        }


        item.position =
            target.position;

        item.localScale =
            Vector3.one *
            flyScaleCurve.Evaluate(1f);

        ReleaseItem(item);
    }


    // =========================================================
    // Pool
    // =========================================================

    private RectTransform GetItem()
    {
        RectTransform item;

        if (pool.Count > 0)
        {
            item = pool.Dequeue();
        }
        else
        {
            GameObject go =
                new GameObject(
                    "UIRewardItem",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );

            item =
                go.GetComponent<RectTransform>();

            // UIRewardFly itself acts as the container.
            item.SetParent(
                transform,
                false
            );

            Image image =
                go.GetComponent<Image>();

            image.preserveAspect =
                true;

            image.raycastTarget =
                false;

            go.SetActive(false);
        }

        UpdateVisual(item);

        return item;
    }


    private void UpdateVisual(
        RectTransform item)
    {
        item.sizeDelta =
            itemSize;

        Image image =
            item.GetComponent<Image>();

        if (image != null)
        {
            image.sprite =
                rewardSprite;
        }
    }


    private void ReleaseItem(
        RectTransform item)
    {
        item.gameObject.SetActive(false);

        item.localScale =
            Vector3.one;

        pool.Enqueue(item);
    }


    // =========================================================
    // Clear
    // =========================================================

    public void Clear()
    {
        StopAllCoroutines();

        pool.Clear();

        for (
            int i = transform.childCount - 1;
            i >= 0;
            i--
        )
        {
            GameObject child =
                transform
                    .GetChild(i)
                    .gameObject;

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }


    // =========================================================
    // Simulation
    // =========================================================

    public void Simulate()
    {
        Play(simulationAmount);
    }

    public void Simulate(int amount)
    {
        Play(amount);
    }
}


#if UNITY_EDITOR

[CustomEditor(typeof(UIRewardFly))]
public class UIRewardFlyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        UIRewardFly rewardFly =
            (UIRewardFly)target;

        GUILayout.Space(12);

        EditorGUILayout.LabelField(
            "Simulation",
            EditorStyles.boldLabel
        );

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(
            "x1",
            GUILayout.Height(30)))
        {
            rewardFly.Simulate(1);
        }

        if (GUILayout.Button(
            "x5",
            GUILayout.Height(30)))
        {
            rewardFly.Simulate(5);
        }

        if (GUILayout.Button(
            "x10",
            GUILayout.Height(30)))
        {
            rewardFly.Simulate(10);
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        if (GUILayout.Button(
            "Simulate Amount",
            GUILayout.Height(30)))
        {
            rewardFly.Simulate();
        }

        GUILayout.Space(4);

        if (GUILayout.Button(
            "Clear",
            GUILayout.Height(25)))
        {
            rewardFly.Clear();
        }
    }
}

#endif
