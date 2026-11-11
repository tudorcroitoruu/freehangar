using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class GamepadDefaultFocusValidation
{
    public static void Run()
    {
        var root = new GameObject("Focus validation");
        var eventRoot = new GameObject("Focus validation events");
        var previous = EventSystem.current;
        var events = eventRoot.AddComponent<EventSystem>();
        try
        {
            Button Make(string name)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Button));
                go.transform.SetParent(root.transform);
                return go.GetComponent<Button>();
            }
            var hidden = Make("hidden"); hidden.gameObject.SetActive(false);
            var disabled = Make("disabled"); disabled.interactable = false;
            var first = Make("first"); var second = Make("second");
            Assert.IsTrue(PadMenuCursor.SelectFirstControl(events, root.transform));
            Assert.AreSame(first.gameObject, events.currentSelectedGameObject);
            events.SetSelectedGameObject(second.gameObject);
            PadMenuCursor.SelectFirstControl(events, root.transform);
            Assert.AreSame(first.gameObject, events.currentSelectedGameObject, "Reopened page starts at first control.");
            first.gameObject.AddComponent<CanvasGroup>().interactable = false;
            PadMenuCursor.SelectFirstControl(events, root.transform);
            Assert.AreSame(second.gameObject, events.currentSelectedGameObject, "CanvasGroup-disabled controls are skipped.");
            second.gameObject.SetActive(false);
            Assert.IsFalse(PadMenuCursor.SelectFirstControl(events, root.transform));
            Assert.IsNull(events.currentSelectedGameObject);
            Directory.CreateDirectory("output/MenuSwitch");
            File.WriteAllText("output/MenuSwitch/default-focus.txt", "PASS first usable control, reopening resets focus, disabled/inactive/CanvasGroup controls skipped, empty page clears selection.");
            Debug.Log("PASS gamepad default focus checks.");
        }
        finally
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(eventRoot);
            if (previous) EventSystem.current = previous;
        }
    }
}
