using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FanViewerBuild
{
    [Serializable] sealed class Finish
    {
        public string name, family;
        public float[] color;
        public float metallic, roughness, relief;
    }
    [Serializable] sealed class Finishes { public Finish[] materials; }
    [Serializable] sealed class MotionGeometry
    {
        public string date;
        public int motionVersion;
        public Vector3 axisX, axisY, axisZ;
        public MotionMesh[] meshes;
    }
    [Serializable] sealed class MotionMesh
    {
        public string name;
        public int part;
        public Vector3[] vertices;
        public int[] triangles;
    }

    static string Key(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    public static void BuildReviewScene()
    {
        SetupScene();
    }

    public static void BuildWindows()
    {
        SetupScene();
        BuildPlayer();
    }

    public static void BuildWindowsRuntime()
    {
        EditorSceneManager.OpenScene("Assets/Generated/FanViewer.unity");
        BuildPlayer();
    }

    public static void BuildWindowsStudio()
    {
        EditorSceneManager.OpenScene("Assets/Generated/FanViewer.unity");
        ApplyStudioPresentation();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        BuildPlayer();
    }

    static void BuildPlayer()
    {
        PlayerSettings.enableFrameTimingStats = true;
        string output = Path.GetFullPath("../IndustrialFanViewer/IndustrialFanViewer.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Generated/FanViewer.unity", true)
        };
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Generated/FanViewer.unity" },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Fan Windows build failed: " + report.summary.result);
        Debug.Log("FAN_WINDOWS_BUILD " + output + " bytes=" + report.summary.totalSize);
    }

    static void SetupScene()
    {
        Directory.CreateDirectory("Assets/Generated");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextures();

        AssetDatabase.DeleteAsset("Assets/Generated/FanRenderer.asset");
        var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
        renderer.name = "Fan forward renderer";
        AssetDatabase.CreateAsset(renderer, "Assets/Generated/FanRenderer.asset");
        var ao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
        AssetDatabase.AddObjectToAsset(ao, renderer);
        ao.name = "Contact occlusion";
        var aoConfig = new SerializedObject(ao);
        aoConfig.FindProperty("m_Settings.Intensity").floatValue = .38f;
        aoConfig.FindProperty("m_Settings.Radius").floatValue = .10f;
        aoConfig.FindProperty("m_Settings.Downsample").boolValue = false;
        aoConfig.ApplyModifiedPropertiesWithoutUndo();
        renderer.rendererFeatures.Add(ao);
        var clarity = ScriptableObject.CreateInstance<FanClarity>();
        AssetDatabase.AddObjectToAsset(clarity, renderer);
        clarity.name = "Fine detail clarity";
        clarity.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/FanClarity.shader");
        if (!clarity.shader || ShaderUtil.ShaderHasError(clarity.shader))
            throw new InvalidOperationException("Fan clarity shader failed compilation.");
        clarity.Create();
        EditorUtility.SetDirty(clarity);
        renderer.rendererFeatures.Add(clarity);
        renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
        EditorUtility.SetDirty(renderer);

        AssetDatabase.DeleteAsset("Assets/Generated/FanPipeline.asset");
        var pipeline = UniversalRenderPipelineAsset.Create(renderer);
        pipeline.name = "Fan studio quality";
        pipeline.msaaSampleCount = 4;
        pipeline.renderScale = 2f;
        pipeline.supportsHDR = true;
        pipeline.supportsCameraDepthTexture = true;
        pipeline.shadowDistance = 10;
        var pipelineConfig = new SerializedObject(pipeline);
        pipelineConfig.FindProperty("m_SoftShadowsSupported").boolValue = true;
        pipelineConfig.FindProperty("m_AdditionalLightsRenderingMode").intValue =
            (int)LightRenderingMode.PerPixel;
        pipelineConfig.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(pipeline, "Assets/Generated/FanPipeline.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.globalTextureMipmapLimit = 0;

        BuildMaterials();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(
            "Assets/IndustrialFan/industrial-fan.fbx");
        importer.importAnimation = false;
        importer.bakeAxisConversion = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject fan = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IndustrialFan/industrial-fan.fbx"));
        fan.name = "industrial-fan";
        Dictionary<string, Material> materials = Directory.GetFiles("Assets/Generated", "*.mat")
            .Select(path => AssetDatabase.LoadAssetAtPath<Material>(path))
            .Where(material => material)
            .ToDictionary(material => Key(material.name), material => material);
        Debug.Log("FAN_MATERIAL_KEYS " + string.Join(",", materials.Keys.OrderBy(key => key)));
        foreach (Renderer item in fan.GetComponentsInChildren<Renderer>())
        {
            item.sharedMaterials = item.sharedMaterials.Select(source =>
            {
                if (!materials.TryGetValue(Key(source.name), out Material result))
                    throw new InvalidOperationException("Unmapped fan material: " + source.name);
                return result;
            }).ToArray();
            Mesh mesh = item is SkinnedMeshRenderer skinned
                ? skinned.sharedMesh
                : item.GetComponent<MeshFilter>()?.sharedMesh;
            if (!mesh) throw new InvalidOperationException("Renderer has no mesh: " + item.name);
            MeshCollider collider = item.GetComponent<MeshCollider>();
            if (!collider) collider = item.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            item.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }
        Bounds bounds = BoundsOf(fan);
        ValidateManifest(fan);
        ExportMotionGeometry(fan);
        Debug.Log("FAN_IMPORT_BOUNDS " + bounds + " MESHES " +
            fan.GetComponentsInChildren<MeshFilter>().Length);
        if (Mathf.Abs(bounds.size.x - 1.6f) > .03f ||
            Mathf.Abs(bounds.size.y - 1.8f) > .03f || Mathf.Abs(bounds.size.z - 1.6f) > .03f)
            throw new InvalidOperationException("Fan FBX size/axis check failed: " + bounds);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Studio floor";
        UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.position = new Vector3(0, -.025f, 0);
        floor.transform.localScale = new Vector3(200, .05f, 200);
        Material floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        floorMat.SetColor("_BaseColor", new Color(.19f, .20f, .20f));
        floorMat.SetFloat("_Smoothness", 0);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        LightAt("Key", new Vector3(48, -35, 0), 1.5f, new Color(1, .98f, .95f), true);
        LightAt("Fill", new Vector3(25, 125, 0), .28f, new Color(.94f, .97f, 1), false);
        LightAt("Rim", new Vector3(65, 200, 0), .42f, Color.white, false);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.30f, .31f, .31f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture =
            AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Textures/inspection-studio.exr");
        RenderSettings.reflectionIntensity = .40f;
        ReflectionProbe probe = new GameObject("Studio reflection", typeof(ReflectionProbe))
            .GetComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Custom;
        probe.customBakedTexture = RenderSettings.customReflectionTexture;
        probe.size = new Vector3(12, 8, 12);
        probe.center = new Vector3(0, 1, 0);
        probe.intensity = .40f;
        if (!probe.customBakedTexture)
            throw new InvalidOperationException("Studio reflection map did not import as cubemap.");

        Camera camera = new GameObject("Orbit camera", typeof(Camera), typeof(AudioListener))
            .GetComponent<Camera>();
        camera.fieldOfView = 34;
        camera.orthographic = true;
        camera.nearClipPlane = .025f;
        camera.farClipPlane = 100;
        camera.backgroundColor = new Color(.54f, .55f, .56f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        camera.allowDynamicResolution = false;
        camera.cullingMask &= ~(1 << 31);
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false;
        cameraData.antialiasing = AntialiasingMode.None;
        FanViewer viewer = new GameObject("Fan viewer", typeof(FanViewer), typeof(FanPartInspection))
            .GetComponent<FanViewer>();
        viewer.fan = fan.transform;
        viewer.viewCamera = camera;
        viewer.componentData = AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/IndustrialFan/parts.json");
        viewer.uiFont = AssetDatabase.LoadAssetAtPath<Font>(
            "Assets/Fonts/SourceHanSansSC-Regular.otf");
        viewer.selectionMaskShader = AssetDatabase.LoadAssetAtPath<Shader>(
            "Assets/Shaders/FanSelectionMask.shader");
        viewer.selectionOutlineShader = AssetDatabase.LoadAssetAtPath<Shader>(
            "Assets/Shaders/FanSelectionOutline.shader");
        if (!viewer.selectionMaskShader || !viewer.selectionOutlineShader)
            throw new InvalidOperationException("Fan selection shaders are missing.");

        ApplyStudioPresentation();
        EditorSceneManager.SaveScene(scene, "Assets/Generated/FanViewer.unity");
        PlayerSettings.companyName = "Workshop";
        PlayerSettings.productName = "Industrial Fan Viewer";
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,
            ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
            new[] { GraphicsDeviceType.Direct3D11 });
        AssetDatabase.SaveAssets();
        Debug.Log("FAN_REVIEW_SCENE_READY " + bounds);
    }

    static void ApplyStudioPresentation()
    {
        var env = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/inspection-studio.exr");
        var environmentSettings = new TextureImporterSettings();
        env.ReadTextureSettings(environmentSettings);
        environmentSettings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
        env.SetTextureSettings(environmentSettings);
        env.maxTextureSize = 2048;
        env.textureCompression = TextureImporterCompression.CompressedHQ;
        env.SaveAndReimport();

        Finishes finishes = JsonUtility.FromJson<Finishes>(
            File.ReadAllText("Assets/IndustrialFan/materials.json"));
        foreach (Finish finish in finishes.materials)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Generated/" + Key(finish.name) + ".mat");
            Vector4 color = new Vector4(finish.color[0], finish.color[1], finish.color[2], 1);
            switch (Key(finish.name))
            {
                case "fanbluegreyenamel": color = new Vector4(.038f, .069f, .086f, 1); break;
                case "fangraphitepowdercoat": color = new Vector4(.018f, .021f, .023f, 1); break;
                case "fansafetyyellow": color = new Vector4(.72f, .37f, .008f, 1); break;
            }
            material.SetVector("_BaseColor", color);
            EditorUtility.SetDirty(material);
        }

        Material floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Generated/StudioFloor.mat");
        if (!floorMaterial)
        {
            floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(floorMaterial, "Assets/Generated/StudioFloor.mat");
        }
        floorMaterial.SetVector("_BaseColor", new Vector4(.19f, .195f, .20f, 1));
        floorMaterial.SetFloat("_Smoothness", 0);
        floorMaterial.SetFloat("_SpecularHighlights", 0);
        floorMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        EditorUtility.SetDirty(floorMaterial);
        GameObject.Find("Studio floor").GetComponent<Renderer>().sharedMaterial = floorMaterial;
        GameObject.Find("Key").GetComponent<Light>().intensity = 1.65f;
        GameObject.Find("Fill").GetComponent<Light>().intensity = .25f;
        GameObject.Find("Rim").GetComponent<Light>().intensity = .40f;

        RenderSettings.ambientMode = AmbientMode.Custom;
        var ambient = new SphericalHarmonicsL2();
        ambient.AddAmbientLight(new Color(.20f, .21f, .22f));
        ambient.AddDirectionalLight(new Vector3(-.4f, .8f, -.3f).normalized, Color.white, .25f);
        RenderSettings.ambientProbe = ambient;
        RenderSettings.reflectionIntensity = .38f;
        ReflectionProbe probe = GameObject.Find("Studio reflection").GetComponent<ReflectionProbe>();
        probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(
            "Assets/Textures/inspection-studio.exr");
        RenderSettings.customReflectionTexture = probe.customBakedTexture;
        probe.size = new Vector3(30, 20, 30);
        probe.center = new Vector3(0, 2, 0);
        probe.intensity = .38f;
        probe.boxProjection = false;
        probe.blendDistance = 2;

        GameObject.Find("Orbit camera").GetComponent<Camera>()
            .GetUniversalAdditionalCameraData().renderPostProcessing = true;
        GameObject gradingObject = GameObject.Find("Studio grading");
        Volume volume = gradingObject ? gradingObject.GetComponent<Volume>() :
            new GameObject("Studio grading", typeof(Volume)).GetComponent<Volume>();
        volume.isGlobal = true;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Generated/StudioGrading.asset");
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, "Assets/Generated/StudioGrading.asset");
        }
        if (!profile.TryGet(out Tonemapping tone))
        {
            tone = profile.Add<Tonemapping>(true);
            AssetDatabase.AddObjectToAsset(tone, profile);
        }
        tone.mode.Override(TonemappingMode.ACES);
        if (!profile.TryGet(out ColorAdjustments grading))
        {
            grading = profile.Add<ColorAdjustments>(true);
            AssetDatabase.AddObjectToAsset(grading, profile);
        }
        grading.postExposure.Override(-.25f);
        grading.contrast.Override(5);
        EditorUtility.SetDirty(tone);
        EditorUtility.SetDirty(grading);
        EditorUtility.SetDirty(profile);
        volume.sharedProfile = profile;
        Debug.Log("FAN_STUDIO_PRESENTATION pump palette, ACES, exposure=-0.25, contrast=5, specular environment");
    }

    static void BuildMaterials()
    {
        Finishes finishes = JsonUtility.FromJson<Finishes>(
            File.ReadAllText("Assets/IndustrialFan/materials.json"));
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/FanFinish.shader");
        if (!shader) throw new InvalidOperationException("Fan finish shader missing.");
        foreach (Finish finish in finishes.materials)
        {
            Material material = new Material(shader) { name = finish.name };
            material.SetVector("_BaseColor",
                new Vector4(finish.color[0], finish.color[1], finish.color[2], 1));
            material.SetFloat("_Metallic", finish.metallic);
            material.SetFloat("_Smoothness", 1 - finish.roughness);
            material.SetFloat("_Grain", Mathf.Clamp(finish.relief * 220, .003f, .045f));
            material.SetFloat("_BumpScale", finish.family == "cast" ? .20f : .10f);
            if (!string.IsNullOrEmpty(finish.family))
            {
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Textures/" + finish.family + "_BaseColor.png"));
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Textures/" + finish.family + "_Normal.png"));
                material.SetTexture("_RoughMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Textures/" + finish.family + "_Roughness.png"));
            }
            string path = "Assets/Generated/" + Key(finish.name) + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
        }
    }

    static void ValidateManifest(GameObject fan)
    {
        FanViewer.Manifest manifest = JsonUtility.FromJson<FanViewer.Manifest>(
            File.ReadAllText("Assets/IndustrialFan/parts.json"));
        Transform[] nodes = fan.GetComponentsInChildren<Transform>(true);
        int moving = 0, meshes = 0;
        foreach (FanViewer.Component part in manifest.parts)
        {
            Transform group = nodes.Single(node => node.name == part.name);
            Renderer[] owned = group.GetComponentsInChildren<Renderer>();
            meshes += owned.Length;
            if (!owned.Any(r => FanPartInspection.NameKey(r.name) == FanPartInspection.NameKey(part.anchor)))
                throw new InvalidOperationException("Fan anchor missing after FBX import: " + part.anchor +
                    "; actual renderers: " + string.Join(", ", owned.Take(8).Select(r => r.name)));
            if (part.path.Length < 2 || part.path[0].t != 0 || part.path[0].offset != Vector3.zero ||
                part.path[part.path.Length - 1].t != 1)
                throw new InvalidOperationException("Invalid fan motion endpoints: " + part.name);
            for (int i = 1; i < part.path.Length; i++)
                if (part.path[i].t <= part.path[i - 1].t)
                    throw new InvalidOperationException("Unordered fan motion path: " + part.name);
            if (part.path.Any(key => key.offset.sqrMagnitude > .0001f)) moving++;
            if (string.IsNullOrWhiteSpace(part.description))
                throw new InvalidOperationException("Missing fan introduction: " + part.name);
        }
        if (meshes != manifest.meshCount || moving != manifest.parts.Length - 2)
            throw new InvalidOperationException("Fan ownership/motion check failed: meshes=" + meshes +
                ", moving=" + moving + ". Check JSON Vector3 object encoding.");
        Debug.Log("FAN_MANIFEST_VALIDATED assemblies=" + manifest.parts.Length +
            " anchors=" + manifest.parts.Length + " meshes=" + meshes + " moving=" + moving);
    }

    static void ConfigureTextures()
    {
        foreach (string path in Directory.GetFiles("Assets/Textures", "*.png"))
        {
            TextureImporter texture = (TextureImporter)AssetImporter.GetAtPath(path);
            texture.maxTextureSize = 4096;
            texture.mipmapEnabled = true;
            texture.anisoLevel = 16;
            texture.mipMapBias = -.25f;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.sRGBTexture = path.Contains("_BaseColor");
            if (path.Contains("_Normal")) texture.textureType = TextureImporterType.NormalMap;
            texture.SaveAndReimport();
        }
        TextureImporter env = (TextureImporter)AssetImporter.GetAtPath(
            "Assets/Textures/inspection-studio.exr");
        if (!env) throw new InvalidOperationException("Studio HDRI is missing.");
        env.textureShape = TextureImporterShape.TextureCube;
        env.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
        env.sRGBTexture = false;
        env.mipmapEnabled = true;
        env.SaveAndReimport();
    }

    static void ExportMotionGeometry(GameObject fan)
    {
        FanViewer.ResolveMotionBasis(fan.transform, out Vector3 x, out Vector3 y, out Vector3 z);
        FanViewer.Manifest manifest = JsonUtility.FromJson<FanViewer.Manifest>(
            File.ReadAllText("Assets/IndustrialFan/parts.json"));
        Transform[] nodes = fan.GetComponentsInChildren<Transform>(true);
        var meshes = new List<MotionMesh>();
        foreach (FanViewer.Component part in manifest.parts)
        foreach (Renderer renderer in nodes.Single(node => node.name == part.name)
            .GetComponentsInChildren<Renderer>())
        {
            Mesh mesh = renderer is SkinnedMeshRenderer skinned
                ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
            meshes.Add(new MotionMesh
            {
                name = renderer.name,
                part = part.index,
                vertices = mesh.vertices.Select(vertex =>
                    renderer.localToWorldMatrix.MultiplyPoint3x4(vertex)).ToArray(),
                triangles = mesh.triangles
            });
        }
        var geometry = new MotionGeometry
        {
            date = DateTimeOffset.Now.ToString("o"), motionVersion = manifest.motionVersion,
            axisX = x, axisY = y, axisZ = z, meshes = meshes.ToArray()
        };
        string output = Path.GetFullPath("../reports/industrial-fan-unity-motion-geometry.json");
        File.WriteAllText(output, JsonUtility.ToJson(geometry));
        Debug.Log("FAN_IMPORTED_MOTION_GEOMETRY " + output + " meshes=" + meshes.Count);
    }

    static Bounds BoundsOf(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer item in renderers) bounds.Encapsulate(item.bounds);
        return bounds;
    }

    static void LightAt(string name, Vector3 euler, float intensity, Color color, bool shadows)
    {
        Light light = new GameObject(name, typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.transform.rotation = Quaternion.Euler(euler);
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = .85f;
    }
}
