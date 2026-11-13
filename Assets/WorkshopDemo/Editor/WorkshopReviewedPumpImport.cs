using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorkshopDemo.Editor
{
    public static class WorkshopReviewedPumpImport
    {
        [Serializable] class Finish
        {
            public string name, family;
            public float[] color;
            public float metallic, roughness, relief;
        }
        [Serializable] class Finishes { public Finish[] materials; }
        static string Key(string name) => new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        public static void ImportAndBuild()
        {
            const string target = "Assets/WorkshopDemo/Art/ReviewedPump";
            const string source = "model/PumpViewerProject/Assets";
            Directory.CreateDirectory(target + "/Textures");
            Directory.CreateDirectory("Assets/WorkshopDemo/Resources/ReviewedEquipment");
            File.Copy(source + "/Pump/water-pump.fbx", target + "/water-pump.fbx", true);
            File.Copy(source + "/Pump/parts.json",
                "Assets/WorkshopDemo/Resources/ReviewedEquipment/pump-parts.json", true);
            File.Copy(source + "/Shaders/PumpFinish.shader", target + "/PumpFinish.shader", true);
            foreach (string file in Directory.GetFiles(source + "/Textures", "*.png"))
                File.Copy(file, target + "/Textures/" + Path.GetFileName(file), true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in Directory.GetFiles(target + "/Textures", "*.png"))
            {
                var texture = (TextureImporter)AssetImporter.GetAtPath(path);
                texture.maxTextureSize = 4096;
                texture.textureCompression = TextureImporterCompression.Uncompressed;
                texture.sRGBTexture = path.Contains("_BaseColor");
                texture.anisoLevel = 16;
                if (path.Contains("_Normal")) texture.textureType = TextureImporterType.NormalMap;
                texture.SaveAndReimport();
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(target + "/water-pump.fbx");
            importer.importAnimation = importer.importLights = importer.importCameras = false;
            importer.bakeAxisConversion = true;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(target + "/PumpFinish.shader");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Reviewed pump shader");
            var finishes = JsonUtility.FromJson<Finishes>(File.ReadAllText(source + "/Pump/materials.json"));
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
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(target + "/water-pump.fbx"));
            model.name = "Reviewed water-pump";
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(m => materials[Key(m.name)]).ToArray();
            PrefabUtility.SaveAsPrefabAsset(model,
                "Assets/WorkshopDemo/Resources/ReviewedEquipment/water-pump.prefab");
            UnityEngine.Object.DestroyImmediate(model);
            AssetDatabase.SaveAssets();
            WorkshopPlatformBuild.Build();
        }
    }
}
