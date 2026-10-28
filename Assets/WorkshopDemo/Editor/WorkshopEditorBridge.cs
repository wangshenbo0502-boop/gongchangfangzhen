using System.IO;
using UnityEditor;

namespace WorkshopDemo.Editor
{
    [InitializeOnLoad]
    public static class WorkshopEditorBridge
    {
        static double nextPoll;
        static WorkshopEditorBridge() { EditorApplication.update += Poll; }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling ||
                EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + 2;
            File.WriteAllText("Evidence/editor-state.txt", "Playing=" + EditorApplication.isPlaying +
                " Transition=" + EditorApplication.isPlayingOrWillChangePlaymode +
                " Scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            const string path = "Evidence/editor-command.txt";
            if (!File.Exists(path)) return;
            string command = File.ReadAllText(path).Trim();
            File.Delete(path);
            if (command == "stop") EditorApplication.isPlaying = false;
            if (command == "play") EditorApplication.isPlaying = true;
            if (command == "equipment-audit") WorkshopFoundationBuilder.Audit();
            if (command == "build-demo") WorkshopFoundationBuilder.Build();
            if (command == "verify-demo") WorkshopVerification.Begin();
            if (command == "build-player") WorkshopVerification.BuildPlayer();
            if (command == "probe") WorkshopAssetProbe.Run();
            if (command == "package-manager")
                UnityEditor.PackageManager.UI.Window.Open("344832");
            if (command == "import-factory") FactoryTrainingImport.Import();
            if (command == "inventory-factory") FactoryTrainingImport.Inventory();
            if (command == "capture-factory") FactoryTrainingImport.CaptureScene();
            if (command == "prepare-foundation") FactoryTrainingImport.CreateFoundation();
            if (command == "begin-extract")
            {
                AssetDatabase.DisallowAutoRefresh();
                EditorApplication.LockReloadAssemblies();
                File.WriteAllText("Evidence/extraction-lock.txt", "locked");
            }
            if (command == "finish-extract")
            {
                AssetDatabase.AllowAutoRefresh();
                EditorApplication.UnlockReloadAssemblies();
                File.WriteAllText("Evidence/extraction-lock.txt", "released");
                AssetDatabase.Refresh();
            }
            if (command.StartsWith("inspect-scene:"))
                FactoryTrainingImport.InspectScene(command.Substring("inspect-scene:".Length));
        }
    }
}
