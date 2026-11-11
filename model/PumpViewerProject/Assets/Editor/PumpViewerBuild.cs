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

public static class PumpViewerBuild
{
    [Serializable] class Finish
    {
        public string name, family;
        public float[] color;
        public float metallic, roughness, relief;
    }
    [Serializable] class Finishes { public Finish[] materials; }
    static string Key(string name) { return new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant(); }
    static T Store<T>(T asset,string path) where T : UnityEngine.Object
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!existing) { AssetDatabase.CreateAsset(asset,path); return asset; }
        EditorUtility.CopySerialized(asset,existing);
        UnityEngine.Object.DestroyImmediate(asset);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    public static void Build()
    {
        Directory.CreateDirectory("Assets/Generated");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextures();
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Generated/PumpRenderer.asset");
        if (!renderer)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = "Pump forward renderer";
            AssetDatabase.CreateAsset(renderer,"Assets/Generated/PumpRenderer.asset");
        }
        var ao = AssetDatabase.LoadAllAssetsAtPath("Assets/Generated/PumpRenderer.asset").OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
        if (!ao)
        {
            ao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            AssetDatabase.AddObjectToAsset(ao,renderer);
        }
        ao.name = "Contact occlusion";
        var aoConfig = new SerializedObject(ao);
        aoConfig.FindProperty("m_Settings.Intensity").floatValue = .40f;
        aoConfig.FindProperty("m_Settings.Radius").floatValue = .10f;
        aoConfig.FindProperty("m_Settings.Downsample").boolValue = false;
        aoConfig.ApplyModifiedPropertiesWithoutUndo();
        renderer.rendererFeatures.Clear();
        renderer.rendererFeatures.Add(ao);
        var clarity = AssetDatabase.LoadAllAssetsAtPath("Assets/Generated/PumpRenderer.asset")
            .OfType<PumpClarity>().FirstOrDefault();
        if (!clarity)
        {
            clarity = ScriptableObject.CreateInstance<PumpClarity>();
            AssetDatabase.AddObjectToAsset(clarity,renderer);
        }
        clarity.name = "Fine detail clarity";
        clarity.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/PumpClarity.shader");
        if (!clarity.shader || ShaderUtil.ShaderHasError(clarity.shader))
            throw new InvalidOperationException("Pump clarity shader failed compilation");
        clarity.Create();
        EditorUtility.SetDirty(clarity);
        renderer.rendererFeatures.Add(clarity);
        renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
        EditorUtility.SetDirty(renderer);
        var pipeline = UniversalRenderPipelineAsset.Create(renderer);
        pipeline.name = "Pump studio quality";
        pipeline.msaaSampleCount = 4;
        pipeline.renderScale = 2f;
        pipeline.supportsHDR = true;
        pipeline.supportsCameraDepthTexture = true;
        pipeline.shadowDistance = 12;
        pipeline.shadowCascadeCount = 2;
        pipeline.mainLightShadowmapResolution = 4096;
        var pipelineConfig = new SerializedObject(pipeline);
        pipelineConfig.FindProperty("m_SoftShadowsSupported").boolValue = true;
        pipelineConfig.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
        pipelineConfig.ApplyModifiedPropertiesWithoutUndo();
        pipeline = Store(pipeline,"Assets/Generated/PumpPipeline.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        QualitySettings.vSyncCount = 1;
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.globalTextureMipmapLimit = 0;

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/PumpFinish.shader");
        if (!shader) throw new InvalidOperationException("Missing pump shader");
        Finish[] finishes = JsonUtility.FromJson<Finishes>(File.ReadAllText("Assets/Pump/materials.json")).materials;
        var materials = new Dictionary<string,Material>();
        foreach (Finish finish in finishes)
        {
            var mat = new Material(shader) { name = finish.name };
            // Source values are scene-linear, as are the shader's uniform values.
            mat.SetVector("_BaseColor",new Vector4(finish.color[0],finish.color[1],finish.color[2],1));
            mat.SetFloat("_Metallic",finish.metallic);
            mat.SetFloat("_Smoothness",1-finish.roughness);
            mat.SetFloat("_Grain",Mathf.Clamp(finish.relief*220,.003f,.045f));
            mat.SetFloat("_BumpScale",finish.family == "cast" ? .20f : .10f);
            if (!string.IsNullOrEmpty(finish.family))
            {
                mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/"+finish.family+"_BaseColor.png"));
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/"+finish.family+"_Normal.png"));
                mat.SetTexture("_RoughMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/"+finish.family+"_Roughness.png"));
            }
            mat = Store(mat,"Assets/Generated/"+Key(finish.name)+".mat");
            materials.Add(Key(finish.name),mat);
        }
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Pump/water-pump.fbx");
        importer.importAnimation = false;
        importer.bakeAxisConversion = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        GameObject pump = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Pump/water-pump.fbx"));
        pump.name = "water-pump";
        foreach (Renderer r in pump.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterials = r.sharedMaterials.Select(m =>
            {
                if (!materials.TryGetValue(Key(m.name),out Material result))
                    throw new InvalidOperationException("Unmapped material: "+m.name);
                return result;
            }).ToArray();
            r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }
        Bounds bounds = pump.GetComponentsInChildren<Renderer>()[0].bounds;
        foreach (Renderer r in pump.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
        Debug.Log("PUMP_IMPORT_BOUNDS "+bounds+" MESHES "+pump.GetComponentsInChildren<MeshFilter>().Length);
        if (Mathf.Abs(bounds.size.y-1.35f) > .02f || Mathf.Abs(bounds.size.x-1.9f) > .02f)
            throw new InvalidOperationException("Pump FBX unit/axis check failed: "+bounds);
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Studio floor";
        UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.position = new Vector3(0,-.022f,0);
        floor.transform.localScale = new Vector3(200,.04f,200);
        var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        floorMat.SetVector("_BaseColor",new Vector4(.19f,.195f,.20f,1));
        floorMat.SetFloat("_Smoothness",0);
        floorMat.SetFloat("_SpecularHighlights",0);
        floorMat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        floorMat = Store(floorMat,"Assets/Generated/StudioFloor.mat");
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        LightAt("Key",new Vector3(48,-35,0),1.65f,new Color(1,.98f,.95f),true);
        LightAt("Fill",new Vector3(25,125,0),.25f,new Color(.94f,.97f,1),false);
        LightAt("Rim",new Vector3(65,200,0),.40f,Color.white,false);
        RenderSettings.ambientMode = AmbientMode.Custom;
        var ambient = new SphericalHarmonicsL2();
        ambient.AddAmbientLight(new Color(.20f,.21f,.22f));
        ambient.AddDirectionalLight(new Vector3(-.4f,.8f,-.3f).normalized,Color.white,.25f);
        RenderSettings.ambientProbe = ambient;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Textures/inspection-studio.exr");
        RenderSettings.reflectionIntensity = .38f;
        ReflectionProbe probe = new GameObject("Studio reflection",typeof(ReflectionProbe)).GetComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Custom;
        probe.customBakedTexture = RenderSettings.customReflectionTexture;
        probe.size = new Vector3(30,20,30);
        probe.center = new Vector3(0,2,0);
        probe.intensity = .38f;
        probe.boxProjection = false;
        probe.blendDistance = 2;
        if (!probe.customBakedTexture) throw new InvalidOperationException("Studio cubemap missing");
        Camera camera = new GameObject("Orbit camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
        camera.fieldOfView = 34;
        camera.orthographic = true;
        camera.nearClipPlane = .025f; camera.farClipPlane = 250;
        camera.backgroundColor = new Color(.54f,.55f,.56f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.allowHDR = true; camera.allowMSAA = true;
        camera.allowDynamicResolution = false;
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.None;
        Volume volume = new GameObject("Studio grading",typeof(Volume)).GetComponent<Volume>();
        volume.isGlobal = true;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Generated/StudioGrading.asset");
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile,"Assets/Generated/StudioGrading.asset");
        }
        foreach (VolumeComponent old in profile.components) UnityEngine.Object.DestroyImmediate(old,true);
        profile.components.Clear();
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);
        var grading = profile.Add<ColorAdjustments>(true);
        grading.postExposure.Override(-.25f);
        grading.contrast.Override(5);
        AssetDatabase.AddObjectToAsset(tone,profile);
        AssetDatabase.AddObjectToAsset(grading,profile);
        EditorUtility.SetDirty(profile);
        volume.sharedProfile = profile;
        PumpViewer viewer = new GameObject("Pump viewer",typeof(PumpViewer)).GetComponent<PumpViewer>();
        viewer.pump = pump.transform; viewer.viewCamera = camera;
        viewer.componentData = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Pump/parts.json");
        viewer.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SourceHanSansSC-Regular.otf");
        EditorSceneManager.SaveScene(scene,"Assets/Generated/PumpViewer.unity");
        PlayerSettings.companyName = "Workshop";
        PlayerSettings.productName = "Water Pump Viewer";
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
        AssetDatabase.SaveAssets();
        string output = Path.GetFullPath("../WaterPumpViewer/WaterPumpViewer.exe");
        var options = new BuildPlayerOptions
        {
            scenes = new[]{"Assets/Generated/PumpViewer.unity"},
            locationPathName = output, target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        File.WriteAllText(Path.GetFullPath("../reports/pump-viewer-build.txt"),
            DateTimeOffset.Now.ToString("O")+"\n"+report.summary.result+"\n"+
            report.summary.totalSize+" bytes\n"+report.summary.totalErrors+" errors\n"+output+"\n");
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Viewer build failed");
        Debug.Log("PUMP_VIEWER_BUILD_SUCCEEDED "+output);
    }

    static void ConfigureTextures()
    {
        foreach (string path in Directory.GetFiles("Assets/Textures","*.png"))
        {
            TextureImporter t = (TextureImporter)AssetImporter.GetAtPath(path);
            t.maxTextureSize = 4096; t.mipmapEnabled = true; t.anisoLevel = 16;
            t.mipMapBias = -.25f;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.sRGBTexture = path.Contains("_BaseColor");
            if (path.Contains("_Normal")) t.textureType = TextureImporterType.NormalMap;
            t.SaveAndReimport();
        }
        var env = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/inspection-studio.exr");
        if (!env) throw new InvalidOperationException("Run bake_pump_studio.py before building");
        env.textureShape = TextureImporterShape.TextureCube;
        env.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
        var environmentSettings = new TextureImporterSettings();
        env.ReadTextureSettings(environmentSettings);
        environmentSettings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
        env.SetTextureSettings(environmentSettings);
        env.sRGBTexture = false;
        env.maxTextureSize = 2048;
        env.textureCompression = TextureImporterCompression.CompressedHQ;
        env.mipmapEnabled = true;
        env.SaveAndReimport();
    }
    static void LightAt(string name,Vector3 euler,float intensity,Color color,bool shadows)
    {
        Light light = new GameObject(name,typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = intensity; light.color = color;
        light.transform.rotation = Quaternion.Euler(euler);
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        light.shadowBias = .025f; light.shadowNormalBias = .04f;
        light.shadowStrength = .85f;
        light.GetUniversalAdditionalLightData().softShadowQuality = SoftShadowQuality.High;
    }
}
