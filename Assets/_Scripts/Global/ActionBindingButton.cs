using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ActionBindingButton : MonoBehaviour
{
    [SerializeField] private string actionName;
    [SerializeField] private Text ActionNameText;
    [Tooltip("Replace the scene label with the input action name. Leave disabled for custom labels such as movement directions.")]
    [SerializeField] private bool updateActionNameText;
    [SerializeField] private Text InputButtonText;
    [SerializeField] private CanvasGroup inputButtonCanvasGroup;
    [Min(0.05f)]
    [SerializeField] private float blinkFadeDuration = 0.4f;
    [Range(0.01f, 1f)]
    [SerializeField] private float blinkMinAlpha = 0.3f;
    [Tooltip("Index in the action's bindings, including composite headers. -1 selects the first keyboard/mouse binding. For WASD, choose each direction's index.")]
    [SerializeField] private int bindingIndex = -1;
    private GameSettingsManager manager;
    private InputAction action;
    private int resolvedBindingIndex = -1;
    private Coroutine blinkCoroutine;
    private float originalTextAlpha;
    private SelectedUI selectedUI;
    private bool selectedForRebinding;

    public void Initialize(GameSettingsManager settingsManager, InputActionAsset asset)
    {
        manager = settingsManager;
        action = asset && !string.IsNullOrWhiteSpace(actionName) ? asset.FindAction(actionName) : null;
        resolvedBindingIndex = bindingIndex;
        if (action != null && bindingIndex == -1)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (!binding.isComposite && (binding.path.StartsWith("<Keyboard>/") ||
                                             binding.path.StartsWith("<Mouse>/")))
                {
                    resolvedBindingIndex = i;
                    break;
                }
            }
        }
        if (!HasValidBinding)
            Debug.LogWarning($"Invalid binding for '{actionName}'. Check the action name and binding index.", this);
        RefreshDisplay();
    }

    private bool HasValidBinding => action != null && resolvedBindingIndex >= 0 &&
        resolvedBindingIndex < action.bindings.Count && !action.bindings[resolvedBindingIndex].isComposite;

    public void StartRebinding()
    {
        if (manager && HasValidBinding && isActiveAndEnabled)
            manager.StartRebinding(this, action, resolvedBindingIndex);
    }

    public void ResetBinding()
    {
        if (manager && HasValidBinding)
        {
            manager.CancelRebinding();
            action.RemoveBindingOverride(resolvedBindingIndex);
            manager.SaveInputBindings();
        }
    }

    public void RefreshDisplay()
    {
        StopBlinking();
        if (updateActionNameText && ActionNameText)
            ActionNameText.text = action != null ? action.name : actionName;
        if (InputButtonText)
            InputButtonText.text = HasValidBinding ? action.GetBindingDisplayString(resolvedBindingIndex) : "Unassigned";
    }

    public void ShowWaitingForInput()
    {
        StopBlinking();
        if (!selectedUI)
            TryGetComponent(out selectedUI);
        if (selectedUI)
        {
            selectedForRebinding = true;
            selectedUI.Select();
        }

        if (InputButtonText)
        {
            InputButtonText.text = "Press a key... (Esc to cancel)";
            if (!inputButtonCanvasGroup && !InputButtonText.TryGetComponent(out inputButtonCanvasGroup))
                inputButtonCanvasGroup = InputButtonText.gameObject.AddComponent<CanvasGroup>();

            originalTextAlpha = inputButtonCanvasGroup.alpha;
            blinkCoroutine = StartCoroutine(BlinkInputText());
        }
    }

    private IEnumerator BlinkInputText()
    {
        float elapsed = 0f;
        while (inputButtonCanvasGroup)
        {
            float duration = Mathf.Max(0.05f, blinkFadeDuration);
            float opacity = (Mathf.Cos(elapsed * Mathf.PI / duration) + 1f) * 0.5f;
            inputButtonCanvasGroup.alpha = originalTextAlpha * Mathf.Lerp(
                Mathf.Clamp(blinkMinAlpha, 0.01f, 1f), 1f, opacity);
            yield return null;
            elapsed = Mathf.Repeat(elapsed + Time.unscaledDeltaTime, duration * 2f);
        }
    }

    private void StopBlinking()
    {
        if (selectedForRebinding)
        {
            selectedForRebinding = false;
            if (selectedUI)
                selectedUI.Deselect();
        }

        if (blinkCoroutine == null)
            return;

        StopCoroutine(blinkCoroutine);
        blinkCoroutine = null;
        if (inputButtonCanvasGroup)
            inputButtonCanvasGroup.alpha = originalTextAlpha;
    }

    private void OnEnable() => RefreshDisplay();

    private void OnDisable()
    {
        StopBlinking();
        if (manager)
            manager.CancelRebinding(this);
    }
}
