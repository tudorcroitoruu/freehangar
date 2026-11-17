using UnityEngine;

// Scene-owned targets keep the demo button callbacks visible in the Inspector.
public sealed class DemoPopupButtons : MonoBehaviour
{
    public TMPro.TMP_FontAsset timerFont;
    public void Yes() => DemoSession.Instance?.Buy();
    public void No() => DemoSession.Instance?.Decline();
}
