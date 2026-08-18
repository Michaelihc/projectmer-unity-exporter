using System;
using System.Collections.Generic;
using NUnit.Framework;
using Scpsl.ProjectMer.Authoring;
using Scpsl.ProjectMer.Authoring.Editor;
using UnityEngine;

namespace Scpsl.ProjectMer.Authoring.Editor.Tests
{
    public sealed class ProjectMerSceneExporterTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                    UnityEngine.Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        [Test]
        public void ExportsSupportedHierarchyWithExactPrimitiveValues()
        {
            GameObject root = CreateObject("root");
            PrimitiveType[] primitiveTypes =
            {
                PrimitiveType.Sphere,
                PrimitiveType.Capsule,
                PrimitiveType.Cylinder,
                PrimitiveType.Cube,
                PrimitiveType.Plane,
                PrimitiveType.Quad,
            };

            foreach (PrimitiveType primitiveType in primitiveTypes)
            {
                GameObject primitive = GameObject.CreatePrimitive(primitiveType);
                created.Add(primitive);
                primitive.name = primitiveType.ToString();
                primitive.transform.SetParent(root.transform, false);
            }

            GameObject lightObject = CreateObject("light");
            lightObject.transform.SetParent(root.transform, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 3.5f;
            light.range = 12f;

            GameObject textObject = CreateObject("text");
            textObject.transform.SetParent(root.transform, false);
            ProjectMerExportMetadata textMetadata = textObject.AddComponent<ProjectMerExportMetadata>();
            textMetadata.BlockKind = MerBlockKind.Text;
            textMetadata.Text = "hello";
            textMetadata.DisplaySize = new Vector2(80f, 20f);

            ProjectMerExportResult result = ProjectMerSceneExporter.BuildHierarchyJson(root);

            Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.BlockCount, Is.EqualTo(9));

            TestDocument document = JsonUtility.FromJson<TestDocument>(result.Json);
            Assert.That(document.RootObjectId, Is.EqualTo(0));
            Assert.That(document.Blocks, Has.Length.EqualTo(9));
            Assert.That(document.Blocks[0].ParentId, Is.EqualTo(-1));
            Assert.That(document.Blocks[0].BlockType, Is.EqualTo(0));

            HashSet<int> ids = new HashSet<int>();
            foreach (TestBlock block in document.Blocks)
                Assert.That(ids.Add(block.ObjectId), Is.True, "Duplicate ObjectId " + block.ObjectId);

            for (int i = 0; i < primitiveTypes.Length; i++)
            {
                TestBlock block = Array.Find(document.Blocks, item => item.Name == primitiveTypes[i].ToString());
                Assert.That(block, Is.Not.Null);
                Assert.That(block.BlockType, Is.EqualTo(1));
                Assert.That(block.Properties.PrimitiveType, Is.EqualTo((int)primitiveTypes[i]));
                Assert.That(block.Properties.PrimitiveFlags, Is.EqualTo(2));
                Assert.That(block.ParentId, Is.EqualTo(0));
            }

            TestBlock lightBlock = Array.Find(document.Blocks, item => item.Name == "light");
            Assert.That(lightBlock.BlockType, Is.EqualTo(2));
            Assert.That(lightBlock.Properties.LightType, Is.EqualTo((int)LightType.Point));
            Assert.That(lightBlock.Properties.Intensity, Is.EqualTo(3.5f));

            TestBlock textBlock = Array.Find(document.Blocks, item => item.Name == "text");
            Assert.That(textBlock.BlockType, Is.EqualTo(8));
            Assert.That(textBlock.Properties.Text, Is.EqualTo("hello"));
            Assert.That(textBlock.Properties.DisplaySize.x, Is.EqualTo(80f));
        }

        [Test]
        public void RejectsArbitraryMeshUnlessPrimitiveOverrideIsExplicit()
        {
            GameObject root = CreateObject("root");
            GameObject custom = CreateObject("custom");
            custom.transform.SetParent(root.transform, false);
            Mesh mesh = new Mesh { name = "GeneratedAiMesh" };
            created.Add(mesh);
            custom.AddComponent<MeshFilter>().sharedMesh = mesh;
            custom.AddComponent<MeshRenderer>();

            ProjectMerExportResult rejected = ProjectMerSceneExporter.BuildHierarchyJson(root);
            Assert.That(rejected.Success, Is.False);
            Assert.That(string.Join("\n", rejected.Errors), Does.Contain("cannot serialize arbitrary mesh vertices"));

            ProjectMerExportMetadata metadata = custom.AddComponent<ProjectMerExportMetadata>();
            metadata.BlockKind = MerBlockKind.Primitive;
            metadata.OverridePrimitiveType = true;
            metadata.PrimitiveType = MerPrimitiveType.Cube;

            ProjectMerExportResult overridden = ProjectMerSceneExporter.BuildHierarchyJson(root);
            Assert.That(overridden.Success, Is.True, string.Join("\n", overridden.Errors));
            Assert.That(overridden.Json, Does.Contain("\"PrimitiveType\": 3"));
        }

