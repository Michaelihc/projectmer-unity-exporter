using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using Scpsl.ProjectMer.Authoring;
using UnityEditor;
using UnityEngine;

namespace Scpsl.ProjectMer.Authoring.Editor
{
    public sealed class ProjectMerImportResult
    {
        internal ProjectMerImportResult(GameObject root, int blockCount, IList<string> warnings)
        {
            Root = root;
            BlockCount = blockCount;
            Warnings = new List<string>(warnings).AsReadOnly();
        }

        public GameObject Root { get; }
        public int BlockCount { get; }
        public IList<string> Warnings { get; }
    }

    public static class ProjectMerSceneImporter
    {
        private const int BlockEmpty = 0;
        private const int BlockPrimitive = 1;
        private const int BlockLight = 2;
        private const int BlockText = 8;
        private const int FlagCollidable = 1;
        private const int FlagVisible = 2;
        private const string LastDirectoryKey = "Scpsl.ProjectMer.Authoring.LastImportDirectory";

        [Serializable]
        private sealed class ImportDocument
        {
            public int RootObjectId;
            public ImportBlock[] Blocks;
        }

        [Serializable]
        private sealed class ImportBlock
        {
            public string Name;
            public int ObjectId;
            public int ParentId;
            public string AnimatorName;
            public Vector3 Position;
            public Vector3 Rotation;
            public Vector3 Scale = Vector3.one;
            public int BlockType;
            public ImportProperties Properties = new ImportProperties();
            [NonSerialized] public bool HasColorRgba;
            [NonSerialized] public bool HasSourceProperties;
            [NonSerialized] public string SourcePropertiesJson;
        }

        [Serializable]
        private sealed class ImportProperties
        {
            public int PrimitiveType = (int)UnityEngine.PrimitiveType.Cube;
            public int PrimitiveFlags = FlagVisible;
            public string Color = "#FFFFFFFF";
            public ImportColor ColorRgba;
            public bool Static = true;
            public float Intensity = 1f;
            public float Range = 10f;
            public int ShadowType;
            public float ShadowStrength = 1f;
            public int LightType = (int)UnityEngine.LightType.Point;
            public int Shape;
            public float SpotAngle = 30f;
            public float InnerSpotAngle = 21.8f;
            public string Text = string.Empty;
            public Vector2 DisplaySize = new Vector2(200f, 50f);
        }

        [Serializable]
        private sealed class ImportColor
        {
            public float r;
            public float g;
            public float b;
            public float a = 1f;

            public Color ToColor()
            {
                return new Color(r, g, b, a);
            }
        }

        private sealed class ImportPlan
        {
            public ImportDocument Document;
            public int SourceBlockCount;
            public readonly Dictionary<int, ImportBlock> BlocksById = new Dictionary<int, ImportBlock>();
            public readonly List<string> Warnings = new List<string>();
        }

        [MenuItem("Tools/ProjectMER/Load JSON...")]
        public static void LoadJsonFromMenu()
        {
            string initialDirectory = EditorPrefs.GetString(LastDirectoryKey, string.Empty);
            string path = EditorUtility.OpenFilePanel("Load ProjectMER JSON", initialDirectory, "json");
            if (string.IsNullOrEmpty(path))
                return;

            EditorPrefs.SetString(LastDirectoryKey, Path.GetDirectoryName(path) ?? string.Empty);

            try
            {
                ProjectMerImportResult result = ImportFile(path, true);
                Selection.activeGameObject = result.Root;
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();

                foreach (string warning in result.Warnings)
                    Debug.LogWarning("ProjectMER: " + warning, result.Root);

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "ProjectMER: loaded {0} blocks from '{1}' into '{2}' with {3} warning(s).",
                    result.BlockCount, path, result.Root.name, result.Warnings.Count), result.Root);

