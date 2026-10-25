using UnityEngine;
using UnityEngine.Rendering;

namespace Liviu.Environment.HDRP
{
    [DisallowMultipleComponent]
    public sealed class PolyverseStyleSkyLightBridge : MonoBehaviour
    {
        [SerializeField]
        private Volume skyVolume;

        [SerializeField]
        private Transform sun;

        [SerializeField]
        private Transform moon;

        [SerializeField]
        private bool updateInEditMode;

        private PolyverseStyleSky runtimeSky;

        private void OnEnable()
        {
            CacheSky();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying && !updateInEditMode)
                return;

            if (runtimeSky == null && !CacheSky())
                return;

            if (sun != null)
            {
                runtimeSky.sunDirection.overrideState = true;
                runtimeSky.sunDirection.value = -sun.forward;
            }

            if (moon != null)
            {
                runtimeSky.moonDirection.overrideState = true;
                runtimeSky.moonDirection.value = -moon.forward;
            }
        }

        private bool CacheSky()
        {
            runtimeSky = null;

            if (skyVolume == null)
                return false;

            VolumeProfile profile = Application.isPlaying
                ? skyVolume.profile
                : skyVolume.sharedProfile;

            return profile != null && profile.TryGet(out runtimeSky);
        }
    }
}