        [Test]
        public void AutomaticallyAssignsUniqueExportIdsForDuplicatedMetadata()
        {
            GameObject root = CreateObject("root");
            GameObject original = CreateObject("original");
            GameObject copy = CreateObject("copy");
            original.transform.SetParent(root.transform, false);
            copy.transform.SetParent(root.transform, false);
            ProjectMerExportMetadata originalMetadata = original.AddComponent<ProjectMerExportMetadata>();
            ProjectMerExportMetadata copyMetadata = copy.AddComponent<ProjectMerExportMetadata>();
            originalMetadata.ObjectId = 42;
            copyMetadata.ObjectId = 42;

            ProjectMerExportResult first = ProjectMerSceneExporter.BuildHierarchyJson(root);
            ProjectMerExportResult second = ProjectMerSceneExporter.BuildHierarchyJson(root);

            Assert.That(first.Success, Is.True, string.Join("\n", first.Errors));
            Assert.That(second.Success, Is.True, string.Join("\n", second.Errors));
            Assert.That(first.Warnings.Count, Is.EqualTo(1));
            Assert.That(first.Warnings[0], Does.Contain("automatically assigned 1"));

            TestDocument firstDocument = JsonUtility.FromJson<TestDocument>(first.Json);
            TestDocument secondDocument = JsonUtility.FromJson<TestDocument>(second.Json);
            TestBlock firstOriginal = Array.Find(firstDocument.Blocks, item => item.Name == "original");
            TestBlock firstCopy = Array.Find(firstDocument.Blocks, item => item.Name == "copy");
            TestBlock secondCopy = Array.Find(secondDocument.Blocks, item => item.Name == "copy");
            Assert.That(firstOriginal.ObjectId, Is.EqualTo(42));
            Assert.That(firstCopy.ObjectId, Is.EqualTo(1));
            Assert.That(secondCopy.ObjectId, Is.EqualTo(firstCopy.ObjectId));
            Assert.That(originalMetadata.ObjectId, Is.EqualTo(42));
            Assert.That(copyMetadata.ObjectId, Is.EqualTo(42));
        }

        [Test]
        public void RepairsLaterDuplicateOverridesWithUndoCompatibleApi()
        {
            GameObject root = CreateObject("root");
            GameObject original = CreateObject("original");
            GameObject copy = CreateObject("copy");
            original.transform.SetParent(root.transform, false);
            copy.transform.SetParent(root.transform, false);
            ProjectMerExportMetadata originalMetadata = original.AddComponent<ProjectMerExportMetadata>();
            ProjectMerExportMetadata copyMetadata = copy.AddComponent<ProjectMerExportMetadata>();
            originalMetadata.ObjectId = 42;
            copyMetadata.ObjectId = 42;

            int repaired = ProjectMerSceneExporter.RepairDuplicateObjectIds(root);

            Assert.That(repaired, Is.EqualTo(1));
            Assert.That(originalMetadata.ObjectId, Is.EqualTo(42));
            Assert.That(copyMetadata.ObjectId, Is.EqualTo(-1));
            ProjectMerExportResult result = ProjectMerSceneExporter.BuildHierarchyJson(root);
            Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Warnings, Is.Empty);
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            created.Add(gameObject);
            return gameObject;
        }

        [Serializable]
        private sealed class TestDocument
        {
            public int RootObjectId;
            public TestBlock[] Blocks;
        }

        [Serializable]
        private sealed class TestBlock
        {
            public string Name;
            public int ObjectId;
            public int ParentId;
            public int BlockType;
            public TestProperties Properties;
        }

        [Serializable]
        private sealed class TestProperties
        {
            public int PrimitiveType;
            public int PrimitiveFlags;
            public int LightType;
            public float Intensity;
            public string Text;
            public Vector2 DisplaySize;
        }
    }
}
