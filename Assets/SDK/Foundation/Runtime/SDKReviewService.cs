using UnityEngine;

internal sealed class SDKReviewService
{
    public void RequestNow()
    {
#if UNITY_IOS && !UNITY_EDITOR
        UnityEngine.iOS.Device.RequestStoreReview();
#else
        Debug.Log("[SDK] Rate Us OS popup requested.");
#endif
    }
}
