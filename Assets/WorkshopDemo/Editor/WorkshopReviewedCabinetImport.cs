using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WorkshopDemo.Editor
{
    public static class WorkshopReviewedCabinetImport
    {
        const string Source = "model/CabinetViewerProject/Assets";
        const string Art = "Assets/WorkshopDemo/Art/ReviewedCabinet";
        const string Resources = "Assets/WorkshopDemo/Resources/ReviewedEquipment";
        static readonly Regex GuidPattern = new Regex(@"(?<=guid: )[a-f0-9]{32}");

        static string Key(string name) =>
            new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        static string ReadGuid(string path) => GuidPattern.Match(File.ReadAllText(path + ".meta")).Value;

        [MenuItem("Tools/Workshop Demo/Import Reviewed Cabinet")]
        public static void Import() => ImportViewerModel(Source, Art, "ElectricalCabinet", "electrical-cabinet",
            "cabinet", "FanFinish", "CabinetFinish", "Cabinet", "cab", 3247, 119066);

        internal static void ImportViewerModel(string source, string art, string inputFolder, string modelName,
            string resourceName, string inputShader, string outputShader, string finishName,
            string materialPrefix, int expectedMeshes, int expectedTriangles)
        {
            string mesh = art + "/" + modelName + ".fbx";
            string shader = art + "/" + outputShader + ".shader";
            string reflection = Resources + "/" + resourceName + "-reflection.exr";
            string input = source + "/" + inputFolder;
            string originalShader = source + "/Shaders/" + inputShader + ".shader";
            Directory.CreateDirectory(art + "/Textures");
            Directory.CreateDirectory(art + "/Materials");
            Directory.CreateDirectory(Resources);
            File.Copy(input + "/" + modelName + ".fbx", mesh, true);
            File.Copy(input + "/parts.json", Resources + "/" + resourceName + "-parts.json", true);
            File.Copy(source + "/Textures/inspection-studio.exr", reflection, true);
            string shaderText = File.ReadAllText(originalShader);
            File.WriteAllText(shader, Regex.Replace(shaderText, "^Shader \"[^\"]+\"",
                    "Shader \"Workshop/" + finishName + " Physical Finish\"")
                .Replace("#pragma multi_compile_fog",
                    "#pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS\n" +
                    "            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP\n" +
                    "            #pragma multi_compile_fog"));

            // Transfer the approved serialized finishes, including glass render state.
            // Only import textures referenced by those materials.
            var finishes = Directory.GetFiles(source + "/Generated", "*.mat")
                .Where(path => Path.GetFileName(path).StartsWith(materialPrefix, StringComparison.Ordinal))
                .ToDictionary(path => Path.GetFileName(path), File.ReadAllText);
            var required = new HashSet<string>(finishes.Values.SelectMany(
                yaml => GuidPattern.Matches(yaml).Cast<Match>().Select(match => match.Value)));
            var textures = new Dictionary<string, string>();
            string textureFolder = Directory.Exists(input + "/FinishTextures") ? "FinishTextures" : "SourceTextures";
            foreach (string meta in Directory.GetFiles(input + "/" + textureFolder, "*.meta"))
            {
                string path = meta.Substring(0, meta.Length - 5);
                string guid = ReadGuid(path);
                if (!required.Contains(guid)) continue;
                string target = art + "/Textures/" + Path.GetFileName(path);
                File.Copy(path, target, true);
                textures.Add(guid, target);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var guids = new Dictionary<string, string>
            {
                [ReadGuid(originalShader)] = AssetDatabase.AssetPathToGUID(shader)
            };
            foreach (var entry in textures)
            {
                var texture = (TextureImporter)AssetImporter.GetAtPath(entry.Value);
                string name = Path.GetFileNameWithoutExtension(entry.Value);
                bool normal = name.EndsWith("_Normal", StringComparison.OrdinalIgnoreCase);
                texture.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                texture.sRGBTexture = !normal && !name.EndsWith("_Roughness") && !name.EndsWith("_Metallic");
                texture.maxTextureSize = 4096;
                texture.textureCompression = TextureImporterCompression.Uncompressed;
                texture.mipmapEnabled = true;
                texture.anisoLevel = 16;
                texture.mipMapBias = name.StartsWith("powder_") ? 0 : -.25f;
                texture.SaveAndReimport();
                guids.Add(entry.Key, AssetDatabase.AssetPathToGUID(entry.Value));
            }
            var environment = (TextureImporter)AssetImporter.GetAtPath(reflection);
            environment.textureShape = TextureImporterShape.TextureCube;
            environment.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            environment.sRGBTexture = false;
            environment.mipmapEnabled = true;
            var settings = new TextureImporterSettings();
            environment.ReadTextureSettings(settings);
            settings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
            environment.SetTextureSettings(settings);
            environment.SaveAndReimport();
            foreach (var finish in finishes)
                File.WriteAllText(art + "/Materials/" + finish.Key,
                    GuidPattern.Replace(finish.Value, match =>
                        guids.TryGetValue(match.Value, out string guid) ? guid : match.Value));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importedShader = AssetDatabase.LoadAssetAtPath<Shader>(shader);
            if (!importedShader || ShaderUtil.ShaderHasError(importedShader))
                throw new InvalidOperationException("Reviewed finish shader failed compilation: " + modelName);

            var importer = (ModelImporter)AssetImporter.GetAtPath(mesh);
            importer.importAnimation = importer.importLights = importer.importCameras = false;
            importer.bakeAxisConversion = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(mesh));
            try
            {
                model.name = "Reviewed " + modelName;
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(slot =>
                    {
                        var material = AssetDatabase.LoadAssetAtPath<Material>(
                            art + "/Materials/" + Key(slot.name) + ".mat");
                        if (!material || !material.shader)
                            throw new InvalidOperationException("Missing " + modelName + " finish: " + slot.name);
                        return material;
                    }).ToArray();
                var meshes = model.GetComponentsInChildren<MeshFilter>();
                int triangles = meshes.Sum(filter => Enumerable.Range(0, filter.sharedMesh.subMeshCount)
                    .Sum(i => (int)filter.sharedMesh.GetIndexCount(i) / 3));
                if (meshes.Length != expectedMeshes || triangles != expectedTriangles)
                    throw new InvalidOperationException($"Reviewed {modelName} geometry changed: {meshes.Length}/{triangles}");
                if (!PrefabUtility.SaveAsPrefabAsset(model, Resources + "/" + modelName + ".prefab"))
                    throw new IOException("Could not save reviewed prefab: " + modelName);
                AssetDatabase.SaveAssets();
                Debug.Log($"REVIEWED_MODEL_IMPORTED {modelName} meshes={meshes.Length} triangles={triangles} textures={textures.Count}");
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }

        public static void ImportAndBuildReview()
        {
            Import();
            WorkshopPlatformBuild.BuildCabinetReview();
        }
    }
}
