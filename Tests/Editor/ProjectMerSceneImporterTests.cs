using System;
using NUnit.Framework;
using Scpsl.ProjectMer.Authoring;
using Scpsl.ProjectMer.Authoring.Editor;
using UnityEngine;

namespace Scpsl.ProjectMer.Authoring.Editor.Tests
{
    public sealed class ProjectMerSceneImporterTests
    {
        private GameObject importedRoot;

        [TearDown]
        public void TearDown()
        {
            if (importedRoot != null)
                UnityEngine.Object.DestroyImmediate(importedRoot);
        }

        [Test]
        public void ImportsSupportedHierarchyAndProperties()
        {
            const string json = @"{
  ""RootObjectId"": 0,
  ""Blocks"": [
    { ""Name"": ""root"", ""ObjectId"": 0, ""ParentId"": -1, ""Position"": {""x"":0,""y"":0,""z"":0}, ""Rotation"": {""x"":0,""y"":0,""z"":0}, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 0, ""Properties"": { ""Static"": true } },
    { ""Name"": ""cube"", ""ObjectId"": 1, ""ParentId"": 0, ""AnimatorName"": ""spin"", ""Position"": {""x"":1,""y"":2,""z"":3}, ""Rotation"": {""x"":4,""y"":5,""z"":6}, ""Scale"": {""x"":2,""y"":3,""z"":4}, ""BlockType"": 1, ""Properties"": { ""PrimitiveType"": 3, ""PrimitiveFlags"": 3, ""Color"": ""#11223344"", ""Static"": false } },
    { ""Name"": ""lamp"", ""ObjectId"": 2, ""ParentId"": 0, ""Position"": {""x"":0,""y"":1,""z"":0}, ""Rotation"": {""x"":0,""y"":0,""z"":0}, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 2, ""Properties"": { ""Color"": ""#80C0FFFF"", ""Intensity"": 3.5, ""Range"": 12, ""LightType"": 2, ""Shape"": 1, ""Static"": true } },
    { ""Name"": ""caption"", ""ObjectId"": 3, ""ParentId"": 0, ""Position"": {""x"":0,""y"":2,""z"":0}, ""Rotation"": {""x"":0,""y"":0,""z"":0}, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 8, ""Properties"": { ""Text"": ""HELLO"", ""DisplaySize"": {""x"":80,""y"":20}, ""Static"": true } }
  ]
}";

            ProjectMerImportResult result = ProjectMerSceneImporter.ImportJson(json, "test");
            importedRoot = result.Root;

            Assert.That(result.BlockCount, Is.EqualTo(4));
            Assert.That(result.Warnings, Is.Empty);
            Assert.That(importedRoot.name, Is.EqualTo("root"));

            Transform cube = importedRoot.transform.Find("cube");
            Assert.That(cube, Is.Not.Null);
            Assert.That(cube.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(cube.localScale, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That(cube.GetComponent<BoxCollider>(), Is.Not.Null);
            ProjectMerExportMetadata cubeMetadata = cube.GetComponent<ProjectMerExportMetadata>();
            Assert.That(cubeMetadata.ObjectId, Is.EqualTo(1));
            Assert.That(cubeMetadata.AnimatorName, Is.EqualTo("spin"));
            Assert.That(cubeMetadata.Collidable, Is.True);
            Assert.That(cubeMetadata.Visible, Is.True);
            Assert.That(cubeMetadata.Static, Is.False);
            Color cubeColor = new Color32(0x11, 0x22, 0x33, 0x44);
            Assert.That(cubeMetadata.Color.r, Is.EqualTo(cubeColor.r).Within(0.0001f));
            Assert.That(cubeMetadata.Color.g, Is.EqualTo(cubeColor.g).Within(0.0001f));
            Assert.That(cubeMetadata.Color.b, Is.EqualTo(cubeColor.b).Within(0.0001f));
            Assert.That(cubeMetadata.Color.a, Is.EqualTo(cubeColor.a).Within(0.0001f));

            Light light = importedRoot.transform.Find("lamp").GetComponent<Light>();
            Assert.That(light, Is.Not.Null);
            Assert.That(light.type, Is.EqualTo(LightType.Point));
            Assert.That(light.intensity, Is.EqualTo(3.5f));
            Assert.That(light.range, Is.EqualTo(12f));

            TextMesh text = importedRoot.transform.Find("caption").GetComponent<TextMesh>();
            Assert.That(text, Is.Not.Null);
            Assert.That(text.text, Is.EqualTo("HELLO"));
            Assert.That(text.GetComponent<ProjectMerExportMetadata>().DisplaySize, Is.EqualTo(new Vector2(80f, 20f)));
        }

        [Test]
        public void PreservesUnsupportedGameplayBlocksForReExport()
        {
            const string json = @"{
  ""RootObjectId"": 10,
  ""Blocks"": [
    { ""Name"": ""root"", ""ObjectId"": 10, ""ParentId"": -1, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 0, ""Properties"": {} },
    { ""Name"": ""workstation"", ""ObjectId"": 11, ""ParentId"": 10, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 4, ""Properties"": { ""IsInteractable"": true, ""Static"": false, ""MovementSmoothing"": 60 } },
    { ""Name"": ""pickup"", ""ObjectId"": 12, ""ParentId"": 10, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 3, ""Properties"": { ""ItemType"": 17, ""CustomItem"": 4, ""AttachmentsCode"": 1234, ""Chance"": 0.75, ""Uses"": 2, ""Static"": true, ""MovementSmoothing"": 25 } }
  ]
}";

            ProjectMerImportResult result = ProjectMerSceneImporter.ImportJson(json, "unsupported");
            importedRoot = result.Root;

            Transform workstation = importedRoot.transform.Find("workstation");
            Transform pickup = importedRoot.transform.Find("pickup");
            Assert.That(workstation, Is.Not.Null);
            Assert.That(pickup, Is.Not.Null);
            Assert.That(workstation.GetComponent<ProjectMerExportMetadata>().BlockKind, Is.EqualTo(MerBlockKind.Empty));
            Assert.That(pickup.GetComponent<ProjectMerExportMetadata>().BlockKind, Is.EqualTo(MerBlockKind.Empty));
            Assert.That(result.Warnings.Count, Is.EqualTo(2));

            workstation.GetComponent<ProjectMerExportMetadata>().Static = true;
            ProjectMerExportResult exported = ProjectMerSceneExporter.BuildHierarchyJson(importedRoot);
            Assert.That(exported.Success, Is.True, string.Join("\n", exported.Errors));

            RoundTripDocument document = JsonUtility.FromJson<RoundTripDocument>(exported.Json);
            RoundTripBlock workstationBlock = FindBlock(document, "workstation");
            RoundTripBlock pickupBlock = FindBlock(document, "pickup");
            Assert.That(workstationBlock.BlockType, Is.EqualTo(4));
            Assert.That(workstationBlock.Properties.IsInteractable, Is.True);
            Assert.That(workstationBlock.Properties.MovementSmoothing, Is.EqualTo(60));
            Assert.That(workstationBlock.Properties.Static, Is.True);
            Assert.That(pickupBlock.BlockType, Is.EqualTo(3));
            Assert.That(pickupBlock.Properties.ItemType, Is.EqualTo(17));
            Assert.That(pickupBlock.Properties.AttachmentsCode, Is.EqualTo(1234));
            Assert.That(pickupBlock.Properties.Chance, Is.EqualTo(0.75f));
            Assert.That(pickupBlock.Properties.MovementSmoothing, Is.EqualTo(25));
        }

        [Test]
        public void PreservesUnmodeledPropertiesOnSupportedBlocks()
        {
            const string json = @"{
  ""RootObjectId"": 0,
  ""Blocks"": [
    { ""Name"": ""root"", ""ObjectId"": 0, ""ParentId"": -1, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 0, ""Properties"": { ""Static"": true, ""MovementSmoothing"": 11 } },
    { ""Name"": ""cube"", ""ObjectId"": 1, ""ParentId"": 0, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 1, ""Properties"": { ""PrimitiveType"": 3, ""PrimitiveFlags"": 2, ""Color"": ""CACACAFF"", ""Static"": true, ""MovementSmoothing"": 42, ""FuturePrimitiveValue"": { ""Enabled"": true } } },
    { ""Name"": ""lamp"", ""ObjectId"": 2, ""ParentId"": 0, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 2, ""Properties"": { ""LightType"": 2, ""Color"": ""FFFFFFFF"", ""Intensity"": 2, ""Range"": 10, ""Flicker"": { ""Min"": 0.2, ""Max"": 0.8 }, ""FlickerZone"": 3, ""Static"": true, ""MovementSmoothing"": 55 } }
  ]
}";

            ProjectMerImportResult result = ProjectMerSceneImporter.ImportJson(json, "supported-extras");
            importedRoot = result.Root;
            importedRoot.transform.Find("cube").GetComponent<ProjectMerExportMetadata>().Visible = false;
            importedRoot.transform.Find("lamp").GetComponent<Light>().intensity = 7f;

            ProjectMerExportResult exported = ProjectMerSceneExporter.BuildHierarchyJson(importedRoot);
            Assert.That(exported.Success, Is.True, string.Join("\n", exported.Errors));

            RoundTripDocument document = JsonUtility.FromJson<RoundTripDocument>(exported.Json);
            RoundTripBlock rootBlock = FindBlock(document, "root");
            RoundTripBlock cubeBlock = FindBlock(document, "cube");
            RoundTripBlock lampBlock = FindBlock(document, "lamp");
            Assert.That(rootBlock.Properties.MovementSmoothing, Is.EqualTo(11));
            Assert.That(cubeBlock.Properties.MovementSmoothing, Is.EqualTo(42));
            Assert.That(cubeBlock.Properties.PrimitiveFlags, Is.EqualTo(0));
            Assert.That(cubeBlock.Properties.FuturePrimitiveValue.Enabled, Is.True);
            Assert.That(lampBlock.Properties.Intensity, Is.EqualTo(7f));
            Assert.That(lampBlock.Properties.FlickerZone, Is.EqualTo(3));
            Assert.That(lampBlock.Properties.Flicker.Min, Is.EqualTo(0.2f));
            Assert.That(lampBlock.Properties.MovementSmoothing, Is.EqualTo(55));
        }

        [Test]
        public void CreatesUnityRootForProjectMerVirtualRoot()
        {
            const string json = @"{
  ""RootObjectId"": 0,
  ""Blocks"": [
    { ""Name"": ""part-a"", ""ObjectId"": 1, ""ParentId"": 0, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 1, ""Properties"": { ""PrimitiveType"": 3, ""PrimitiveFlags"": 2 } },
    { ""Name"": ""part-b"", ""ObjectId"": 2, ""ParentId"": 0, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 1, ""Properties"": { ""PrimitiveType"": 3, ""PrimitiveFlags"": 2 } }
  ]
}";

            ProjectMerImportResult result = ProjectMerSceneImporter.ImportJson(json, "nu22-layer-1.json");
            importedRoot = result.Root;

            Assert.That(result.BlockCount, Is.EqualTo(2));
            Assert.That(importedRoot.name, Is.EqualTo("nu22-layer-1"));
            Assert.That(importedRoot.transform.childCount, Is.EqualTo(2));
            Assert.That(importedRoot.GetComponent<ProjectMerExportMetadata>().ObjectId, Is.EqualTo(0));
            Assert.That(result.Warnings.Count, Is.EqualTo(1));
            Assert.That(result.Warnings[0], Does.Contain("virtual ProjectMER root"));
        }

        [Test]
        public void ColorRgbaOverridesHexOnlyWhenExplicitlyPresent()
        {
            const string json = @"{
  ""RootObjectId"": 0,
  ""Blocks"": [
    { ""Name"": ""root"", ""ObjectId"": 0, ""ParentId"": -1, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 0, ""Properties"": {} },
    { ""Name"": ""hex"", ""ObjectId"": 1, ""ParentId"": 0, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 1, ""Properties"": { ""PrimitiveType"": 3, ""PrimitiveFlags"": 2, ""Color"": ""#193971FF"" } },
    { ""Name"": ""unprefixed"", ""ObjectId"": 2, ""ParentId"": 0, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 1, ""Properties"": { ""PrimitiveType"": 3, ""PrimitiveFlags"": 2, ""Color"": ""CACACAFF"" } },
    { ""Name"": ""hdr"", ""ObjectId"": 3, ""ParentId"": 0, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 1, ""Properties"": { ""PrimitiveType"": 3, ""PrimitiveFlags"": 2, ""Color"": ""#FFFFFFFF"", ""ColorRgba"": { ""r"":4, ""g"":0.25, ""b"":1, ""a"":0.6 } } }
  ]
}";

            ProjectMerImportResult result = ProjectMerSceneImporter.ImportJson(json, "colors");
            importedRoot = result.Root;

            ProjectMerExportMetadata hex = importedRoot.transform.Find("hex").GetComponent<ProjectMerExportMetadata>();
            ProjectMerExportMetadata unprefixed = importedRoot.transform.Find("unprefixed").GetComponent<ProjectMerExportMetadata>();
            ProjectMerExportMetadata hdr = importedRoot.transform.Find("hdr").GetComponent<ProjectMerExportMetadata>();
            Color expectedHex = new Color32(0x19, 0x39, 0x71, 0xFF);
            Color expectedUnprefixed = new Color32(0xCA, 0xCA, 0xCA, 0xFF);
            Assert.That(hex.Color.r, Is.EqualTo(expectedHex.r).Within(0.0001f));
            Assert.That(hex.Color.g, Is.EqualTo(expectedHex.g).Within(0.0001f));
            Assert.That(hex.Color.b, Is.EqualTo(expectedHex.b).Within(0.0001f));
            Assert.That(hex.Color.a, Is.EqualTo(expectedHex.a).Within(0.0001f));
            Assert.That(unprefixed.Color.r, Is.EqualTo(expectedUnprefixed.r).Within(0.0001f));
            Assert.That(unprefixed.Color.g, Is.EqualTo(expectedUnprefixed.g).Within(0.0001f));
            Assert.That(unprefixed.Color.b, Is.EqualTo(expectedUnprefixed.b).Within(0.0001f));
            Assert.That(unprefixed.Color.a, Is.EqualTo(expectedUnprefixed.a).Within(0.0001f));
            Assert.That(hdr.Color.r, Is.EqualTo(4f));
            Assert.That(hdr.Color.g, Is.EqualTo(0.25f));
            Assert.That(hdr.Color.b, Is.EqualTo(1f));
            Assert.That(hdr.Color.a, Is.EqualTo(0.6f));
        }

        [Test]
        public void RejectsBrokenParentGraphBeforeCreatingObjects()
        {
            const string json = @"{
  ""RootObjectId"": 0,
  ""Blocks"": [
    { ""Name"": ""root"", ""ObjectId"": 0, ""ParentId"": -1, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 0, ""Properties"": {} },
    { ""Name"": ""orphan"", ""ObjectId"": 1, ""ParentId"": 99, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 0, ""Properties"": {} }
  ]
}";

            FormatException exception = Assert.Throws<FormatException>(
                () => ProjectMerSceneImporter.ImportJson(json, "broken"));
            Assert.That(exception.Message, Does.Contain("missing ParentId 99"));
        }

        private static RoundTripBlock FindBlock(RoundTripDocument document, string name)
        {
            foreach (RoundTripBlock block in document.Blocks)
            {
                if (block.Name == name)
                    return block;
            }

            Assert.Fail("Could not find exported block '" + name + "'.");
            return null;
        }

        [Serializable]
        private sealed class RoundTripDocument
        {
            public RoundTripBlock[] Blocks;
        }

        [Serializable]
        private sealed class RoundTripBlock
        {
            public string Name;
            public int BlockType;
            public RoundTripProperties Properties;
        }

        [Serializable]
        private sealed class RoundTripProperties
        {
            public bool IsInteractable;
            public bool Static;
            public int MovementSmoothing;
            public int ItemType;
            public int AttachmentsCode;
            public float Chance;
            public int PrimitiveFlags;
            public FuturePrimitiveValue FuturePrimitiveValue;
            public float Intensity;
            public int FlickerZone;
            public FlickerValue Flicker;
        }

        [Serializable]
        private sealed class FuturePrimitiveValue
        {
            public bool Enabled;
        }

        [Serializable]
        private sealed class FlickerValue
        {
            public float Min;
        }
    }
}
