using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace UIPopups
{
    public abstract class BasePopupUI : MonoBehaviour
    {
        private static readonly HashSet<BasePopupUI> activePopups = new HashSet<BasePopupUI>();

        public static event Action<bool> AnyPopupVisibilityChanged;
        public static bool HasActivePopup => activePopups.Count > 0;
        private static int queueBypassDepth;

        public static bool HasActivePopupExcept(Predicate<BasePopupUI> ignorePredicate)
        {
            foreach (BasePopupUI popup in activePopups)
            {
                if (popup == null)
                    continue;
                if (ignorePredicate != null && ignorePredicate(popup))
                    continue;

                return true;
            }

            return false;
        }

        public static void ShowFromPopupQueue(BasePopupUI popup)
        {
            if (popup == null)
                return;

            queueBypassDepth++;
            try
            {
                popup.Show(true);
            }
            finally
            {
                queueBypassDepth--;
            }
        }

        public static bool CanShowPopup(BasePopupUI popup)
        {
            if (popup == null)
                return false;
            return true;
        }

        public bool useHapticFeedback = true;
        public bool IsShowing { get; protected set; }
        public Action OnShowed, OnHided;

        [SerializeField] private bool initialHided = true;
        [SerializeField] private bool disableOnHide = true;
        
        [Header("ANIMATION SETTINGS:")]
        [SerializeField] protected Ease easeType = Ease.InOutQuad;
        [SerializeField, Min(0)] protected float animationTime = 0.15f;
        [SerializeField] protected bool useScaledTime = true;

        // A popup must still be able to close while it is responsible for pausing
        // gameplay. Scaled delta time is zero at Time.timeScale == 0.
        protected float AnimationDeltaTime =>
            useScaledTime && Time.timeScale > 0f ? Time.deltaTime : Time.unscaledDeltaTime;

        private bool isInitialized;
        private Coroutine showCoroutine;
        private bool isReportedActive;
        private CanvasGroup interactionCanvasGroup;
        private static BasePopupUI currentPopup;

        public static BasePopupUI CurrentPopup => currentPopup != null ? currentPopup : GetAnyActivePopup();

        public static void CloseCurrentPopup()
        {
            if (currentPopup == null)
                currentPopup = GetAnyActivePopup();

            currentPopup?.Show(false);
        }

        private void Start()
        {
            InitializeFromInspectorState();
        }

        public void Show(bool show)
        {
            if (show && !CanShowPopup(this))
                return;

            if (show)
            {
                ClosePreviousPopup();
                ReportActive(true);
            }

            if (!isInitialized)
            {
                isInitialized = true;
                if (!show && !gameObject.activeSelf)
                {
                    IsShowing = false;
                    return;
                }

                IsShowing = !show;
            }

            SetInteractionEnabled(show);
            if (showCoroutine == null && show == IsShowing)
                return;

            gameObject.SetActive(true);
            if (showCoroutine != null)
            {
                StopCoroutine(showCoroutine);
            }

            showCoroutine = StartCoroutine(RunShowAnimation(show));
            if (show)
                AudioManager.Instance?.PlayConfirm();
            else
                AudioManager.Instance?.PlayCancel();

            if (useHapticFeedback)
            {
                if (show)
                {
                    //VibrationService.Instance?.Vibrate(HapticPatterns.PresetType.Selection);
                }
                else
                {
                    //VibrationService.Instance?.Vibrate(HapticPatterns.PresetType.SoftImpact);
                }
            }
        }

        private IEnumerator RunShowAnimation(bool show)
        {
            yield return ShowAnimation(show);
            showCoroutine = null;
        }
        
        protected abstract IEnumerator ShowAnimation(bool show);
        public void ShowInstant(bool show)
        {
            ShowInstant(show, true);
        }

        public void ShowInstant(bool show, bool closePreviousPopup)
        {
            if (show && !CanShowPopup(this))
                return;

            if (show)
            {
                if (closePreviousPopup)
                    ClosePreviousPopup();
                ReportActive(true);
            }

            if (showCoroutine != null)
            {
                StopCoroutine(showCoroutine);
                showCoroutine = null;
            }

            isInitialized = true;
            if (show)
                gameObject.SetActive(true);

            SetInteractionEnabled(show);
            ApplyInstantState(show);
        }

        protected abstract void ApplyInstantState(bool show);

        private void ClosePreviousPopup()
        {
            BasePopupUI previousPopup = currentPopup;
            if (previousPopup == null)
                previousPopup = GetAnyActivePopup();

            if (previousPopup != null && previousPopup != this)
                previousPopup.Show(false);
        }

        private void InitializeFromInspectorState()
        {
            if (isInitialized)
                return;

            isInitialized = true;
            ShowInstant(!initialHided);
        }

        protected void ChangeStateTo(bool newState)
        {
            IsShowing = newState;
            ReportActive(newState);

            if (newState)
            {
                OnShowed?.Invoke();
            }
            else
            {
                OnHided?.Invoke();
                if (disableOnHide)
                    gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            SetInteractionEnabled(false);
            ReportActive(false);
        }

        private void SetInteractionEnabled(bool enabled)
        {
            if (interactionCanvasGroup == null)
            {
                interactionCanvasGroup = GetComponent<CanvasGroup>();
                if (interactionCanvasGroup == null && enabled)
                    interactionCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (interactionCanvasGroup == null)
                return;

            interactionCanvasGroup.interactable = enabled;
            interactionCanvasGroup.blocksRaycasts = enabled;
        }

        private void ReportActive(bool active)
        {
            if (active == isReportedActive)
                return;

            bool hadActivePopup = activePopups.Count > 0;
            isReportedActive = active;

            if (active)
            {
                activePopups.Add(this);
                currentPopup = this;
            }
            else
            {
                activePopups.Remove(this);

                if (currentPopup == this)
                    currentPopup = GetAnyActivePopup();
            }

            bool hasActivePopup = activePopups.Count > 0;
            if (hadActivePopup != hasActivePopup)
                AnyPopupVisibilityChanged?.Invoke(hasActivePopup);
        }

        private static BasePopupUI GetAnyActivePopup()
        {
            foreach (BasePopupUI popup in activePopups)
            {
                if (popup != null)
                    return popup;
            }

            return null;
        }
    }
}
