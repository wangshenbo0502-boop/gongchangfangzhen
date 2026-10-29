using System.Collections.Generic;
using ImmersiveTraining.Management;
using TMPro;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    public class TrainingCompleteHandler : MonoBehaviour
    {
        [SerializeField] private Material _instructionScreen;
        [SerializeField] private Texture _finalInstrunctionImage;
    
        [SerializeField] private GameObject _assemblyCompletePanel;
        [SerializeField] private TMP_Text _finalTimeText;
        [SerializeField] private List<TMP_Text> _stepTimeTextList;

        private TimeManager _timeManager;

        void Start()
        {
            EventManager.StartListening(EventTypes.TRAINING_COMPLETE, SetSceneForCompletedTraining);
            EventManager.StartListening(EventTypes.FINAL_TIMES_SAVED, UpdateUIWithFinalTimes);
        }
    
        private void SetSceneForCompletedTraining(GameObject arg0)
        {
            _instructionScreen.mainTexture = _finalInstrunctionImage;
            _assemblyCompletePanel.SetActive(true);
        }
    
        private void UpdateUIWithFinalTimes(GameObject callingTimeManagerObject)
        {
            _timeManager = callingTimeManagerObject.GetComponent<TimeManager>();

            if (_timeManager != null)
            {
                int totalTime = _timeManager.GetTotalTime();
                _finalTimeText.text = $"装配总用时   {totalTime} 秒";

                List<TimeData> timeDataList = _timeManager.StepTimeLog;
                var states = FindFirstObjectByType<StateManager_Training>().GetTrainingStates();
                for (int i = 0; i < _stepTimeTextList.Count; i++)
                {
                    int seconds = 0;
                    bool recorded = false;
                    foreach (var entry in timeDataList)
                        if (i < states.Count && entry.StepName == states[i].StateName)
                        {
                            seconds += entry.TimeInSeconds;
                            recorded = true;
                        }
                    string duration = recorded ? $"{seconds} 秒" : "未记录";
                    _stepTimeTextList[i].text =
                        $"{i + 1:00}   {WorkshopDemo.FactoryTrainingUI.StepTitle(i)}    {duration}";
                }
            }
        }
    }
}
