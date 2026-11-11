using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed class WorkshopSceneSwitcher : MonoBehaviour
    {
        public const string AssemblyScene = "Assets/WorkshopDemo/External/FactoryTraining/Scenes/urp_scene_with_training.unity";
        public const string InspectionScene = "Assets/WorkshopDemo/Scenes/WorkshopDemo.unity";
        public bool IsSwitching { get; private set; }
        public Font InterfaceFont { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            // Keep the existing single-scene players independent.
            if (!Application.CanStreamedLevelBeLoaded(AssemblyScene) ||
                !Application.CanStreamedLevelBeLoaded(InspectionScene)) return;
            var host = new GameObject("Workshop Scene Switcher");
            DontDestroyOnLoad(host);
            host.AddComponent<WorkshopSceneSwitcher>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || IsSwitching || WorkshopUI.Typing) return;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                SwitchTo(AssemblyScene);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                SwitchTo(InspectionScene);
        }

        public void SwitchTo(string path)
        {
            if (IsSwitching || !Application.CanStreamedLevelBeLoaded(path) || SceneManager.GetActiveScene().path == path) return;
            StartCoroutine(LoadScene(path));
        }

        void OnGUI()
        {
            if (SceneManager.GetActiveScene().path != AssemblyScene || IsSwitching) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas && !canvas.enabled) return;
            var style = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            if (InterfaceFont) style.font = InterfaceFont;
            if (GUI.Button(new Rect(Screen.width - 190, Screen.height - 50, 174, 34), "返回数字孪生平台", style))
                SwitchTo(InspectionScene);
        }

        IEnumerator LoadScene(string path)
        {
            IsSwitching = true;
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            // Pause and mute belong to the scene that was just unloaded.
            Time.timeScale = 1;
            AudioListener.volume = 1;
            IsSwitching = false;
        }
    }
}
