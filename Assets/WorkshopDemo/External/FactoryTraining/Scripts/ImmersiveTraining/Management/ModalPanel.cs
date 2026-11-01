using TMPro;
using UnityEngine;

namespace ImmersiveTraining.Management
{
    public class ModalPanel : Singleton<ModalPanel>
    {
        [SerializeField] private GameObject _modalPanel;
        [SerializeField] private TextMeshProUGUI _panelText;
        [SerializeField] private Animator _animator;
        [SerializeField, Min(.1f)] private float _displaySeconds = 2.4f;
        [SerializeField, Min(.01f)] private float _dismissSeconds = .2f;
        [SerializeField, Min(0)] private float _dismissDistance = 14f;
        private static readonly int PlayNotification = Animator.StringToHash("PlayNotification");
        private float _hideAt = float.PositiveInfinity;
        private CanvasGroup _canvasGroup;
        private RectTransform _panelRect;
        private Vector2 _shownPosition;
        private float _dismissAt;
        private bool _dismissing;
        private bool _started;
        private EventManager _events;

        void Start()
        {
            _started = true;
            _panelRect = _modalPanel.GetComponent<RectTransform>();
            _shownPosition = _panelRect.anchoredPosition;
            _canvasGroup = _modalPanel.GetComponent<CanvasGroup>();
            if (!_canvasGroup) _canvasGroup = _modalPanel.AddComponent<CanvasGroup>();
            HidePanel(null);
            Subscribe();
        }

        void OnEnable()
        {
            if (_started) Subscribe();
        }

        void Subscribe()
        {
            _events = EventManager.Instance;
            EventManager.StartListening(EventTypes.POP_MODAL_PANEL_WITH_MESSAGE_ON_OBJECT, PopModalPanelWithMessageFromObject);
            EventManager.StartListening(EventTypes.RESET_TRAINING, HidePanel);
            EventManager.StartListening(EventTypes.ALLOWED_TIME_EXPIRED, HidePanel);
            EventManager.StartListening(EventTypes.TRAINING_COMPLETE, HidePanel);
            EventManager.StartListening(EventTypes.RESET_TIMER, OnTimerReset);
        }

        void OnDisable()
        {
            HidePanel(null);
            if (!_events) return;
            EventManager.StopListening(EventTypes.POP_MODAL_PANEL_WITH_MESSAGE_ON_OBJECT, PopModalPanelWithMessageFromObject);
            EventManager.StopListening(EventTypes.RESET_TRAINING, HidePanel);
            EventManager.StopListening(EventTypes.ALLOWED_TIME_EXPIRED, HidePanel);
            EventManager.StopListening(EventTypes.TRAINING_COMPLETE, HidePanel);
            EventManager.StopListening(EventTypes.RESET_TIMER, OnTimerReset);
        }

        void Update()
        {
            if (_dismissing)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - _dismissAt) / _dismissSeconds);
                float eased = progress * progress * (3 - 2 * progress);
                _canvasGroup.alpha = 1 - eased;
                _panelRect.anchoredPosition = _shownPosition + Vector2.down * (_dismissDistance * eased);
                if (progress >= 1) HidePanel(null);
            }
            else if (Time.unscaledTime >= _hideAt) Dismiss();
        }

        public void ClosePanel()
        {
            if (!_modalPanel.activeSelf) return;
            Dismiss();
        }

        void Dismiss()
        {
            if (_dismissing) return;
            _hideAt = float.PositiveInfinity;
            _dismissAt = Time.unscaledTime;
            _dismissing = true;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        void HidePanel(GameObject caller)
        {
            _hideAt = float.PositiveInfinity;
            _dismissing = false;
            if (_modalPanel) _modalPanel.SetActive(false);
            RestoreAppearance();
        }

        void RestoreAppearance()
        {
            if (_panelRect) _panelRect.anchoredPosition = _shownPosition;
            if (!_canvasGroup) return;
            _canvasGroup.alpha = 1;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        void OnTimerReset(GameObject caller)
        {
            if (caller && caller.GetComponent<TrainingTypeManager>()) HidePanel(caller);
        }

        //This method requires that the calling object implements the IMessageForModal interface
        private void PopModalPanelWithMessageFromObject(GameObject callingObject)
        {
            _panelText.text = callingObject.GetComponent<IMessageForModalPanel>()?.MessageForModal();
            // The next state already owns its timer; dismissing a notice must not restart it.
            _dismissing = false;
            RestoreAppearance();
            _hideAt = Time.unscaledTime + _displaySeconds;
            _modalPanel.SetActive(true);
            if (_animator && _animator.isActiveAndEnabled)
                _animator.SetTrigger(PlayNotification);
        }
    }
}
