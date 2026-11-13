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
        public string name, family, baseMap, normalMap, roughMap, metalMap, emissionMap;
        public float[] color, emissionColor;
        public float metallic, roughness, roughScale, relief, repeat, normalStrength, grain;
        public float transmission, emissionStrength;
        public int uvChannel;
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
        EditorSceneManager.OpenScene("Assets/Generated/CabinetViewer.unity");
        BuildPlayer();
    }

    public static void BuildWindowsStudio()
    {
        EditorSceneManager.OpenScene("Assets/Generated/CabinetViewer.unity");
        ConfigureTextures();
        ApplyCabinetStudioPresentation();
        EditorSceneManager.SaveScene(
            EditorSceneManager.GetActiveScene(), "Assets/Generated/CabinetViewer.unity");
        AssetDatabase.SaveAssets();
        BuildPlayer();
    }

    static void BuildPlayer()
    {
        PlayerSettings.enableFrameTimingStats = true;
        string candidate = Environment.GetEnvironmentVariable("WORKSHOP_CABINET_BUILD_DIRECTORY");
        string output = Path.GetFullPath(Path.Combine(
            string.IsNullOrWhiteSpace(candidate) ? "../CabinetViewer" : candidate, "CabinetViewer.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Generated/CabinetViewer.unity", true)
        };
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Generated/CabinetViewer.unity" },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Fan Windows build failed: " + report.summary.result);
        Debug.Log("CABINET_WINDOWS_BUILD " + output + " bytes=" + report.summary.totalSize);
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
            "Assets/ElectricalCabinet/electrical-cabinet.fbx");
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
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ElectricalCabinet/electrical-cabinet.fbx"));
        fan.name = "electrical-cabinet";
        Dictionary<string, Material> materials = Directory.GetFiles("Assets/Generated", "*.mat")
            .Select(path => AssetDatabase.LoadAssetAtPath<Material>(path))
            .Where(material => material)
            .ToDictionary(material => Key(material.name), material => material);
        Debug.Log("CABINET_MATERIAL_KEYS " + string.Join(",", materials.Keys.OrderBy(key => key)));
        foreach (Renderer item in fan.GetComponentsInChildren<Renderer>())
        {
            item.sharedMaterials = item.sharedMaterials.Select(source =>
            {
                if (!materials.TryGetValue(Key(source.name), out Material result))
                    throw new InvalidOperationException("Unmapped cabinet material: " + source.name);
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
        Directory.CreateDirectory("Assets/Generated/Materials/Harness");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var harnessMaterials = new Dictionary<Material, Material>();
        Transform harness = fan.GetComponentsInChildren<Transform>()
            .Single(node => node.name == "CAB_07_Wiring");
        foreach (Renderer item in harness.GetComponentsInChildren<Renderer>())
            item.sharedMaterials = item.sharedMaterials.Select(material =>
            {
                if (harnessMaterials.TryGetValue(material, out Material existing))
                    return existing;
                string path = "Assets/Generated/Materials/Harness/" + Key(material.name) + ".mat";
                Material isolated = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (isolated)
                    isolated.CopyPropertiesFromMaterial(material);
                else
                {
                    isolated = new Material(material) { name = material.name + " (harness)" };
                    AssetDatabase.CreateAsset(isolated, path);
                }
                EditorUtility.SetDirty(isolated);
                harnessMaterials.Add(material, isolated);
                return isolated;
            }).ToArray();
        Debug.Log("CABINET_IMPORT_BOUNDS " + bounds + " MESHES " +
            fan.GetComponentsInChildren<MeshFilter>().Length);
        if (Mathf.Abs(bounds.size.x - 1.208147f) > .003f ||
            Mathf.Abs(bounds.size.y - 2f) > .003f || Mathf.Abs(bounds.size.z - .655f) > .003f)
            throw new InvalidOperationException("Cabinet FBX size/axis check failed: " + bounds);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Studio floor";
        UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.position = new Vector3(0, -.025f, 0);
        floor.transform.localScale = new Vector3(200, .05f, 200);
        Material floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        floorMat.SetColor("_BaseColor", new Color(.19f, .20f, .20f));
        floorMat.SetFloat("_Smoothness", 0);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        LightAt("Key", new Vector3(48, -35, 0), .95f, new Color(1, .98f, .95f), true);
        LightAt("Fill", new Vector3(25, 125, 0), .28f, new Color(.94f, .97f, 1), false);
        LightAt("Rim", new Vector3(65, 200, 0), .42f, Color.white, false);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.24f, .25f, .25f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture =
            AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Textures/inspection-studio.exr");
        RenderSettings.reflectionIntensity = .72f;
        ReflectionProbe probe = new GameObject("Studio reflection", typeof(ReflectionProbe))
            .GetComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Custom;
        probe.customBakedTexture = RenderSettings.customReflectionTexture;
        probe.size = new Vector3(12, 8, 12);
        probe.center = new Vector3(0, 1, 0);
        probe.intensity = .72f;
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
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.None;
        var volume = new GameObject("Studio tone mapping", typeof(Volume)).GetComponent<Volume>();
        volume.isGlobal = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var tone = profile.Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.ACES);
        AssetDatabase.CreateAsset(profile, "Assets/Generated/CabinetGrading.asset");
        volume.sharedProfile = profile;
        FanViewer viewer = new GameObject("Cabinet viewer", typeof(FanViewer), typeof(FanPartInspection))
            .GetComponent<FanViewer>();
        viewer.fan = fan.transform;
        viewer.viewCamera = camera;
        viewer.componentData = AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/ElectricalCabinet/parts.json");
        viewer.uiFont = AssetDatabase.LoadAssetAtPath<Font>(
            "Assets/Fonts/SourceHanSansSC-Regular.otf");
        viewer.selectionMaskShader = AssetDatabase.LoadAssetAtPath<Shader>(
            "Assets/Shaders/FanSelectionMask.shader");
        viewer.selectionOutlineShader = AssetDatabase.LoadAssetAtPath<Shader>(
            "Assets/Shaders/FanSelectionOutline.shader");
        if (!viewer.selectionMaskShader || !viewer.selectionOutlineShader)
            throw new InvalidOperationException("Fan selection shaders are missing.");

        ApplyCabinetStudioPresentation();
        EditorSceneManager.SaveScene(scene, "Assets/Generated/CabinetViewer.unity");
        PlayerSettings.companyName = "Workshop";
        PlayerSettings.productName = "Electrical Cabinet Viewer";
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
        Debug.Log("CABINET_REVIEW_SCENE_READY " + bounds);
    }

    static void BuildMaterials()
    {
        Finishes finishes = JsonUtility.FromJson<Finishes>(
            File.ReadAllText("Assets/ElectricalCabinet/materials.json"));
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/FanFinish.shader");
        if (!shader) throw new InvalidOperationException("Fan finish shader missing.");
        foreach (Finish finish in finishes.materials)
        {
            Material material = new Material(shader) { name = finish.name };
            material.SetVector("_BaseColor",
                new Vector4(finish.color[0], finish.color[1], finish.color[2], 1));
            material.SetFloat("_Metallic", finish.metallic);
            material.SetFloat("_RoughBias", finish.roughness);
            material.SetFloat("_RoughScale", finish.roughScale);
            material.SetFloat("_Grain", finish.grain);
            material.SetFloat("_BumpScale", finish.normalStrength);
            material.SetFloat("_UVChannel", finish.uvChannel);
            material.SetFloat("_Repeat", finish.repeat);
            foreach (var mapping in new[] {
                ("_BaseMap", finish.baseMap), ("_BumpMap", finish.normalMap),
                ("_RoughMap", finish.roughMap), ("_MetalMap", finish.metalMap),
                ("_EmissionMap", finish.emissionMap) })
            {
                if (string.IsNullOrEmpty(mapping.Item2)) continue;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(mapping.Item2);
                if (!texture) throw new InvalidOperationException("Missing source texture: " + mapping.Item2);
                material.SetTexture(mapping.Item1, texture);
            }
            material.SetVector("_EmissionColor", new Vector4(finish.emissionColor[0],
                finish.emissionColor[1], finish.emissionColor[2], 1) * finish.emissionStrength);
            if (finish.transmission > .5f)
            {
                material.shader = Shader.Find("Universal Render Pipeline/Lit");
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_BlendModePreserveSpecular", 0);
                material.SetFloat("_ZWrite", 0);
                material.SetFloat("_Smoothness", .88f);
                bool hmi = finish.name == "CAB / HMI cover glass";
                material.SetVector("_BaseColor", new Vector4(.012f, .018f, .021f, hmi ? .055f : .115f));
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
            string path = "Assets/Generated/" + Key(finish.name) + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
        }
    }

    static void ApplyCabinetStudioPresentation()
    {
        foreach (string materialName in new[] { "cabpaint", "cabsteel", "cabblue" })
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Generated/" + materialName + ".mat");
            if (!material) throw new InvalidOperationException("Missing cabinet finish: " + materialName);
            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ElectricalCabinet/SourceTextures/powder_BaseColor.png");
            Texture2D roughMap = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ElectricalCabinet/SourceTextures/powder_Roughness.png");
            if (!baseMap || !roughMap)
                throw new InvalidOperationException("Powder-coat base/roughness texture missing.");

            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_RoughMap", roughMap);
            material.SetVector("_BaseColor", materialName == "cabblue"
                ? new Vector4(.022f, .026f, .031f, 1)
                : new Vector4(.087f, .110f, .125f, 1));
            material.SetFloat("_BaseMapStrength", .22f);
            material.SetTexture("_BumpMap", null);
            material.SetTexture("_MetalMap", null);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_RoughBias", .38f);
            material.SetFloat("_RoughScale", .19f);
            material.SetFloat("_BumpScale", 0);
            material.SetFloat("_Grain", 0);
            EditorUtility.SetDirty(material);
        }

        Material display = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/cabhminormal.mat");
        display.SetVector("_BaseColor", new Vector4(.10f, .10f, .10f, 1));
        display.SetVector("_EmissionColor", new Vector4(.82f, .82f, .82f, 1));
        EditorUtility.SetDirty(display);

        TextureImporter env = (TextureImporter)AssetImporter.GetAtPath(
            "Assets/Textures/inspection-studio.exr");
        if (!env) throw new InvalidOperationException("Studio HDRI is missing.");
        var environmentSettings = new TextureImporterSettings();
        env.ReadTextureSettings(environmentSettings);
        environmentSettings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
        env.SetTextureSettings(environmentSettings);
        env.maxTextureSize = 2048;
        env.textureCompression = TextureImporterCompression.CompressedHQ;
        env.textureShape = TextureImporterShape.TextureCube;
        env.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
        env.sRGBTexture = false;
        env.mipmapEnabled = true;
        env.SaveAndReimport();

        Light key = GameObject.Find("Key").GetComponent<Light>();
        key.intensity = 1.65f;
        key.transform.rotation = Quaternion.Euler(42, 28, 0);
        Light fill = GameObject.Find("Fill").GetComponent<Light>();
        fill.intensity = .42f;
        fill.transform.rotation = Quaternion.Euler(24, -64, 0);
        GameObject.Find("Rim").GetComponent<Light>().intensity = .40f;
        RenderSettings.ambientMode = AmbientMode.Custom;
        var ambient = new SphericalHarmonicsL2();
        ambient.AddAmbientLight(new Color(.20f, .21f, .22f));
        ambient.AddDirectionalLight(
            new Vector3(-.4f, .8f, -.3f).normalized, Color.white, .25f);
        RenderSettings.ambientProbe = ambient;
        RenderSettings.reflectionIntensity = .38f;

        ReflectionProbe probe = GameObject.Find("Studio reflection").GetComponent<ReflectionProbe>();
        probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(
            "Assets/Textures/inspection-studio.exr");
        probe.size = new Vector3(30, 20, 30);
        probe.center = new Vector3(0, 2, 0);
        probe.intensity = .38f;
        probe.boxProjection = false;
        probe.blendDistance = 2;
        RenderSettings.customReflectionTexture = probe.customBakedTexture;
        if (!probe.customBakedTexture)
            throw new InvalidOperationException("Studio reflection map did not import as cubemap.");

        Material floor = GameObject.Find("Studio floor").GetComponent<Renderer>().sharedMaterial;
        floor.SetVector("_BaseColor", new Vector4(.19f, .195f, .20f, 1));
        floor.SetFloat("_Smoothness", 0);
        floor.SetFloat("_SpecularHighlights", 0);
        floor.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        EditorUtility.SetDirty(floor);
        Camera camera = GameObject.Find("Orbit camera").GetComponent<Camera>();
        camera.backgroundColor = new Color(.54f, .55f, .56f);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        Volume volume = GameObject.Find("Studio tone mapping").GetComponent<Volume>();
        VolumeProfile profile = volume.sharedProfile;
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
        Debug.Log("CABINET_STUDIO_PRESENTATION aligned with pump/fan; powder base+roughness maps, " +
            "no false metallic/diagonal normal, ACES exposure=-0.25 contrast=5");
    }

    static void ValidateManifest(GameObject fan)
    {
        FanViewer.Manifest manifest = JsonUtility.FromJson<FanViewer.Manifest>(
            File.ReadAllText("Assets/ElectricalCabinet/parts.json"));
        Transform[] nodes = fan.GetComponentsInChildren<Transform>(true);
        int moving = 0, meshes = 0;
        foreach (FanViewer.Component part in manifest.parts)
        {
            Transform group = nodes.Single(node => node.name == part.name);
            Renderer[] owned = group.GetComponentsInChildren<Renderer>();
            meshes += owned.Length;
            if (!owned.Any(r => FanPartInspection.NameKey(r.name) == FanPartInspection.NameKey(part.anchor)))
                throw new InvalidOperationException("Cabinet anchor missing after FBX import: " + part.anchor +
                    "; actual renderers: " + string.Join(", ", owned.Take(8).Select(r => r.name)));
            if (part.path.Length < 2 || part.path[0].t != 0 || part.path[0].offset != Vector3.zero ||
                part.path[part.path.Length - 1].t != 1)
                throw new InvalidOperationException("Invalid cabinet motion endpoints: " + part.name);
            for (int i = 1; i < part.path.Length; i++)
                if (part.path[i].t <= part.path[i - 1].t)
                    throw new InvalidOperationException("Unordered cabinet motion path: " + part.name);
            if (part.doorAttached || part.path.Any(key => key.offset.sqrMagnitude > .0001f))
                moving++;
            if (string.IsNullOrWhiteSpace(part.description))
                throw new InvalidOperationException("Missing cabinet introduction: " + part.name);
        }
        int expectedMoving = manifest.parts.Count(part =>
            part.name != "CAB_00_Enclosure" &&
            part.name != "CAB_01_Backplate" &&
            part.name != "CAB_10_PlinthGlands");
        if (meshes != manifest.meshCount || moving != expectedMoving)
            throw new InvalidOperationException("Cabinet ownership/motion check failed: meshes=" + meshes +
                ", moving=" + moving + ", expected=" + expectedMoving +
                ". Check JSON Vector3 object encoding.");
        Debug.Log("CABINET_MANIFEST_VALIDATED assemblies=" + manifest.parts.Length +
            " anchors=" + manifest.parts.Length + " meshes=" + meshes + " moving=" + moving);
    }

    static void ConfigureTextures()
    {
        foreach (string path in Directory.GetFiles("Assets/ElectricalCabinet/SourceTextures")
            .Where(path => new[] { ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".exr" }
                .Contains(Path.GetExtension(path).ToLowerInvariant())))
        {
            TextureImporter texture = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!texture) throw new InvalidOperationException("Texture importer missing: " + path);
            texture.maxTextureSize = 4096;
            texture.mipmapEnabled = true;
            texture.anisoLevel = 16;
            bool powderFinish = Path.GetFileName(path).StartsWith(
                "powder_", StringComparison.OrdinalIgnoreCase);
            texture.mipMapBias = powderFinish ? 0f : -.25f;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            TextureImporterPlatformSettings standalone = texture.GetPlatformTextureSettings("Standalone");
            standalone.name = "Standalone";
            standalone.overridden = true;
            standalone.maxTextureSize = 4096;
            standalone.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
            standalone.format = TextureImporterFormat.RGBA32;
            standalone.textureCompression = TextureImporterCompression.Uncompressed;
            texture.SetPlatformTextureSettings(standalone);
            string file = Path.GetFileNameWithoutExtension(path);
            bool normal = file.EndsWith("_Normal", StringComparison.OrdinalIgnoreCase);
            bool linearData = normal || file.EndsWith("_Roughness", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith("_Metallic", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith("_Height", StringComparison.OrdinalIgnoreCase);
            texture.sRGBTexture = !linearData && !file.Equals("studio", StringComparison.OrdinalIgnoreCase);
            texture.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
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
            File.ReadAllText("Assets/ElectricalCabinet/parts.json"));
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
        string output = Path.GetFullPath("../reports/electrical-cabinet-unity-motion-geometry.json");
        File.WriteAllText(output, JsonUtility.ToJson(geometry));
        Debug.Log("CABINET_IMPORTED_MOTION_GEOMETRY " + output + " meshes=" + meshes.Count);
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