                EditorUtility.DisplayDialog(
                    "ProjectMER load",
                    string.Format(CultureInfo.InvariantCulture,
                        "Loaded {0} blocks.\n\n{1}",
                        result.BlockCount,
                        result.Warnings.Count == 0
                            ? "No warnings."
                            : result.Warnings.Count + " warning(s); see the Console."),
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("ProjectMER load", "Could not load the file:\n\n" + exception.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static ProjectMerImportResult ImportFile(string path)
        {
            return ImportFile(path, false);
        }

        public static ProjectMerImportResult ImportJson(string json, string sourceName = "ProjectMER JSON")
        {
            return ImportJson(json, sourceName, false);
        }

        private static ProjectMerImportResult ImportFile(string path, bool registerUndo)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A ProjectMER JSON path is required.", nameof(path));

            string json = File.ReadAllText(path);
            return ImportJson(json, Path.GetFileName(path), registerUndo);
        }

        private static ProjectMerImportResult ImportJson(string json, string sourceName, bool registerUndo)
        {
            ImportPlan plan = BuildPlan(json, sourceName);
            List<GameObject> created = new List<GameObject>(plan.Document.Blocks.Length);
            Dictionary<int, GameObject> objectsById = new Dictionary<int, GameObject>(plan.Document.Blocks.Length);
            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            int undoGroup = -1;

            if (registerUndo)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Load ProjectMER JSON");
            }

            try
            {
                for (int i = 0; i < plan.Document.Blocks.Length; i++)
                {
                    if (registerUndo && (i & 127) == 0 && EditorUtility.DisplayCancelableProgressBar(
                        "Loading ProjectMER JSON",
                        string.Format(CultureInfo.InvariantCulture, "Creating block {0:N0} of {1:N0}", i + 1, plan.Document.Blocks.Length),
                        plan.Document.Blocks.Length == 0 ? 1f : (float)i / plan.Document.Blocks.Length))
                    {
                        throw new OperationCanceledException("ProjectMER import was cancelled.");
                    }

                    ImportBlock block = plan.Document.Blocks[i];
                    GameObject gameObject = CreateBlock(block, plan.Warnings, materials);
                    created.Add(gameObject);
                    objectsById.Add(block.ObjectId, gameObject);
                    if (registerUndo)
                        Undo.RegisterCreatedObjectUndo(gameObject, "Load ProjectMER block");
                }

                for (int i = 0; i < plan.Document.Blocks.Length; i++)
                {
                    ImportBlock block = plan.Document.Blocks[i];
                    GameObject gameObject = objectsById[block.ObjectId];
                    if (block.ObjectId != plan.Document.RootObjectId)
                        gameObject.transform.SetParent(objectsById[block.ParentId].transform, false);

                    gameObject.transform.localPosition = block.Position;
                    gameObject.transform.localEulerAngles = block.Rotation;
                    gameObject.transform.localScale = block.Scale;
                }

                GameObject root = objectsById[plan.Document.RootObjectId];
                if (registerUndo)
                    Undo.CollapseUndoOperations(undoGroup);

                return new ProjectMerImportResult(root, plan.SourceBlockCount, plan.Warnings);
            }
            catch
            {
                for (int i = created.Count - 1; i >= 0; i--)
                {
                    if (created[i] != null)
                        UnityEngine.Object.DestroyImmediate(created[i]);
                }

                foreach (Material material in materials.Values)
                {
                    if (material != null)
                        UnityEngine.Object.DestroyImmediate(material);
                }

                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static ImportPlan BuildPlan(string json, string sourceName)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException(sourceName + ": the file is empty.");

            ImportDocument document;
            try
            {
                document = JsonUtility.FromJson<ImportDocument>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException(sourceName + ": invalid JSON. " + exception.Message, exception);
            }

            if (document == null || document.Blocks == null || document.Blocks.Length == 0)
                throw new FormatException(sourceName + ": no ProjectMER Blocks array was found.");

            JArray jsonBlocks;
            try
            {
                jsonBlocks = JObject.Parse(json)["Blocks"] as JArray;
            }
            catch (Exception exception)
            {
                throw new FormatException(sourceName + ": invalid JSON. " + exception.Message, exception);
            }
            if (jsonBlocks == null || jsonBlocks.Count != document.Blocks.Length)
                throw new FormatException(sourceName + ": the ProjectMER Blocks array could not be read consistently.");

            ImportPlan plan = new ImportPlan
            {
                Document = document,
                SourceBlockCount = document.Blocks.Length,
            };
            for (int i = 0; i < document.Blocks.Length; i++)
            {
                ImportBlock block = document.Blocks[i];
                if (block == null)
                    throw new FormatException(sourceName + ": Blocks[" + i.ToString(CultureInfo.InvariantCulture) + "] is null.");
                if (plan.BlocksById.ContainsKey(block.ObjectId))
                    throw new FormatException(sourceName + ": duplicate ObjectId " + block.ObjectId.ToString(CultureInfo.InvariantCulture) + ".");
                plan.BlocksById.Add(block.ObjectId, block);
                if (!IsFinite(block.Position) || !IsFinite(block.Rotation) || !IsFinite(block.Scale))
                    throw new FormatException(sourceName + ": block " + block.ObjectId.ToString(CultureInfo.InvariantCulture) + " has a non-finite transform.");
                if (block.Properties == null)
                    block.Properties = new ImportProperties();
                JToken propertiesToken = jsonBlocks[i]["Properties"];
                block.HasColorRgba = propertiesToken != null && propertiesToken["ColorRgba"] is JObject;
                if (propertiesToken is JObject propertiesObject)
                {
                    block.HasSourceProperties = true;
                    block.SourcePropertiesJson = propertiesObject.ToString(Newtonsoft.Json.Formatting.None);
                }
            }

            if (!plan.BlocksById.ContainsKey(document.RootObjectId))
                ResolveMissingRoot(plan, sourceName);
            if (plan.BlocksById[document.RootObjectId].ParentId >= 0)
                throw new FormatException(sourceName + ": the root block must have a negative ParentId.");

            foreach (ImportBlock block in document.Blocks)
            {
                if (block.ObjectId == document.RootObjectId)
                    continue;
                if (!plan.BlocksById.ContainsKey(block.ParentId))
                    throw new FormatException(sourceName + ": block " + block.ObjectId.ToString(CultureInfo.InvariantCulture) +
                        " references missing ParentId " + block.ParentId.ToString(CultureInfo.InvariantCulture) + ".");

                HashSet<int> visited = new HashSet<int>();
                ImportBlock cursor = block;
                while (cursor.ObjectId != document.RootObjectId)
                {
                    if (!visited.Add(cursor.ObjectId))
                        throw new FormatException(sourceName + ": parent cycle detected at ObjectId " + cursor.ObjectId.ToString(CultureInfo.InvariantCulture) + ".");
                    if (!plan.BlocksById.TryGetValue(cursor.ParentId, out cursor))
                        throw new FormatException(sourceName + ": block " + block.ObjectId.ToString(CultureInfo.InvariantCulture) + " is not connected to RootObjectId.");
                }
            }

            return plan;
        }

        private static void ResolveMissingRoot(ImportPlan plan, string sourceName)
        {
            ImportDocument document = plan.Document;
            List<ImportBlock> declaredRoots = new List<ImportBlock>();
            bool referencesVirtualRoot = false;
            foreach (ImportBlock block in document.Blocks)
            {
                if (block.ParentId < 0)
                    declaredRoots.Add(block);
                if (block.ParentId == document.RootObjectId)
                    referencesVirtualRoot = true;
            }

            if (!referencesVirtualRoot && declaredRoots.Count == 1)
            {
                document.RootObjectId = declaredRoots[0].ObjectId;
                plan.Warnings.Add(string.Format(CultureInfo.InvariantCulture,
                    "RootObjectId was missing; inferred ObjectId {0} ('{1}') as the root.",
                    declaredRoots[0].ObjectId, NameOf(declaredRoots[0])));
                return;
            }

            if (!referencesVirtualRoot && declaredRoots.Count == 0)
            {
                throw new FormatException(sourceName + ": RootObjectId " +
                    document.RootObjectId.ToString(CultureInfo.InvariantCulture) +
                    " does not exist and no root block can be inferred.");
            }

            ImportBlock syntheticRoot = new ImportBlock
            {
                Name = SyntheticRootName(sourceName),
                ObjectId = document.RootObjectId,
                ParentId = -1,
                Position = Vector3.zero,
                Rotation = Vector3.zero,
                Scale = Vector3.one,
                BlockType = BlockEmpty,
                Properties = new ImportProperties(),
            };

            if (!referencesVirtualRoot)
            {
                foreach (ImportBlock root in declaredRoots)
                    root.ParentId = syntheticRoot.ObjectId;
            }

            ImportBlock[] blocksWithRoot = new ImportBlock[document.Blocks.Length + 1];
            blocksWithRoot[0] = syntheticRoot;
            Array.Copy(document.Blocks, 0, blocksWithRoot, 1, document.Blocks.Length);
            document.Blocks = blocksWithRoot;
            plan.BlocksById.Add(syntheticRoot.ObjectId, syntheticRoot);
            plan.Warnings.Add(string.Format(CultureInfo.InvariantCulture,
                "RootObjectId {0} is a virtual ProjectMER root; created Unity root '{1}'.",
                syntheticRoot.ObjectId, syntheticRoot.Name));
        }

        private static string SyntheticRootName(string sourceName)
        {
            string name = Path.GetFileNameWithoutExtension(sourceName ?? string.Empty);
            if (name.EndsWith(".mer", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            return string.IsNullOrWhiteSpace(name) ? "ProjectMER Schematic" : name;
        }

        private static GameObject CreateBlock(
            ImportBlock block,
            IList<string> warnings,
            IDictionary<string, Material> materials)
        {
            GameObject gameObject;
            switch (block.BlockType)
            {
                case BlockPrimitive:
                    gameObject = CreatePrimitive(block, warnings, materials);
                    break;
                case BlockLight:
                    gameObject = CreateLight(block, warnings);
                    break;
                case BlockText:
                    gameObject = CreateText(block);
                    break;
                default:
                    gameObject = new GameObject(NameOf(block));
                    if (block.BlockType != BlockEmpty)
                    {
                        warnings.Add(string.Format(CultureInfo.InvariantCulture,
                            "'{0}' (ObjectId {1}) uses unsupported BlockType {2}; loaded as a transform-only placeholder with its source properties preserved for re-export.",
                            NameOf(block), block.ObjectId, block.BlockType));
                    }
                    break;
            }

            gameObject.name = NameOf(block);
            gameObject.isStatic = block.Properties.Static;
            ApplyMetadata(gameObject, block);
            return gameObject;
        }

        private static GameObject CreatePrimitive(
            ImportBlock block,
            IList<string> warnings,
            IDictionary<string, Material> materials)
        {
            int primitiveValue = block.Properties.PrimitiveType;
            if (primitiveValue < (int)PrimitiveType.Sphere || primitiveValue > (int)PrimitiveType.Quad)
            {
                warnings.Add(string.Format(CultureInfo.InvariantCulture,
                    "'{0}' (ObjectId {1}) uses unsupported PrimitiveType {2}; showing a Cube placeholder.",
                    NameOf(block), block.ObjectId, primitiveValue));
                primitiveValue = (int)PrimitiveType.Cube;
            }

            GameObject gameObject = GameObject.CreatePrimitive((PrimitiveType)primitiveValue);
            bool visible = (block.Properties.PrimitiveFlags & FlagVisible) != 0;
            bool collidable = (block.Properties.PrimitiveFlags & FlagCollidable) != 0;
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = visible;
                Color color = ColorOf(block, warnings);
                string key = ColorKey(color);
                if (!materials.TryGetValue(key, out Material material))
                {
                    material = CreateMaterial(color);
                    materials.Add(key, material);
                }
                renderer.sharedMaterial = material;
            }

            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null && !collidable)
                UnityEngine.Object.DestroyImmediate(collider);

            return gameObject;
        }

        private static GameObject CreateLight(ImportBlock block, IList<string> warnings)
        {
            GameObject gameObject = new GameObject(NameOf(block));
            Light light = gameObject.AddComponent<Light>();
            int lightType = block.Properties.LightType;
            if (lightType < (int)UnityEngine.LightType.Spot || lightType > (int)UnityEngine.LightType.Point)
            {
                warnings.Add(string.Format(CultureInfo.InvariantCulture,
                    "'{0}' (ObjectId {1}) uses unsupported LightType {2}; showing a Point light.",
                    NameOf(block), block.ObjectId, lightType));
                lightType = (int)UnityEngine.LightType.Point;
            }

            light.type = (UnityEngine.LightType)lightType;
            light.color = ColorOf(block, warnings);
            light.intensity = Mathf.Max(0f, block.Properties.Intensity);
            light.range = Mathf.Max(0.01f, block.Properties.Range);
            light.shadows = (LightShadows)Mathf.Clamp(block.Properties.ShadowType, 0, 2);
            light.shadowStrength = Mathf.Clamp01(block.Properties.ShadowStrength);
            light.spotAngle = Mathf.Clamp(block.Properties.SpotAngle, 1f, 179f);
            light.innerSpotAngle = Mathf.Clamp(block.Properties.InnerSpotAngle, 0f, light.spotAngle);
            return gameObject;
        }

        private static GameObject CreateText(ImportBlock block)
        {
            GameObject gameObject = new GameObject(NameOf(block));
            TextMesh text = gameObject.AddComponent<TextMesh>();
            text.text = block.Properties.Text ?? string.Empty;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.richText = true;
            text.fontSize = 64;
            text.characterSize = Mathf.Max(0.01f, block.Properties.DisplaySize.y / 64f);
            return gameObject;
        }

        private static void ApplyMetadata(GameObject gameObject, ImportBlock block)
        {
            ProjectMerExportMetadata metadata = gameObject.AddComponent<ProjectMerExportMetadata>();
            metadata.ObjectId = block.ObjectId;
            metadata.AnimatorName = block.AnimatorName ?? string.Empty;
            metadata.Static = block.Properties.Static;
            if (block.HasSourceProperties)
                metadata.SetImportedSource(block.BlockType, block.SourcePropertiesJson);

            switch (block.BlockType)
            {
                case BlockPrimitive:
                    metadata.BlockKind = MerBlockKind.Primitive;
                    metadata.OverridePrimitiveType = true;
                    metadata.PrimitiveType = block.Properties.PrimitiveType >= 0 && block.Properties.PrimitiveType <= 5
                        ? (MerPrimitiveType)block.Properties.PrimitiveType
                        : MerPrimitiveType.Cube;
                    metadata.OverrideColor = true;
                    metadata.Color = ColorOf(block, null);
                    metadata.Visible = (block.Properties.PrimitiveFlags & FlagVisible) != 0;
                    metadata.Collidable = (block.Properties.PrimitiveFlags & FlagCollidable) != 0;
                    break;
                case BlockLight:
                    metadata.BlockKind = MerBlockKind.Light;
                    metadata.OverrideColor = true;
                    metadata.Color = ColorOf(block, null);
                    metadata.LightShape = block.Properties.Shape >= 0 && block.Properties.Shape <= 2
                        ? (MerLightShape)block.Properties.Shape
                        : MerLightShape.Cone;
                    break;
                case BlockText:
                    metadata.BlockKind = MerBlockKind.Text;
                    metadata.Text = block.Properties.Text ?? string.Empty;
                    metadata.DisplaySize = block.Properties.DisplaySize.x > 0f && block.Properties.DisplaySize.y > 0f
                        ? block.Properties.DisplaySize
                        : new Vector2(200f, 50f);
                    break;
                default:
                    metadata.BlockKind = MerBlockKind.Empty;
                    break;
            }
        }

        private static Color ColorOf(ImportBlock block, IList<string> warnings)
        {
            if (block.HasColorRgba && block.Properties.ColorRgba != null)
                return block.Properties.ColorRgba.ToColor();

            string colorText = block.Properties.Color ?? string.Empty;
            if (!string.IsNullOrEmpty(colorText) && colorText[0] != '#')
                colorText = "#" + colorText;

            if (ColorUtility.TryParseHtmlString(colorText, out Color color))
                return color;

            if (warnings != null)
            {
                warnings.Add(string.Format(CultureInfo.InvariantCulture,
                    "'{0}' (ObjectId {1}) has invalid color '{2}'; using white.",
                    NameOf(block), block.ObjectId, block.Properties.Color));
            }
            return Color.white;
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/InternalErrorShader");
            Material material = new Material(shader)
            {
                name = "ProjectMER " + ColorKey(color),
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            return material;
        }

        private static string ColorKey(Color color)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:R}_{1:R}_{2:R}_{3:R}", color.r, color.g, color.b, color.a);
        }

        private static string NameOf(ImportBlock block)
        {
            return string.IsNullOrWhiteSpace(block.Name)
                ? "ProjectMER Block " + block.ObjectId.ToString(CultureInfo.InvariantCulture)
                : block.Name;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
