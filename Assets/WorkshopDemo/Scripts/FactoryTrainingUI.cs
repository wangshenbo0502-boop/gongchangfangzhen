using System.Collections.Generic;
using DistributionCenter;
using ImmersiveTraining.Management;
using ImmersiveTraining.StateHandling;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WorkshopDemo
{
    /// <summary>Presentation only; camera, movement and training retain their existing state owners.</summary>
    public sealed class FactoryTrainingUI : MonoBehaviour
    {
        public static readonly Color Surface = new Color32(32, 34, 33, 250);
        public static readonly Color Raised = new Color32(55, 58, 55, 255);
        public static readonly Color Accent = new Color32(245, 197, 37, 255);
        public static readonly Color OnAccent = new Color32(34, 31, 20, 255);
        public static readonly Color Ink = new Color32(242, 242, 234, 255);
        public static readonly Color Muted = new Color32(177, 181, 172, 255);

        public CameraSwitcher cameraSwitcher;
        public RandomAgentMovement movement;
        public StateManager_Training training;
        public TrainingTypeManager trainingMode;
        public Button[] cameraButtons;
        public TMP_Text stepLabel;
        public TMP_Text modeLabel;
        public TMP_Text guidedTime;
        public TMP_Text modeButtonLabel;
        public TMP_Text movementLabel;
        public TMP_Text armValue;
        public Slider armSlider;
        public TMP_Text[] guideNumbers;
        public Image[] guideRows;
        public GameObject instruction;
        public GameObject[] instructionMenus;
        public GameObject[] trainingDialogs;
        public AudioMuter audioMuter;
        public Canvas[] interfaceCanvases;
        readonly Dictionary<Behaviour, bool> hiddenUIState = new Dictionary<Behaviour, bool>();
        List<StateTrainingWithPass> states;

        void Start() { states = training.GetTrainingStates(); }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.aKey.wasPressedThisFrame && audioMuter) audioMuter.ToggleAudio();
            if (keyboard.hKey.wasPressedThisFrame) ToggleInterface();
        }

        void ToggleInterface()
        {
            if (hiddenUIState.Count > 0)
            {
                RestoreInterface();
                return;
            }
            foreach (var canvas in interfaceCanvases)
            {
                if (!canvas) continue;
                hiddenUIState[canvas] = canvas.enabled;
                canvas.enabled = false;
                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (!raycaster) continue;
                hiddenUIState[raycaster] = raycaster.enabled;
                raycaster.enabled = false;
            }
        }

        void RestoreInterface()
        {
            foreach (var entry in hiddenUIState)
                if (entry.Key) entry.Key.enabled = entry.Value;
            hiddenUIState.Clear();
        }

        void OnDisable() { RestoreInterface(); }

        public static readonly string[] StepNames =
        {
            "前保险杠", "后部线缆", "机身盖板", "桥架底座", "线缆护罩",
            "机械臂底架", "底架支撑框", "驱动电机", "外伸缩臂", "内伸缩臂",
            "夹爪组件", "左侧滑轨", "右侧滑轨", "左侧滑轮", "右侧滑轮"
        };

        public static string StepTitle(int index)
        {
            return index >= 0 && index < StepNames.Length ? StepNames[index] : "装配步骤";
        }

        void LateUpdate()
        {
            for (int i = 0; i < cameraButtons.Length; i++)
            {
                bool selected = cameraSwitcher.cameras[i].activeSelf;
                cameraButtons[i].GetComponent<Image>().color = selected ? Accent : Raised;
                cameraButtons[i].GetComponentInChildren<TMP_Text>().color = selected ? OnAccent : Ink;
            }

            bool guided = trainingMode.IsTrainingMode;
            modeLabel.text = guided ? "引导装配" : "独立考核";
            modeButtonLabel.text = guided ? "切换考核" : "切换引导";
            guidedTime.gameObject.SetActive(guided);
            guidedTime.text = "不限时";
            int index = states.IndexOf(training.CurrentState as StateTrainingWithPass);
            stepLabel.text = $"当前工序   {training.GetStateTrackerText()}";
            movementLabel.text = movement.IsAutoRunning ? "停止自动运行" : "启动自动运行";
            armValue.text = $"{Mathf.RoundToInt(armSlider.normalizedValue * 100)}%";

            int row = index < 11 ? index : index < 13 ? 11 : 12;
            for (int i = 0; i < guideRows.Length; i++)
            {
                guideRows[i].color = i == row ? new Color32(77, 67, 32, 255) :
                    i % 2 == 0 ? new Color32(43, 46, 42, 255) : new Color32(38, 41, 37, 255);
                guideNumbers[i].color = i == row ? Accent : Muted;
            }
            bool assembly = cameraSwitcher.cameras[0].activeSelf;
            foreach (var menu in instructionMenus)
                menu.SetActive(guided && assembly);
            if (!assembly && instruction.activeSelf) CloseInstructions();
        }

        public void CloseInstructions()
        {
            // Use the existing toggle to keep its internal visibility state in sync.
            var toggle = cameraSwitcher.trainingSubMenu.GetComponentInChildren<UI_Toggle>(true);
            if (toggle != null) toggle.SetVisible(false);
        }

        public void ResetAssembly()
        {
            CloseInstructions();
            foreach (var dialog in trainingDialogs) dialog.SetActive(false);
            trainingMode.EnableTrainingModeWithReset();
        }
    }
}
