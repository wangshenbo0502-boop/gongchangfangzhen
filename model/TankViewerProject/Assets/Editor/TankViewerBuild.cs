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

public static class TankViewerBuild
{
    [Serializable] sealed class Finish
    {
        public string name, baseMap, normalMap, roughMap;
        public float[] color, emission;
        public float metallic, roughness, relief, transmission, emissionStrength;
    }
    [Serializable] sealed class Finishes { public Finish[] materials; }
    [Serializable] sealed class Part { public string name; public string[] meshes; }
    [Serializable] sealed class Parts { public Part[] parts; public int meshCount; }
    [Serializable] sealed class MotionMesh
    {
        public string name;
        public int part;
        public Vector3[] vertices;
        public int[] triangles;
    }
    [Serializable] sealed class MotionGeometry
    {
        public Vector3 axisX, axisY, axisZ;
        public MotionMesh[] meshes;
    }

    static string Key(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    static T Store<T>(T value,string path) where T:UnityEngine.Object
    {
        T old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(!old){AssetDatabase.CreateAsset(value,path);return value;}
        EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;
    }

    public static void Build()
    {
        Directory.CreateDirectory("Assets/Generated");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ModelImporter importer=(ModelImporter)AssetImporter.GetAtPath("Assets/Tank/silo.fbx");
        importer.importAnimation=false; importer.importCameras=false; importer.importLights=false;
        importer.bakeAxisConversion=true;
        importer.isReadable=true; importer.importNormals=ModelImporterNormals.Import;
        importer.importTangents=ModelImporterTangents.CalculateMikk;
        importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
        var envImporter=(TextureImporter)AssetImporter.GetAtPath("Assets/Textures/inspection-studio.exr");
        envImporter.textureShape=TextureImporterShape.TextureCube;
        envImporter.generateCubemap=TextureImporterGenerateCubemap.AutoCubemap;
        envImporter.sRGBTexture=false;envImporter.maxTextureSize=2048;
        var environmentSettings = new TextureImporterSettings();
        envImporter.ReadTextureSettings(environmentSettings);
        environmentSettings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
        envImporter.SetTextureSettings(environmentSettings);
        envImporter.mipmapEnabled=true;envImporter.SaveAndReimport();

        var finishes=JsonUtility.FromJson<Finishes>(File.ReadAllText("Assets/Tank/materials.json")).materials;
        var mapped=new Dictionary<string,Material>();
        Shader shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/TankFinish.shader");
        if (!shader || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("Tank finish shader did not compile.");
        foreach (var entry in finishes.SelectMany(f => new[] { f.baseMap, f.normalMap, f.roughMap })
                     .Where(path => !string.IsNullOrEmpty(path)).Distinct())
        {
            var texture = (TextureImporter)AssetImporter.GetAtPath(entry);
            bool normal = entry.EndsWith("_Normal.png");
            texture.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            texture.sRGBTexture = !normal && !entry.EndsWith("_Roughness.png");
            texture.maxTextureSize = 2048;
            texture.anisoLevel = 12;
            texture.mipmapEnabled = true;
            texture.textureCompression = TextureImporterCompression.CompressedHQ;
            texture.SaveAndReimport();
        }
        foreach(var f in finishes)
        {
            var m = f.transmission > .5f ? Glass(f) : new Material(shader) { name = f.name };
            if (f.transmission <= .5f)
            {
            var color = new Vector4(f.color[0], f.color[1], f.color[2], f.color[3]);
            if (f.name == "TK Shell") color = new Vector4(.073f, .112f, .133f, 1);
            if (f.name == "TK Machined") color = new Vector4(.53f, .56f, .59f, 1);
            m.SetVector("_BaseColor",color);
            m.SetFloat("_Metallic",f.metallic);m.SetFloat("_Smoothness",1-f.roughness);
            m.SetFloat("_Grain",Mathf.Clamp(f.relief * 170, 0, .012f));
            m.SetFloat("_BumpScale",string.IsNullOrEmpty(f.normalMap) ? 0 : .11f);
            m.SetFloat("_BaseMapStrength", f.name == "TK Shell" ? .24f : .30f);
            m.SetVector("_EmissionColor", new Vector4(f.emission[0], f.emission[1], f.emission[2], 1) * f.emissionStrength);
            foreach (var (property, path) in new[] { ("_BaseMap", f.baseMap), ("_BumpMap", f.normalMap), ("_RoughMap", f.roughMap) })
                if (!string.IsNullOrEmpty(path))
                    m.SetTexture(property, AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
            mapped[Key(f.name)]=Store(m,"Assets/Generated/"+Key(f.name)+".mat");
        }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        GameObject tank=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tank/silo.fbx"));
        tank.name="silo";
        foreach(var r in tank.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterials=r.sharedMaterials.Select(m=>mapped[Key(m.name)]).ToArray();
            r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
            var mesh = r.GetComponent<MeshFilter>();
            if (mesh) r.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
        }
        Bounds bounds=tank.GetComponentsInChildren<Renderer>()[0].bounds;
        foreach(var r in tank.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
        Debug.Log("TANK_FBX_BOUNDS "+bounds+" MESHES "+tank.GetComponentsInChildren<MeshFilter>().Length);
        var partData=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Tank/parts.json");
        var parts=JsonUtility.FromJson<Parts>(partData.text);
        if(tank.GetComponentsInChildren<MeshFilter>().Length!=parts.meshCount)
            throw new InvalidOperationException("FBX mesh count does not match parts manifest");
        foreach(var p in parts.parts)
            if(!tank.GetComponentsInChildren<Transform>(true).Any(t=>t.name==p.name))
                throw new InvalidOperationException("Missing assembly root "+p.name);
        ExportGeometry(tank.transform, parts);

        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Studio floor";
        UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.position=new Vector3(bounds.center.x,bounds.min.y-.025f,bounds.center.z);
        floor.transform.localScale=new Vector3(400,.05f,400);
        var floorMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        floorMat.SetVector("_BaseColor",new Vector4(.19f,.195f,.20f,1));floorMat.SetFloat("_Smoothness",0);
        floorMat.SetFloat("_SpecularHighlights", 0);
        floorMat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        floor.GetComponent<Renderer>().sharedMaterial=Store(floorMat,"Assets/Generated/StudioFloor.mat");
        LightAt("Key",new Vector3(42,28,0),1.65f,new Color(1,.98f,.95f),true);
        LightAt("Fill",new Vector3(22,-29,0),.55f,new Color(.94f,.97f,1),false);
        LightAt("Rim",new Vector3(65,200,0),.40f,Color.white,false);
        RenderSettings.ambientMode=AmbientMode.Custom;
        var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.20f,.21f,.22f));
        sh.AddDirectionalLight(new Vector3(-.4f,.8f,-.3f).normalized, Color.white,.25f);
        RenderSettings.ambientProbe=sh;
        RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture=AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Textures/inspection-studio.exr");
        RenderSettings.reflectionIntensity=.38f;
        Camera camera=new GameObject("Orbit camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
        camera.orthographic=true;camera.fieldOfView=34;camera.nearClipPlane=.03f;camera.farClipPlane=100;
        camera.backgroundColor=new Color(.43f,.46f,.47f);camera.clearFlags=CameraClearFlags.SolidColor;
        camera.allowHDR=true;camera.allowMSAA=true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var volume = new GameObject("Studio grading", typeof(Volume)).GetComponent<Volume>();
        volume.isGlobal = true;
        const string gradingPath = "Assets/Generated/StudioGrading.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(gradingPath);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, gradingPath);
        }
        if (!profile.TryGet(out Tonemapping tone))
        {
            tone = profile.Add<Tonemapping>(true);
            AssetDatabase.AddObjectToAsset(tone, profile);
        }
        if (!profile.TryGet(out ColorAdjustments grade))
        {
            grade = profile.Add<ColorAdjustments>(true);
            AssetDatabase.AddObjectToAsset(grade, profile);
        }
        tone.mode.Override(TonemappingMode.ACES);
        grade.postExposure.Override(-.25f);
        grade.contrast.Override(5);
        EditorUtility.SetDirty(tone);
        EditorUtility.SetDirty(grade);
        EditorUtility.SetDirty(profile);
        volume.sharedProfile = profile;
        var probe=new GameObject("Studio reflection",typeof(ReflectionProbe)).GetComponent<ReflectionProbe>();
        probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=RenderSettings.customReflectionTexture;
        probe.size=new Vector3(30,24,30);probe.center=bounds.center;probe.intensity=.38f;
        if(!probe.customBakedTexture)throw new InvalidOperationException("Studio reflection map missing");
        var viewer=new GameObject("Tank viewer",typeof(TankViewer)).GetComponent<TankViewer>();
        viewer.tank=tank.transform;viewer.viewCamera=camera;
        viewer.componentData=partData;
        viewer.uiFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SourceHanSansSC-Regular.otf");

        var rendererData=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Generated/TankRenderer.asset");
        if(!rendererData)
        {
            rendererData=ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name="Tank forward renderer";
            AssetDatabase.CreateAsset(rendererData,"Assets/Generated/TankRenderer.asset");
        }
        rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
        var clarity = AssetDatabase.LoadAllAssetsAtPath("Assets/Generated/TankRenderer.asset").OfType<TankClarity>().FirstOrDefault();
        if (!clarity)
        {
            clarity = ScriptableObject.CreateInstance<TankClarity>();
            clarity.name = "Tank fine detail clarity";
            AssetDatabase.AddObjectToAsset(clarity, rendererData);
        }
        if (!rendererData.rendererFeatures.Contains(clarity)) rendererData.rendererFeatures.Add(clarity);
        clarity.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/TankClarity.shader");
        EditorUtility.SetDirty(clarity);
        EditorUtility.SetDirty(rendererData);
        var pipeline=UniversalRenderPipelineAsset.Create(rendererData);
        pipeline.name="Tank studio quality";pipeline.msaaSampleCount=4;pipeline.renderScale=2f;
        pipeline.supportsHDR=true;pipeline.supportsCameraDepthTexture=true;pipeline.shadowDistance=25;
        pipeline.mainLightShadowmapResolution=4096;
        pipeline=Store(pipeline,"Assets/Generated/TankPipeline.asset");
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        QualitySettings.vSyncCount=1;QualitySettings.anisotropicFiltering=AnisotropicFiltering.ForceEnable;
        PlayerSettings.companyName="Workshop";PlayerSettings.productName="Vertical Tank Viewer";
        PlayerSettings.defaultScreenWidth=1920;PlayerSettings.defaultScreenHeight=1080;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
        PlayerSettings.runInBackground=true;PlayerSettings.SplashScreen.show=false;
        PlayerSettings.colorSpace=ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
        EditorSceneManager.SaveScene(scene,"Assets/Generated/TankViewer.unity");
        AssetDatabase.SaveAssets();
        string directory = Environment.GetEnvironmentVariable("WORKSHOP_TANK_BUILD_DIRECTORY") ?? "../TankViewer";
        string output=Path.GetFullPath(Path.Combine(directory, "TankViewer.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=new[]{"Assets/Generated/TankViewer.unity"},locationPathName=output,
            target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        File.WriteAllText(Path.GetFullPath("../reports/tank-viewer-build.txt"),
            DateTimeOffset.Now.ToString("O")+"\n"+report.summary.result+"\n"+report.summary.totalSize+
            " bytes\n"+report.summary.totalErrors+" errors\n"+output+"\n");
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Tank viewer build failed");
        Debug.Log("TANK_VIEWER_BUILD_SUCCEEDED "+output);
    }

    static Material Glass(Finish finish)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = finish.name };
        bool water = finish.name.Contains("Process Water");
        material.SetVector("_BaseColor", water ? new Vector4(.025f,.24f,.28f,.25f) :
            new Vector4(.018f,.027f,.030f,.105f));
        material.SetFloat("_Smoothness", water ? .84f : .92f);
        material.SetFloat("_Surface", 1);
        material.SetFloat("_Blend", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_BlendModePreserveSpecular", 0);
        material.SetFloat("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = water ? 2999 : 3000;
        material.SetShaderPassEnabled("ShadowCaster", false);
        return material;
    }

    static void ExportGeometry(Transform tank, Parts parts)
    {
        TankViewer.ResolveBasis(tank, out var x, out var y, out var z);
        var nodes = tank.GetComponentsInChildren<Transform>(true);
        var geometry = new MotionGeometry { axisX = x, axisY = y, axisZ = z,
            meshes = parts.parts.SelectMany((part, index) => nodes.Single(node => node.name == part.name)
                .GetComponentsInChildren<MeshFilter>().Select(filter => new MotionMesh {
                    name = filter.name, part = index,
                    vertices = filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray(),
                    triangles = filter.sharedMesh.triangles
                })).ToArray() };
        if (geometry.meshes.Length != parts.meshCount)
            throw new InvalidOperationException("Invalid imported tank assembly ownership.");
        File.WriteAllText(Path.GetFullPath("../reports/tank-unity-motion-geometry-20261113.json"),
            JsonUtility.ToJson(geometry));
    }
    static void LightAt(string name,Vector3 angles,float intensity,Color color,bool shadows)
    {
        var l=new GameObject(name,typeof(Light)).GetComponent<Light>();l.type=LightType.Directional;
        l.intensity=intensity;l.color=color;l.transform.rotation=Quaternion.Euler(angles);
        l.shadows=shadows?LightShadows.Soft:LightShadows.None;l.shadowStrength=.82f;
    }
}
