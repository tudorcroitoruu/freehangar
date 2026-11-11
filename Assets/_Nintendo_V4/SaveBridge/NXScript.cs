using UnityEngine;

public class NXScript : MonoBehaviour
{
#if UNITY_SWITCH && !UNITY_EDITOR
    private static bool initialized;
    private static nn.account.Uid userId;
    private static nn.account.UserHandle userHandle;

    public static void Initialize()
    {
        if (initialized) return;
        nn.account.Account.Initialize();
        if (!nn.account.Account.TryOpenPreselectedUser(ref userHandle))
            throw new System.InvalidOperationException("No preselected Nintendo user is available.");
        var result = nn.account.Account.GetUserId(ref userId, userHandle);
        if (!result.IsSuccess()) throw new System.InvalidOperationException("Could not obtain Nintendo user: " + result);
        initialized = true;
    }

    public static nn.account.Uid GetUserID()
    {
        Initialize();
        return userId;
    }

    public static bool TryGetUserHandle(out nn.account.UserHandle handle)
    {
        Initialize();
        handle = userHandle;
        return initialized;
    }
#endif
}
