using System.IO;
using UnityEditor;
using UnityEngine;

namespace WorkshopDemo.Editor
{
    public static class WorkshopEquipmentArt
    {
        const string Root = "Assets/WorkshopDemo/Art/Equipment";
        const string Textures = "Assets/WorkshopDemo/External/FactoryTraining/Textures/";

        public static void Prepare()
        {
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Textures");
            AssetDatabase.Refresh();
            Copy("PaintBlue", "shared/damaged_brush_painted_metal_base_blue.png");
            Copy("PaintYellow", "shared/damaged_brush_painted_metal_base_yellow.png");
            Copy("Galvanized", "metal/metal_galvanized/metal_galvanized_base.png");
            Copy("MetalNormal", "shared/metal_galvanized_normal.png");
            Copy("PaintNormal", "metal/damaged_brush_painted_metal/damaged_brush_painted_metal_normal.png");
            Copy("Rubber", "moto/rubber_01_Base_color.png");
            Create("PumpBlue", Color.white, "PaintBlue", "PaintNormal", .35f, .34f);
            Create("PumpStandby", new Color(.43f, .66f, .60f), "Galvanized", "MetalNormal", .38f, .31f);
            Create("ConveyorFrame", new Color(.75f, .78f, .8f), "Galvanized", "MetalNormal", .7f, .42f);
            Create("BrushedSteel", new Color(.68f, .72f, .75f), "Galvanized", "MetalNormal", .85f, .48f);
            Create("TankSteel", new Color(.73f, .77f, .79f), "Galvanized", "MetalNormal", .75f, .4f);
            Create("TankGreen", new Color(.31f, .58f, .52f), "Galvanized", "MetalNormal", .4f, .35f);
            Create("FanYellow", Color.white, "PaintYellow", "PaintNormal", .4f, .32f);
            Create("CabinetBody", new Color(.29f, .33f, .36f), "Galvanized", "MetalNormal", .45f, .32f);
            Create("CabinetDoor", new Color(.62f, .67f, .70f), "Galvanized", "MetalNormal", .42f, .38f);
            Create("Rubber", new Color(.5f, .5f, .5f), "Rubber", null, .05f, .2f);
        }

        static void Copy(string name, string source)
        {
            string path = Root + "/Textures/" + name + ".png";
            if (!File.Exists(path) && !AssetDatabase.CopyAsset(Textures + source, path))
                throw new IOException("Cannot copy equipment texture " + source);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var type = name.EndsWith("Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type || importer.maxTextureSize > 1024)
            {
                importer.textureType = type;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
        }

        static void Create(string name, Color color, string texture, string normal, float metallic, float smoothness)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + texture + ".png"));
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetTextureScale("_BaseMap", Vector2.one * 2);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + normal + ".png"));
                material.SetTextureScale("_BumpMap", Vector2.one * 2);
                material.SetFloat("_BumpScale", .22f);
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
        }

        public static void Apply(GameObject model, DeviceKind kind, string id)
        {
            if (kind == DeviceKind.Motor) return;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                // Keep gauge faces, markings and lamp lenses distinct from the enclosure.
                if (renderer.name.Contains("gauge") || renderer.name.Contains("lens")) continue;
                string name = kind == DeviceKind.Pump ? id == "P-01" ? "PumpBlue" : "PumpStandby" :
                    kind == DeviceKind.Tank ? id == "TK-01" ? "TankSteel" : "TankGreen" :
                    kind == DeviceKind.Fan ? renderer.name.Contains("rotor") ? "BrushedSteel" : "FanYellow" :
                    kind == DeviceKind.Cabinet ? renderer.name.Contains("door") ? "CabinetDoor" : "CabinetBody" :
                    renderer.name.Contains("drum") ? "Rubber" : "ConveyorFrame";
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat");
            }
        }
    }
}
