using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorkshopDemo.Editor
{
    public static class WorkshopReviewedFanImport
    {
        [Serializable] sealed class Finish
        {
            public string name, family;
            public float[] color;
            public float metallic, roughness, relief;
        }
        [Serializable] sealed class Finishes { public Finish[] materials; }
        static string Key(string name) => new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        [MenuItem("Tools/Workshop Demo/Import Reviewed Fan")]
        public static void Import()
        {
            const string target = "Assets/WorkshopDemo/Art/ReviewedFan";
            const string source = "model/FanViewerProject/Assets";
            const string resources = "Assets/WorkshopDemo/Resources/ReviewedEquipment";
            Directory.CreateDirectory(target + "/Textures");
            Directory.CreateDirectory(resources);
            File.Copy(source + "/IndustrialFan/industrial-fan.fbx", target + "/industrial-fan.fbx", true);
            File.Copy(source + "/IndustrialFan/parts.json", resources + "/fan-parts.json", true);
            File.Copy(source + "/Shaders/FanFinish.shader", target + "/FanFinish.shader", true);
            // The workshop uses clustered lighting, while the standalone studio uses Forward.
            string shaderPath = target + "/FanFinish.shader";
            File.WriteAllText(shaderPath, File.ReadAllText(shaderPath).Replace("#pragma multi_compile_fog",
                "#pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS\n" +
                "            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP\n" +
                "            #pragma multi_compile_fog"));
            File.Copy(source + "/Textures/inspection-studio.exr", resources + "/fan-reflection.exr", true);
            foreach (string file in Directory.GetFiles(source + "/Textures", "*.png"))
                File.Copy(file, target + "/Textures/" + Path.GetFileName(file), true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var reflection = (TextureImporter)AssetImporter.GetAtPath(resources + "/fan-reflection.exr");
            reflection.textureShape = TextureImporterShape.TextureCube;
            reflection.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            reflection.sRGBTexture = false;
            reflection.mipmapEnabled = true;
            reflection.SaveAndReimport();
            foreach (string path in Directory.GetFiles(target + "/Textures", "*.png"))
            {
                var texture = (TextureImporter)AssetImporter.GetAtPath(path);
                texture.maxTextureSize = 4096;
                texture.textureCompression = TextureImporterCompression.Uncompressed;
                texture.sRGBTexture = path.Contains("_BaseColor");
                texture.mipmapEnabled = true;
                texture.anisoLevel = 16;
                texture.mipMapBias = -.25f;
                if (path.Contains("_Normal")) texture.textureType = TextureImporterType.NormalMap;
                texture.SaveAndReimport();
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(target + "/industrial-fan.fbx");
            importer.importAnimation = importer.importLights = importer.importCameras = false;
            importer.bakeAxisConversion = true;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.importBlendShapes = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(target + "/FanFinish.shader");
            if (!shader || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Reviewed fan shader failed compilation.");
            var finishes = JsonUtility.FromJson<Finishes>(File.ReadAllText(source + "/IndustrialFan/materials.json"));
            var materials = finishes.materials.ToDictionary(f => Key(f.name), f =>
            {
                string path = target + "/" + Key(f.name) + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                mat.SetVector("_BaseColor", new Vector4(f.color[0], f.color[1], f.color[2], 1));
                mat.SetFloat("_Metallic", f.metallic);
                mat.SetFloat("_Smoothness", 1 - f.roughness);
                mat.SetFloat("_Grain", Mathf.Clamp(f.relief * 220, .003f, .045f));
                mat.SetFloat("_BumpScale", f.family == "cast" ? .20f : .10f);
                if (!string.IsNullOrEmpty(f.family))
                {
                    mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(target + "/Textures/" + f.family + "_BaseColor.png"));
                    mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(target + "/Textures/" + f.family + "_Normal.png"));
                    mat.SetTexture("_RoughMap", AssetDatabase.LoadAssetAtPath<Texture2D>(target + "/Textures/" + f.family + "_Roughness.png"));
                }
                EditorUtility.SetDirty(mat);
                return mat;
            });
            var model = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(target + "/industrial-fan.fbx"));
            try
            {
                model.name = "Reviewed industrial-fan";
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                    r.sharedMaterials = r.sharedMaterials.Select(m => materials[Key(m.name)]).ToArray();
                // The blade uses vertex shapes, without a skeleton. Keep a MeshFilter so the
                // platform's existing same-mesh fault cameras and measurements can inspect it.
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    r.gameObject.AddComponent<MeshFilter>().sharedMesh = r.sharedMesh;
                    var renderer = r.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = r.sharedMaterials;
                    UnityEngine.Object.DestroyImmediate(r);
                }
                int meshes = model.GetComponentsInChildren<MeshFilter>().Length;
                int triangles = model.GetComponentsInChildren<MeshFilter>()
                    .Sum(f => (int)f.sharedMesh.GetIndexCount(0) / 3 +
                        Enumerable.Range(1, f.sharedMesh.subMeshCount - 1)
                            .Sum(i => (int)f.sharedMesh.GetIndexCount(i) / 3));
                if (meshes != 590 || triangles != 98140)
                    throw new InvalidOperationException("Reviewed fan geometry changed: " + meshes + "/" + triangles);
                PrefabUtility.SaveAsPrefabAsset(model, resources + "/industrial-fan.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("REVIEWED_FAN_IMPORTED meshes=" + meshes + " triangles=" + triangles);
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }

        public static void ImportAndBuild()
        {
            Import();
            WorkshopPlatformBuild.Build();
        }
    }
}
