using System;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [Tooltip("Bypass the introductory cutscene and start normal gameplay immediately.")]
    public bool skipTutorial = false;
    [Tooltip("Skip the arrival intro cutscene and start gameplay immediately.")]
    public bool skipIntroCutscene = false;
    [SerializeField] private GameIntroduction introduction;
    public bool IsIntroPlaying => introduction != null && introduction.IsPlaying;

    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        if (Instance != this || skipTutorial || skipIntroCutscene || introduction == null) return;
        introduction.Play();
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
