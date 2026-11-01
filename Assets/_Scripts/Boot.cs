using System;
using System.Collections;
using UnityEngine;

public class Boot : MonoBehaviour
{

    private void Awake()
    {
        // Saved volume is applied through the mixer; never leave the whole session muted.
        AudioListener.volume = 1f;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);

        yield return new WaitUntil(() => GlobalManager.Instance != null && GlobalManager.Instance.savesLoaded);


        LoadingScene.LoadScene(SceneName.Menu);
    }

 
}
