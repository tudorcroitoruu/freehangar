using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Boot : MonoBehaviour
{

    private void Awake()
    {
        // Saved volume is applied through the mixer; never leave the whole session muted.
        AudioListener.volume = 1f;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1f);

        while (GlobalManager.Instance == null || !GlobalManager.Instance.savesLoaded)
        {
            var manager = GlobalManager.Instance;
            if (manager != null && manager.StartupError != null && !manager.IsLoadingSave &&
                (MenuPadInput.ConfirmPressed(FarmInput.ActivePad ?? Gamepad.current) ||
                 Keyboard.current?.enterKey.wasPressedThisFrame == true))
                _ = manager.LoadStartupData();
            yield return null;
        }


        LoadingScene.LoadScene(SceneName.Menu);
    }

    private void OnGUI()
    {
        var manager = GlobalManager.Instance;
        if (manager == null || manager.StartupError == null || manager.IsLoadingSave) return;
        GUI.depth = -10000;
        var panel = new Rect(Screen.width * .15f, Screen.height * .35f, Screen.width * .7f, Screen.height * .3f);
        GUI.Box(panel, GUIContent.none);
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true,
            fontSize = Mathf.Max(18, Mathf.RoundToInt(Screen.height / 30f)) };
        GUI.Label(panel, "Unable to load saved data.\nYour saved progress has not been reset.\nPress A / Enter to retry.", style);
    }

 
}
