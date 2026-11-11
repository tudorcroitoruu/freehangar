#if UNITY_SWITCH
using nn.account;
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class NintendoNetworkConnection
{
    //System.Action onSuccess, System.Action onFail
    // StartCoroutine(NintendoNetworkConnection.CheckNetwork());

    //Exemplu de apelare a metodei CheckNetwork
    // StartCoroutine(NintendoNetworkConnection.CheckNetwork(() =>
    // {
    //      Debug.LogError("-> TEST SUCCESS <-");
    // }, () =>
    // {
    //      Debug.LogError("-> TEST FAIL <-");
    // }));

    public static bool memberShip = true;
    public static bool isMemberShipCheck = true;

    public static string userName = "";
    public static string UserID = "";

    public static IEnumerator CheckNetwork(System.Action onSuccess, System.Action onFail)
    {
#if !UNITY_SWITCH || UNITY_EDITOR
        // Testarea retelei in editor
        yield return new WaitForSecondsRealtime(0.1f);
        // Test bit for PC
        // if (false)
        if (true)
            onSuccess?.Invoke();
        else
            onFail?.Invoke();
        Debug.LogError("-> TEST ENTER <-");
#endif

#if UNITY_SWITCH && !UNITY_EDITOR
        //UnityEngine.Switch.NetworkInterfaceWrapper.SetNetworkConnectingOnBackground(false);
        Debug.LogError("!!! SUCCESS ENTER !");
        bool NetworkCheckBusy = true;
        bool NetworkCheckLastResult = true;
        
        Uid localUserID = new Uid();
        Nickname userNick = new Nickname();

        if (!NXScript.TryGetUserHandle(out UserHandle userHandle))
        {
            Debug.LogError("NXScript.userHandle not ready - Initialize() wasn't called or hasn't completed yet.");
            onFail?.Invoke();
            yield break;
        }

        Account.GetUserId(ref localUserID, userHandle);
        Account.GetNickname(ref userNick, localUserID);
        
        userName = userNick.name;
        UserID = localUserID.ToString();
        
        Debug.LogError($"<color=yellow>Players Nickname: {userName}</color>");
        Debug.LogError($"<color=orange>Players UserID: {UserID}</color>");
        
        var nsaResult = NetworkServiceAccount.EnsureAvailable(userHandle);
        if (nsaResult.IsSuccess())
        {
            if (NetworkServiceAccount.ResultNetworkServiceAccountUnavailable.Includes(nsaResult))
            {
                Debug.LogError("Failed ResultNetworkServiceAccountUnavailable");
                NetworkCheckLastResult = false;
            }
            else if (NetworkServiceAccount.ResultNetworkCommunicationError.Includes(nsaResult))
            {
                Debug.LogError("Failed ResultNetworkCommunicationError");
                NetworkCheckLastResult = false;
            }
        }
        else
        {
            NetworkCheckLastResult = false;
        }

        if (!NetworkCheckLastResult)
        {
            Debug.LogWarning("EnsureAvailable failed");
            NetworkCheckLastResult = false;
            NetworkCheckBusy = false;
            memberShip = false;
            Debug.LogError("! NOT ALLOWED !!! (1)");
            Debug.LogError($"! NOT ALLOWED -> {memberShip}");
            onFail?.Invoke();
            //NetworkingModule.SP.SetAuthStatus(NetworkingModule.AuthStatusEnum.AuthFailedNotAllowed);
            yield break;
        }
        else
            memberShip = true;

        //Ensure that we do start a new NetworkRequest session: So that we get to see a flightmode popup if this was turned on AFTER one succesfull connection
        Debug.Log("next: GetNetworkReferenceCount");
        int count = UnityEngine.Switch.NetworkInterfaceWrapper.GetNetworkReferenceCount();
        while (count > 0)
        {
            Debug.Log("LEAVE NETWORK COUNT ");
            count -= 1;
            Debug.Log("next: LeaveNetworkConnecting");
            UnityEngine.Switch.NetworkInterfaceWrapper.LeaveNetworkConnecting();
        }

        Debug.Log("next: EnterNetworkConnecting");
        if (!UnityEngine.Switch.NetworkInterfaceWrapper.EnterNetworkConnecting())
        {
            Debug.LogWarning("EnterNetworkConnecting failed..");
            NetworkCheckLastResult = false;
            NetworkCheckBusy = false;
            Debug.LogError("! NOT ALLOWED !!! (2)");
            onFail?.Invoke();
            //NetworkingModule.SP.SetAuthStatus(NetworkingModule.AuthStatusEnum.AuthFailedNotAllowed);
            yield break;
        }

        yield return new WaitForEndOfFrame();
        Debug.Log("next: WaitForNetworkConnecting");

        while (UnityEngine.Switch.NetworkInterfaceWrapper.IsNetworkConnecting())
        {
            Debug.Log("next: next : " + UnityEngine.Switch.NetworkInterfaceWrapper.IsNetworkConnecting());
            yield return null;
        }

        //UnityEngine.Switch.NetworkInterfaceWrapper.IsNetworkConnecting();
        yield return new WaitForEndOfFrame();
        Debug.Log("next: IsNetworkAccepted");
        if (!UnityEngine.Switch.NetworkInterfaceWrapper.IsNetworkAvailable())
        {
            Debug.LogWarning("Network not accepted..will stop...");
            NetworkCheckLastResult = false;
            NetworkCheckBusy = false;
            Debug.LogError("! NOT ALLOWED !!! (3)");
            isMemberShipCheck = true;
            onFail?.Invoke();
            //NetworkingModule.SP.SetAuthStatus(NetworkingModule.AuthStatusEnum.AuthFailedNotAllowed);
            yield break;
        }

        Debug.Log("Did Unity-Switch network openings...");
        Debug.Log("EnsurIdTokenCacheAsync");
        AsyncContext asyncContext = new AsyncContext();
        nsaResult = NetworkServiceAccount.EnsureIdTokenCacheAsync(asyncContext, userHandle);
        if (nsaResult.IsSuccess())
        {
            Debug.Log("After  EnsurIdTokenCacheAsync");
            if (NetworkServiceAccount.ResultTokenCacheUnavailable.Includes(nsaResult))
            {
                nsaResult = NetworkServiceAccount.EnsureAvailable(userHandle);
                if (nsaResult.IsSuccess())
                {
                    if (NetworkServiceAccount.ResultNetworkServiceAccountUnavailable.Includes(nsaResult))
                    {
                        Debug.LogError("Failed ResultNetworkServiceAccountUnavailable 2");
                        NetworkCheckLastResult = false;
                    }
                    else if (NetworkServiceAccount.ResultNetworkCommunicationError.Includes(nsaResult))
                    {
                        Debug.LogError("Failed ResultNetworkCommunicationError 2");
                        NetworkCheckLastResult = false;
                    }
                }
                else
                {
                    Debug.LogError("Failed other 2");
                    NetworkCheckLastResult = false;
                }
            }

            nn.Result result;
            /*float timeout = Time.realtimeSinceStartup + 10;
            while (Time.realtimeSinceStartup < timeout)
            {
                bool a = false;
                result = asyncContext.HasDone(ref a);
                if (!result.IsSuccess())
                    Debug.LogError("HasDone not success, we do not handle this failure.");
                if (a) break;
                yield return new WaitForSeconds(0.05f);
            }

            bool b = false;
            result = asyncContext.HasDone(ref b);
            if (!result.IsSuccess())
                Debug.LogError("HasDone not success, we do not handle this failure.");


            var res = asyncContext.GetResult();*/

            bool b = false;
            result = asyncContext.HasDone(ref b);

            while (!b)
            {
                yield return new WaitForSeconds(0.5f);
                result = asyncContext.HasDone(ref b);
            }
            var res = asyncContext.GetResult();


            if (!res.IsSuccess())
            {
                //this seems to be valid even for accounts without online subscription
                if (NetworkServiceAccount.ResultNetworkServiceAccountUnavailable.Includes(res))
                {
                    isMemberShipCheck = true;
                    //check for online subscription
                    var checkOnlineSubResult = NetworkServiceAccount.EnsureAvailable(userHandle);
                    if (checkOnlineSubResult.IsSuccess() && !Account.ResultCancelledByUser.Includes(checkOnlineSubResult))
                    {
                        isMemberShipCheck = true;
                        Debug.Log("All still fine...");
                        //continue...
                    }
                    else //if we cancelled in the subscription window
                    {
                        Debug.Log("user cancelled...");
                        //OnAuthFinished(ConnectResult.FAILURE_CANCEL);
                        NetworkCheckLastResult = false;
                        NetworkCheckBusy = false;
                        Debug.LogError("! NOT ALLOWED !!! (4)");
                        isMemberShipCheck = false;
                        onFail?.Invoke();
                        //NetworkingModule.SP.SetAuthStatus(NetworkingModule.AuthStatusEnum.AuthFailedNotAllowed);
                        yield break;
                    }
                }
                else //this might never happen ever?
                {
                    //NOTE: IF WE GET HERE IN RUNTIME, THIS IS A QA-REJECTION
                    Debug.LogError("Ehh..net error or something?");
                    nn.err.Error.Show(res, true);
                    //OnAuthFinished(ConnectResult.FAILURE_TRY_AGAIN);
                    NetworkCheckLastResult = false;
                    NetworkCheckBusy = false;
                    Debug.LogError("! NOT ALLOWED !!! (5)");
                    isMemberShipCheck = false;
                    onFail?.Invoke();
                    //NetworkingModule.SP.SetAuthStatus(NetworkingModule.AuthStatusEnum.AuthFailedNotAllowed);
                    yield break;
                }
            }

            Debug.LogError("!!! SUCCESS EXIT !");
            onSuccess?.Invoke();
            yield break;
        }

        Debug.LogError("! SOMETHING WENT WRONG !!!");
        onFail?.Invoke();
        yield break;
#endif
    }
}