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
        public void LoadsUnsupportedBlockAsTransformOnlyPlaceholder()
        {
            const string json = @"{
  ""RootObjectId"": 10,
  ""Blocks"": [
    { ""Name"": ""root"", ""ObjectId"": 10, ""ParentId"": -1, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 0, ""Properties"": {} },
    { ""Name"": ""door"", ""ObjectId"": 11, ""ParentId"": 10, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 4, ""Properties"": {} }
  ]
}";

            ProjectMerImportResult result = ProjectMerSceneImporter.ImportJson(json, "unsupported");
            importedRoot = result.Root;

            Transform placeholder = importedRoot.transform.Find("door");
            Assert.That(placeholder, Is.Not.Null);
            Assert.That(placeholder.GetComponent<ProjectMerExportMetadata>().BlockKind, Is.EqualTo(MerBlockKind.Empty));
            Assert.That(result.Warnings.Count, Is.EqualTo(1));
            Assert.That(result.Warnings[0], Does.Contain("BlockType 4"));
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
    { ""Name"": ""hdr"", ""ObjectId"": 2, ""ParentId"": 0, ""Scale"": {""x"":1,""y"":1,""z"":1}, ""BlockType"": 1, ""Properties"": { ""PrimitiveType"": 3, ""PrimitiveFlags"": 2, ""Color"": ""#FFFFFFFF"", ""ColorRgba"": { ""r"":4, ""g"":0.25, ""b"":1, ""a"":0.6 } } }
  ]
}";

            ProjectMerImportResult result = ProjectMerSceneImporter.ImportJson(json, "colors");
            importedRoot = result.Root;

            ProjectMerExportMetadata hex = importedRoot.transform.Find("hex").GetComponent<ProjectMerExportMetadata>();
            ProjectMerExportMetadata hdr = importedRoot.transform.Find("hdr").GetComponent<ProjectMerExportMetadata>();
            Color expectedHex = new Color32(0x19, 0x39, 0x71, 0xFF);
            Assert.That(hex.Color.r, Is.EqualTo(expectedHex.r).Within(0.0001f));
            Assert.That(hex.Color.g, Is.EqualTo(expectedHex.g).Within(0.0001f));
            Assert.That(hex.Color.b, Is.EqualTo(expectedHex.b).Within(0.0001f));
            Assert.That(hex.Color.a, Is.EqualTo(expectedHex.a).Within(0.0001f));
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
    }
}
