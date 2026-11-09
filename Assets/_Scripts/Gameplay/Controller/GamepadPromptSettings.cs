using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GamepadPromptSettings", menuName = "Input/Gamepad Prompt Settings")]
public sealed class GamepadPromptSettings : ScriptableObject
{
    [Serializable]
    public sealed class Prompt
    {
        [Tooltip("Input System control name, for example buttonSouth or dpad/up.")]
        public string control;
        public Sprite sprite;
    }

    public Prompt[] prompts = Array.Empty<Prompt>();
}
