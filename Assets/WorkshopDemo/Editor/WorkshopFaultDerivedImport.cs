using UnityEditor;

namespace WorkshopDemo.Editor
{
    // Only the derived shell needs CPU geometry for the local perforation checks.
    public sealed class WorkshopFaultDerivedImport : AssetPostprocessor
    {
        const string DerivedPath = "Assets/WorkshopDemo/Resources/WorkshopFaults/tank-shell-perforated.fbx";
        const string ThinnedPath = "Assets/WorkshopDemo/Resources/WorkshopFaults/tank-shell-thinned.fbx";

        public override uint GetVersion() => 2;

        [System.Serializable] sealed class Geometry
        {
            public UnityEngine.Vector3[] vertices;
            public int[] triangles;
        }

        public static void Inspect()
        {
            EnsureImported();
            var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(DerivedPath);
            var filter = prefab.GetComponentInChildren<UnityEngine.MeshFilter>(true);
            System.IO.File.WriteAllText("Evidence/fault-module-20261115/tank-imported-geometry.json",
                UnityEngine.JsonUtility.ToJson(new Geometry {
                    vertices = filter.sharedMesh.vertices, triangles = filter.sharedMesh.triangles
                }));
        }

        internal static void EnsureImported()
        {
            foreach (var path in new[] { DerivedPath, ThinnedPath })
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer && (!importer.isReadable || !importer.bakeAxisConversion ||
                    importer.materialImportMode != ModelImporterMaterialImportMode.None))
                {
                    importer.isReadable = true;
                    importer.bakeAxisConversion = true;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.SaveAndReimport();
                }
            }
        }

        void OnPreprocessModel()
        {
            if (assetPath != DerivedPath && assetPath != ThinnedPath) return;
            var importer = (ModelImporter)assetImporter;
            importer.isReadable = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
