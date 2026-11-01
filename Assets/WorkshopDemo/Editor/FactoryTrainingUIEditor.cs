using System;
using System.IO;
using System.Linq;
using System.Text;
using DistributionCenter;
using ImmersiveTraining.Management;
using ImmersiveTraining.StateHandling;
using ImmersiveTraining.TrainingInteractions;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace WorkshopDemo.Editor
{
    public static class FactoryTrainingUIEditor
    {
        public const string ScenePath =
            "Assets/WorkshopDemo/External/FactoryTraining/Scenes/urp_scene_with_training.unity";
        const string FontPath = "Assets/WorkshopDemo/Generated/FactoryUI/SourceHanSansSC UI.asset";
        const string RoundedPath = "Assets/WorkshopDemo/Generated/FactoryUI/RoundedSurface.asset";
        static TMP_FontAsset chinese;
        static Font sourceFont;
        static Sprite roundedSurface;
        static readonly string[] Instructions =
        {
            "将前保险杠安装到主机架前端。",
            "将线缆连接到主机架后部。",
            "将机身盖板安装到主机架上方。",
            "将桥架底座安装到主机架后端。",
            "将线缆护罩安装到底座上方的线缆接口处。",
            "将机械臂底架滑入桥架底座的槽口。",
            "将支撑框安装到机械臂底架的前端开口。",
            "将驱动电机安装到机械臂底架后部。",
            "将外伸缩臂插入支撑框。",
            "将内伸缩臂装入外伸缩臂。",
            "将夹爪组件安装到机械臂前端。",
            "将左右滑轨安装到机身上方。",
            "将左右滑轮竖直安装到滑轨前端。"
        };

        [MenuItem("Tools/Workshop Demo/Apply Chinese Factory UI")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("请在停止运行的装配训练场景中应用界面。");

            Directory.CreateDirectory("Evidence");
            const string backup = "Evidence/factory-scene-before-ui-20261029.unity";
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);
            PrepareFont();
            PrepareRoundedSurface();
            var canvas = SceneManager.GetActiveScene().GetRootGameObjects()
                .Single(root => root.name == "Canvas").GetComponent<Canvas>();
            foreach (var misplaced in UnityEngine.Object.FindObjectsByType<FactoryTrainingUI>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (misplaced.gameObject != canvas.gameObject)
                    UnityEngine.Object.DestroyImmediate(misplaced);
            var ui = canvas.GetComponent<FactoryTrainingUI>() ??
                     canvas.gameObject.AddComponent<FactoryTrainingUI>();
            ui.cameraSwitcher = Find<CameraSwitcher>();
            ui.movement = Find<RandomAgentMovement>();
            ui.training = Find<StateManager_Training>();
            ui.trainingMode = Find<TrainingTypeManager>();
            ui.cameraButtons = ui.cameraSwitcher.switchButtons;
            ui.instructionMenus = ui.trainingMode.objectsUsedForTrainingMode;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.renderMode == RenderMode.WorldSpace) continue;
                var scaler = c.GetComponent<CanvasScaler>() ?? c.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600, 900);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
            }

            LayoutNavigation(canvas.transform, ui);
            LayoutTraining(ui);
            LayoutUsage(ui);
            LayoutInstructions(ui);
            ConfigureShortcuts(canvas.transform, ui);
            LayoutDialogs(ui);
            LocalizeStates(ui);
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var t in c.GetComponentsInChildren<TMP_Text>(true))
                {
                    t.font = chinese;
                    t.characterSpacing = 0;
                    if (t.text == "SHOW ME" || t.text == "演示装配")
                    {
                        t.text = "演示装配";
                        t.color = FactoryTrainingUI.OnAccent;
                        var demo = t.GetComponentInParent<Button>();
                        if (demo)
                        {
                            Paint(demo.transform, FactoryTrainingUI.Accent);
                            demo.targetGraphic = demo.GetComponent<Image>();
                            SetButtonTint(demo);
                        }
                    }
                }
                foreach (var t in c.GetComponentsInChildren<Text>(true)) t.font = sourceFont;
            }
            // Record overrides as well as scene dirtiness, including inactive prefab UI.
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component && PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            File.WriteAllText("Evidence/factory-ui-applied-20261029.txt",
                $"Time={DateTime.Now:O}\nScene={ScenePath}\n" +
                "Chinese UI saved; equipment yellow #F5C525; sliced 8px rounded surfaces.\n" +
                "Corner toolbar removed; A/H shortcuts; separated slider and diagram; timed assessment notices.\n");
            Inspect();
        }

        static T Find<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        [MenuItem("Tools/Workshop Demo/Fix Factory Training Dialogs")]
        public static void ApplyDialogs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("请在停止运行的装配训练场景中修复弹窗。");

            Directory.CreateDirectory("Evidence");
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
            File.Copy(ScenePath, $"Evidence/factory-scene-before-dialog-fix-{stamp}.unity");
            PrepareFont();
            PrepareRoundedSurface();
            var ui = Find<FactoryTrainingUI>();
            LayoutDialogs(ui);
            // Keep the targeted fix reproducible without reapplying unrelated UI layouts.
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component && PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            File.WriteAllText($"Evidence/factory-dialogs-applied-{stamp}.txt",
                $"Time={DateTime.Now:O}\nScene={ScenePath}\n" +
                "Completion list uses one column inside its surface; modal canvas has a GraphicRaycaster.\n" +
                "Continue starts dismissal immediately; both modes wait 2.4 unscaled seconds before automatic dismissal.\n" +
                "Dismissal fades and slides down 14 UI units over 0.2 unscaled seconds.\n");
        }

        static void PrepareRoundedSurface()
        {
            roundedSurface = AssetDatabase.LoadAllAssetsAtPath(RoundedPath).OfType<Sprite>().FirstOrDefault();
            if (roundedSurface) return;
            const int size = 64;
            const float radius = 16;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(RoundedPath);
            if (!texture)
            {
                texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "RoundedSurface";
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(Mathf.Abs(x + .5f - size / 2f) - (size / 2f - radius), 0);
                        float dy = Mathf.Max(Mathf.Abs(y + .5f - size / 2f) - (size / 2f - radius), 0);
                        float alpha = Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy));
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
                    }
                texture.SetPixels32(pixels);
                texture.Apply();
                AssetDatabase.CreateAsset(texture, RoundedPath);
            }
            roundedSurface = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f),
                100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            roundedSurface.name = "RoundedSurface";
            AssetDatabase.AddObjectToAsset(roundedSurface, texture);
        }

        static void PrepareFont()
        {
            sourceFont = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/WorkshopDemo/External/SourceHanSans/SourceHanSansSC-Regular.otf");
            FontEngine.InitializeFontEngine();
            var loadResult = FontEngine.LoadFontFace(sourceFont, 36, 0);
            if (loadResult != FontEngineError.Success)
                throw new InvalidOperationException("无法加载中文字体：" + loadResult);
            chinese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (!chinese || !chinese.material || chinese.atlasTextures.Length == 0 ||
                !chinese.atlasTextures[0])
            {
                Directory.CreateDirectory("Assets/WorkshopDemo/Generated/FactoryUI");
                var replacement = TMP_FontAsset.CreateFontAsset(sourceFont, 36, 5, GlyphRenderMode.SDFAA,
                    2048, 2048, AtlasPopulationMode.Dynamic, true);
                if (!replacement) throw new InvalidOperationException("无法创建中文字体。");
                // Keep the generated resources alive across CreateAsset's import.
                var material = replacement.material;
                var textures = replacement.atlasTextures;
                replacement.name = "SourceHanSansSC UI";
                if (chinese)
                {
                    EditorUtility.CopySerialized(replacement, chinese);
                    UnityEngine.Object.DestroyImmediate(replacement);
                }
                else
                {
                    chinese = replacement;
                    AssetDatabase.CreateAsset(chinese, FontPath);
                }
                AssetDatabase.AddObjectToAsset(material, chinese);
                foreach (var texture in textures)
                    if (texture) AssetDatabase.AddObjectToAsset(texture, chinese);
                chinese.material = material;
                chinese.atlasTextures = textures;
                chinese.ReadFontAssetDefinition();
            }
            string characters = string.Join("", Instructions) + string.Join("", FactoryTrainingUI.StepNames) +
                "工业车间设备交互实训组装使用远景近景引导装配独立考核当前工序步骤剩余时间不限时" +
                "工序清单切换重置启动停止自动运行底盘机械臂伸缩完成确认继续用秒未记录总用时返回" +
                "考核已结束本轮操作超时请重新开始训练全部部件就位声音界面演示装配关闭前进后退左转右转" +
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz :/%.，。·×↑←↓→";
            chinese.TryAddCharacters(characters, out _);
            // TryAddCharacters also returns false when every glyph is already present.
            if (!chinese.HasCharacters(characters, out System.Collections.Generic.List<char> missing))
                throw new InvalidOperationException("中文字体缺少字符：" + new string(missing.ToArray()));
            EditorUtility.SetDirty(chinese);
            EditorUtility.SetDirty(chinese.material);
            foreach (var texture in chinese.atlasTextures)
                if (texture) EditorUtility.SetDirty(texture);
        }

        static void LayoutNavigation(Transform canvas, FactoryTrainingUI ui)
        {
            var left = canvas.Find("TopLeftPanel");
            Place(left, 24, 24, 360, 132);
            Paint(left, FactoryTrainingUI.Surface);
            var title = left.Find("Text_Cameras").GetComponent<TMP_Text>();
            SetText(title, "工业车间 · 设备实训", 20, FactoryTrainingUI.Ink);
            Place(title.transform, 20, 16, 320, 30);
            var bar = Box(left, "UI_Accent", FactoryTrainingUI.Accent);
            Place(bar, 8, 20, 3, 23);
            bar.GetComponent<Image>().raycastTarget = false;
            string[] labels = { "组装", "使用", "远景", "近景" };
            for (int i = 0; i < ui.cameraButtons.Length; i++)
            {
                var b = ui.cameraButtons[i];
                Place(b.transform, 20 + i * 81, 68, 76, 42);
                StyleButton(b, labels[i]);
            }
            foreach (string name in new[] { "low_BTN", "medium_BTN", "high_BTN" })
                left.Find(name).gameObject.SetActive(false);
        }

        static void LayoutTraining(FactoryTrainingUI ui)
        {
            var root = ui.cameraSwitcher.trainingSubMenu.transform;
            Place(root, 0, 148, 360, 246);
            Paint(root, FactoryTrainingUI.Surface);
            var panel = root.Find("subMenu_01");
            Place(panel, 0, 0, 360, 246);
            panel.Find("Panel_02").gameObject.SetActive(false);
            panel.Find("line").gameObject.SetActive(false);
            ui.modeLabel = Label(panel, "UI_Mode", "引导装配", 20, FactoryTrainingUI.Ink);
            Place(ui.modeLabel.transform, 20, 16, 206, 28);
            ui.stepLabel = Label(panel, "UI_Step", "当前工序   1 / 15", 16, FactoryTrainingUI.Muted);
            Place(ui.stepLabel.transform, 20, 52, 320, 26);
            var label = Label(panel, "UI_TimerCaption", "步骤剩余时间", 14, FactoryTrainingUI.Muted);
            Place(label.transform, 20, 94, 168, 22);
            var timerRoot = panel.Find("Timer");
            Place(timerRoot, 20, 119, 320, 40);
            timerRoot.Find("Image").gameObject.SetActive(false);
            var timer = timerRoot.Find("TimeText").GetComponent<TMP_Text>();
            Place(timer.transform, 0, 0, 320, 40);
            SetText(timer, "0:00", 30, FactoryTrainingUI.Ink);
            ui.guidedTime = Label(timerRoot, "UI_GuidedTime", "不限时", 28, FactoryTrainingUI.Ink);
            Place(ui.guidedTime.transform, 0, 0, 320, 40);
            var info = panel.Find("info_btn").GetComponent<Button>();
            Place(info.transform, 242, 16, 98, 32);
            StyleButton(info, "工序清单", 14);
            var mode = panel.Find("TrainingModeButton").GetComponent<Button>();
            mode.gameObject.SetActive(true);
            Place(mode.transform, 20, 184, 154, 42);
            ui.modeButtonLabel = StyleButton(mode, "切换考核");
            mode.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(mode.onClick, ui.trainingMode.ToggleTrainingMode);
            var reset = NewButton(panel, "UI_Reset", "重置装配");
            Place(reset.transform, 186, 184, 154, 42);
            reset.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(reset.onClick, ui.ResetAssembly);
        }

        static void LayoutUsage(FactoryTrainingUI ui)
        {
            var root = ui.cameraSwitcher.subMenu.transform;
            Place(root, 0, 148, 360, 404);
            Paint(root, FactoryTrainingUI.Surface);
            var drive = root.Find("subMenu_01");
            Place(drive, 0, 0, 360, 198);
            drive.Find("Panel_02").gameObject.SetActive(false);
            Place(Label(drive, "UI_DriveTitle", "底盘运行", 20, FactoryTrainingUI.Ink).transform,
                20, 16, 320, 30);
            var start = drive.Find("startStopBTN").GetComponent<Button>();
            Place(start.transform, 20, 58, 320, 42);
            ui.movementLabel = StyleButton(start, "启动自动运行");
            string[] keys = { "W", "A", "S", "D" };
            string[] arrows = { "↑", "←", "↓", "→" };
            string[] captions = { "前进", "左转", "后退", "右转" };
            for (int i = 0; i < keys.Length; i++)
            {
                var key = drive.Find("key_" + keys[i]);
                Place(key, 20 + i * 82, 116, 72, 34);
                Paint(key, FactoryTrainingUI.Raised);
                key.Find("Image").gameObject.SetActive(false);
                var keyText = key.Find("Text (TMP)").GetComponent<TMP_Text>();
                SetText(keyText, arrows[i], 17, FactoryTrainingUI.Ink);
                Stretch(keyText.transform);
                keyText.alignment = TextAlignmentOptions.Center;
                Place(Label(drive, "UI_KeyCaption" + i, captions[i], 13,
                    FactoryTrainingUI.Muted).transform, 20 + i * 82, 156, 72, 22);
            }
            drive.Find("GameObject").gameObject.SetActive(false);
            var arm = root.Find("subMenu_02");
            Place(arm, 0, 206, 360, 198);
            arm.Find("Panel_03").gameObject.SetActive(false);
            var divider = Box(root, "UI_Divider", FactoryTrainingUI.Raised);
            Place(divider, 20, 198, 320, 1);
            Paint(divider, FactoryTrainingUI.Raised, false);
            divider.GetComponent<Image>().raycastTarget = false;
            var title = arm.Find("Text_ArmController").GetComponent<TMP_Text>();
            SetText(title, "机械臂伸缩", 20, FactoryTrainingUI.Ink);
            Place(title.transform, 20, 10, 230, 30);
            ui.armValue = Label(arm, "UI_ArmValue", "0%", 16, FactoryTrainingUI.Muted);
            Place(ui.armValue.transform, 278, 10, 62, 30);
            ui.armValue.alignment = TextAlignmentOptions.Right;
            var slider = arm.Find("slider_Translate").GetComponent<Slider>();
            ui.armSlider = slider;
            Place(slider.transform, 28, 57, 304, 32);
            var background = slider.transform.Find("Background");
            Stretch(background, 0, 13, 0, 13);
            Paint(background, new Color32(78, 81, 74, 255));
            var fillArea = slider.transform.Find("Fill Area");
            Stretch(fillArea, 0, 13, 0, 13);
            Stretch(fillArea.Find("Fill"));
            Paint(fillArea.Find("Fill"), FactoryTrainingUI.Accent);
            Stretch(slider.transform.Find("Handle Slide Area"), 8, 4, 8, 4);
            var handle = slider.handleRect;
            handle.localScale = Vector3.one;
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.up;
            handle.pivot = new Vector2(.5f, .5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(16, 0);
            Paint(handle, FactoryTrainingUI.Ink);
            slider.transform.Find("GameObject").gameObject.SetActive(false);
            var diagram = arm.Find("motoIcon");
            // Center the visible artwork; transparent sprite padding is not part of its silhouette.
            const float diagramScale = .7f;
            Place(diagram, 178, 144, 0, 0);
            diagram.localScale = Vector3.one * diagramScale;
            foreach (var image in diagram.GetComponentsInChildren<Image>(true))
            {
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(420, 120);
                rect.anchoredPosition3D = Vector3.zero;
                rect.localScale = Vector3.one;
                image.color = FactoryTrainingUI.Muted;
                image.raycastTarget = false;
            }
        }

        static void LayoutInstructions(FactoryTrainingUI ui)
        {
            var root = GameObject.Find("ImmersiveTraining/FullscreenPanel").transform.Find("Instruction");
            ui.instruction = root.gameObject;
            foreach (Transform child in root)
                if (child.name != "UI_Content") child.gameObject.SetActive(false);
            Place(root, 24, 88, 658, 744, new Vector2(1, 1), new Vector2(1, 1));
            Paint(root, FactoryTrainingUI.Surface);
            var content = Ensure(root, "UI_Content");
            Stretch(content);
            Place(Label(content, "Title", "组装工序", 23, FactoryTrainingUI.Ink).transform,
                24, 20, 490, 32);
            var close = NewButton(content, "Close", "×");
            Place(close.transform, 592, 16, 42, 38);
            close.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(close.onClick, ui.CloseInstructions);
            ui.guideNumbers = new TMP_Text[Instructions.Length];
            ui.guideRows = new Image[Instructions.Length];
            for (int i = 0; i < Instructions.Length; i++)
            {
                var row = Box(content, "Step" + i, FactoryTrainingUI.Raised);
                Place(row, 16, 70 + i * 50, 626, 48);
                ui.guideRows[i] = row.GetComponent<Image>();
                ui.guideNumbers[i] = Label(row, "Number", (i + 1).ToString("00"), 17,
                    FactoryTrainingUI.Muted);
                Place(ui.guideNumbers[i].transform, 14, 6, 38, 36);
                var text = Label(row, "Instruction", Instructions[i], 17, FactoryTrainingUI.Ink);
                Place(text.transform, 62, 4, 548, 40);
            }
            root.gameObject.SetActive(false);
        }

        static void ConfigureShortcuts(Transform canvas, FactoryTrainingUI ui)
        {
            var root = canvas.Find("TopRightPanel");
            if (root) UnityEngine.Object.DestroyImmediate(root.gameObject);
            ui.audioMuter = Find<AudioMuter>();
            ui.audioMuter.muteToggleButton = null;
            ui.interfaceCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var toggle in UnityEngine.Object.FindObjectsByType<UI_Toggle>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (toggle.objectsToToggle.Contains(canvas.Find("TopLeftPanel").gameObject))
                    UnityEngine.Object.DestroyImmediate(toggle);
            foreach (var movement in UnityEngine.Object.FindObjectsByType<AgentMovement>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                movement.useArrowKeys = true;
        }

        static void LayoutDialogs(FactoryTrainingUI ui)
        {
            var full = GameObject.Find("ImmersiveTraining/FullscreenPanel").transform;
            var complete = full.Find("FinishPanel");
            DialogSurface(complete, 680, 718);
            var title = complete.Find("Text (TMP) ").GetComponent<TMP_Text>();
            SetText(title, "组装完成", 30, FactoryTrainingUI.Ink);
            CenterPlace(title.transform, 30, 24, 620, 44, 680, 718);
            var finalTime = complete.Find("FinalTimeText").GetComponent<TMP_Text>();
            SetText(finalTime, "装配总用时   0 秒", 18, FactoryTrainingUI.Muted);
            CenterPlace(finalTime.transform, 30, 78, 620, 30, 680, 718);
            complete.Find("Image").gameObject.SetActive(false);
            var steps = complete.Find("StepsTimePanel");
            CenterPlace(steps, 30, 126, 620, 480, 680, 718);
            var grid = steps.GetComponent<GridLayoutGroup>();
            grid.enabled = true;
            grid.padding = new RectOffset();
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Vertical;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 1;
            grid.cellSize = new Vector2(620, 31);
            grid.spacing = Vector2.zero;
            var stepsImage = steps.GetComponent<Image>();
            if (stepsImage) stepsImage.enabled = false;
            var list = steps.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < list.Length; i++)
            {
                SetText(list[i], $"{i + 1:00}   {FactoryTrainingUI.StepTitle(i)}    未记录",
                    17, FactoryTrainingUI.Ink);
                Place(list[i].transform, 0, i * 31, 620, 31);
            }
            var retry = complete.Find("Button").GetComponent<Button>();
            CenterPlace(retry.transform, 30, 644, 620, 44, 680, 718);
            StyleButton(retry, "重新装配");
            retry.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(retry.onClick, ui.ResetAssembly);

            var timeout = full.Find("TimesUpPanel");
            DialogSurface(timeout, 540, 296);
            var times = timeout.Find("Text (TMP)").GetComponent<TMP_Text>();
            SetText(times, "本轮操作超时", 28, FactoryTrainingUI.Ink);
            CenterPlace(times.transform, 30, 24, 480, 42, 540, 296);
            var message = timeout.Find("Text (TMP) (1)").GetComponent<TMP_Text>();
            SetText(message, "考核已结束，请返回引导装配重新开始。", 18, FactoryTrainingUI.Muted);
            CenterPlace(message.transform, 30, 88, 480, 60, 540, 296);
            timeout.Find("Image").gameObject.SetActive(false);
            var train = timeout.Find("TrainingButton").GetComponent<Button>();
            CenterPlace(train.transform, 30, 214, 480, 44, 540, 296);
            StyleButton(train, "返回引导装配");
            train.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(train.onClick, ui.ResetAssembly);

            var modalCanvas = GameObject.Find("ImmersiveTraining/ModalCanvas");
            var raycaster = modalCanvas.GetComponent<GraphicRaycaster>();
            if (!raycaster) raycaster = modalCanvas.AddComponent<GraphicRaycaster>();
            raycaster.enabled = true;
            var modal = modalCanvas.transform.Find("ModalPanel");
            var group = modal.GetComponent<CanvasGroup>();
            if (!group) group = modal.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1;
            group.interactable = true;
            group.blocksRaycasts = true;
            // The controller owns dismissal; legacy animation must not move the notice offscreen.
            var animator = modal.GetComponent<Animator>();
            if (animator) animator.enabled = false;
            Place(modal, 0, 24, 600, 102, new Vector2(.5f, 0), new Vector2(.5f, 0));
            Paint(modal, FactoryTrainingUI.Surface);
            modal.Find("Image").gameObject.SetActive(false);
            var notification = modal.Find("Text (TMP)").GetComponent<TMP_Text>();
            SetText(notification, "工序完成", 20, FactoryTrainingUI.Ink);
            Place(notification.transform, 24, 15, 416, 72);
            var ok = modal.Find("Button").GetComponent<Button>();
            ok.gameObject.SetActive(true);
            Place(ok.transform, 464, 30, 112, 42);
            StyleButton(ok, "继续");
            ok.onClick = new Button.ButtonClickedEvent();
            var controller = Find<ModalPanel>();
            UnityEventTools.AddPersistentListener(ok.onClick, controller.ClosePanel);
            var modalSettings = new SerializedObject(controller);
            modalSettings.FindProperty("_displaySeconds").floatValue = 2.4f;
            modalSettings.FindProperty("_dismissSeconds").floatValue = .2f;
            modalSettings.FindProperty("_dismissDistance").floatValue = 14f;
            modalSettings.ApplyModifiedPropertiesWithoutUndo();
            ui.trainingDialogs = new[] { complete.gameObject, timeout.gameObject, modal.gameObject };
            modal.gameObject.SetActive(false);
        }

        static void LocalizeStates(FactoryTrainingUI ui)
        {
            var states = ui.training.GetTrainingStates();
            for (int i = 0; i < states.Count; i++)
            {
                var serialized = new SerializedObject(states[i]);
                serialized.FindProperty("_infoMessage").stringValue = FactoryTrainingUI.StepTitle(i);
                serialized.FindProperty("_messageForModalPanel").stringValue =
                    i == states.Count - 1 ? "全部部件已就位，组装完成。" :
                    $"工序 {i + 1:00} 完成：{FactoryTrainingUI.StepTitle(i)}";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void DialogSurface(Transform root, float width, float height)
        {
            Stretch(root);
            Paint(root, new Color32(8, 10, 9, 170), false);
            var surface = Box(root, "UI_Surface", FactoryTrainingUI.Surface);
            surface.SetAsFirstSibling();
            CenterPlace(surface, 0, 0, width, height, width, height);
        }

        static void CenterPlace(Transform t, float x, float y, float w, float h, float totalW, float totalH)
        {
            Place(t, x - totalW / 2, y - totalH / 2, w, h,
                new Vector2(.5f, .5f), new Vector2(0, 1));
        }

        static RectTransform Ensure(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing) return existing;
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static RectTransform Box(Transform parent, string name, Color color)
        {
            var t = Ensure(parent, name);
            Paint(t, color);
            return t;
        }

        static void Paint(Transform t, Color color, bool rounded = true)
        {
            var image = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
            image.enabled = true;
            image.sprite = rounded ? roundedSurface : null;
            image.type = rounded ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 2;
            image.fillCenter = true;
            image.color = color;
            image.raycastTarget = true;
        }

        static TMP_Text Label(Transform parent, string name, string text, float size, Color color)
        {
            var rt = Ensure(parent, name);
            var label = rt.GetComponent<TextMeshProUGUI>() ?? rt.gameObject.AddComponent<TextMeshProUGUI>();
            SetText(label, text, size, color);
            return label;
        }

        static void SetText(TMP_Text label, string text, float size, Color color)
        {
            label.text = text;
            label.font = chinese;
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.fontStyle = FontStyles.Normal;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.margin = Vector4.zero;
            label.characterSpacing = 0;
            label.raycastTarget = false;
        }

        static Button NewButton(Transform parent, string name, string label)
        {
            var rt = Ensure(parent, name);
            var button = rt.GetComponent<Button>() ?? rt.gameObject.AddComponent<Button>();
            StyleButton(button, label);
            return button;
        }

        static TMP_Text StyleButton(Button button, string label, float size = 16)
        {
            Paint(button.transform, FactoryTrainingUI.Raised);
            button.targetGraphic = button.GetComponent<Image>();
            SetButtonTint(button);
            foreach (var legacy in button.GetComponentsInChildren<Text>(true))
                legacy.gameObject.SetActive(false);
            foreach (var old in button.GetComponentsInChildren<TMP_Text>(true))
                if (old.name != "UI_Label") old.gameObject.SetActive(false);
            var text = Label(button.transform, "UI_Label", label, size, FactoryTrainingUI.Ink);
            text.gameObject.SetActive(true);
            text.alignment = TextAlignmentOptions.Center;
            Stretch(text.transform, 4, 2, 4, 2);
            return text;
        }

        static void SetButtonTint(Button button)
        {
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(255, 244, 211, 255);
            colors.pressedColor = new Color32(205, 192, 153, 255);
            colors.selectedColor = Color.white;
            button.colors = colors;
        }

        static void Place(Transform transform, float x, float y, float width, float height,
            Vector2? anchor = null, Vector2? pivot = null)
        {
            var rt = (RectTransform)transform;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchorMin = rt.anchorMax = anchor ?? new Vector2(0, 1);
            rt.pivot = pivot ?? new Vector2(0, 1);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition3D = new Vector3(
                (anchor.HasValue && anchor.Value.x == 1) ? -x : x,
                (anchor.HasValue && anchor.Value.y == 0) ? y : -y, 0);
        }

        static void Stretch(Transform transform, float left = 0, float top = 0,
            float right = 0, float bottom = 0)
        {
            var rt = (RectTransform)transform;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Inspect()
        {
            var result = new StringBuilder();
            result.AppendLine($"Scene={SceneManager.GetActiveScene().path} Playing={Application.isPlaying}");
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                result.AppendLine($"CANVAS {PathOf(canvas.transform)} {canvas.renderMode}");
                Walk(canvas.transform, result, 0);
            }
            foreach (var toggle in UnityEngine.Object.FindObjectsByType<UI_Toggle>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                result.AppendLine($"TOGGLE {PathOf(toggle.transform)} => " +
                    string.Join(", ", toggle.objectsToToggle.Select(o => o ? PathOf(o.transform) : "null")));
            var mode = UnityEngine.Object.FindFirstObjectByType<TrainingTypeManager>();
            if (mode)
                result.AppendLine("MODE OBJECTS " + string.Join(", ",
                    mode.objectsUsedForTrainingMode.Select(o => PathOf(o.transform))));
            File.WriteAllText("Evidence/factory-ui-hierarchy-20261029.txt", result.ToString());
        }

        static void Walk(Transform t, StringBuilder result, int depth)
        {
            var text = t.GetComponent<TMP_Text>();
            var legacy = t.GetComponent<Text>();
            result.AppendLine(new string(' ', depth * 2) + t.name + " active=" + t.gameObject.activeSelf +
                (text ? " TMP=" + text.text.Replace("\n", "\\n") : "") +
                (legacy ? " TEXT=" + legacy.text.Replace("\n", "\\n") : "") +
                (t.GetComponent<Button>() ? " BUTTON" : ""));
            foreach (Transform child in t) Walk(child, result, depth + 1);
        }

        public static string PathOf(Transform t) =>
            t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    }
}
