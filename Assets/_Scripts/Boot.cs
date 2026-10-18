using System;
using System.Collections;
using UnityEngine;

public class Boot : MonoBehaviour
{

    private void Awake()
    {
        AudioListener.volume = 0f;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);

        yield return new WaitUntil(() => GlobalManager.Instance != null && GlobalManager.Instance.savesLoaded);


        LoadingScene.LoadScene(SceneName.Menu);
    }

 
}
