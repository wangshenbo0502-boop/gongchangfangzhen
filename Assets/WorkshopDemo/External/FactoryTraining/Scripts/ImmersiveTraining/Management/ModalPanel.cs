using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ImmersiveTraining.Management
{
    public class ModalPanel : Singleton<ModalPanel>
    {
        [SerializeField] private GameObject _modalPanel;
        [SerializeField] private TextMeshProUGUI _panelText;
        [SerializeField] private Animator _animator;
        [SerializeField] private TrainingTypeManager _trainingMode;
        [SerializeField] private Button _continueButton;
        [SerializeField, Min(.1f)] private float _assessmentDisplaySeconds = 1.8f;
        private static readonly int PlayNotification = Animator.StringToHash("PlayNotification");
        private float _hideAt = float.PositiveInfinity;
        private bool _started;
        private EventManager _events;

        void Start()
        {
            if (!_trainingMode) _trainingMode = FindFirstObjectByType<TrainingTypeManager>();
            if (!_continueButton) _continueButton = _modalPanel.GetComponentInChildren<Button>(true);
            _started = true;
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
            if (Time.unscaledTime >= _hideAt) HidePanel(null);
        }

        public void ClosePanel()
        {
            if (!_modalPanel.activeSelf) return;
            bool guided = !_trainingMode || _trainingMode.IsTrainingMode;
            HidePanel(null);
            if (guided) EventManager.TriggerEvent(EventTypes.RESET_TIMER, _modalPanel);
        }

        void HidePanel(GameObject caller)
        {
            _hideAt = float.PositiveInfinity;
            if (_modalPanel) _modalPanel.SetActive(false);
        }

        void OnTimerReset(GameObject caller)
        {
            if (caller && caller.GetComponent<TrainingTypeManager>()) HidePanel(caller);
        }

        //This method requires that the calling object implements the IMessageForModal interface
        private void PopModalPanelWithMessageFromObject(GameObject callingObject)
        {
            _panelText.text = callingObject.GetComponent<IMessageForModalPanel>()?.MessageForModal();
            bool guided = !_trainingMode || _trainingMode.IsTrainingMode;
            if (_continueButton) _continueButton.gameObject.SetActive(guided);
            // The next state already owns its timer; dismissing a notice must not restart it.
            _hideAt = guided ? float.PositiveInfinity : Time.unscaledTime + _assessmentDisplaySeconds;
            _modalPanel.SetActive(true);
            if (_animator && _animator.isActiveAndEnabled)
                _animator.SetTrigger(PlayNotification);
        }
    }
}
