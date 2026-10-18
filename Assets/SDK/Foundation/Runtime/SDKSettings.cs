using UnityEngine;

public sealed class SDKSettings : ScriptableObject
{
    public const string ResourceName = "SDKSettings";

    [SerializeField] private SDKConfig config;

    public SDKConfig Config
    {
        get => config;
        set => config = value;
    }
}
