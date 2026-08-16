using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Scpsl.ProjectMer.Authoring;
using UnityEditor;
using UnityEngine;

namespace Scpsl.ProjectMer.Authoring.Editor
{
    public sealed class ProjectMerExportResult
    {
        internal ProjectMerExportResult(string json, int blockCount, IList<string> errors, IList<string> warnings)
        {
            Json = json;
            BlockCount = blockCount;
            Errors = new List<string>(errors).AsReadOnly();
            Warnings = new List<string>(warnings).AsReadOnly();
        }

        public string Json { get; }
        public int BlockCount { get; }
        public IList<string> Errors { get; }
        public IList<string> Warnings { get; }
        public bool Success { get { return Errors.Count == 0; } }
    }

    public static class ProjectMerSceneExporter
    {
        private const int BlockEmpty = 0;
        private const int BlockPrimitive = 1;
        private const int BlockLight = 2;
        private const int BlockText = 8;
        private const int FlagCollidable = 1;
        private const int FlagVisible = 2;

        private sealed class ExportNode
        {
            public Transform Transform;
            public ProjectMerExportMetadata Metadata;
            public MerBlockKind Kind;
            public int ObjectId;
            public int ParentId;
            public MerPrimitiveType PrimitiveType;
        }

        private sealed class ExportPlan
        {
            public readonly List<ExportNode> Nodes = new List<ExportNode>();
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
        }

        [MenuItem("Tools/ProjectMER/Validate Selected Hierarchy", true)]
        [MenuItem("Tools/ProjectMER/Export Selected Hierarchy...", true)]
        private static bool HasSelectedRoot()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem("Tools/ProjectMER/Validate Selected Hierarchy")]
        public static void ValidateSelectedHierarchy()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
                return;

            ExportPlan plan = BuildPlan(root.transform);
            LogReport(root.name, plan);

            string message = plan.Errors.Count == 0
                ? string.Format(CultureInfo.InvariantCulture,
                    "Valid ProjectMER hierarchy: {0} blocks, {1} warning(s).", plan.Nodes.Count, plan.Warnings.Count)
                : string.Format(CultureInfo.InvariantCulture,
                    "ProjectMER export is blocked by {0} error(s). See the Console.", plan.Errors.Count);

            EditorUtility.DisplayDialog("ProjectMER validation", message, "OK");
        }

        [MenuItem("Tools/ProjectMER/Export Selected Hierarchy...")]
        public static void ExportSelectedHierarchy()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
                return;

            ExportPlan plan = BuildPlan(root.transform);
            LogReport(root.name, plan);
            if (plan.Errors.Count != 0)
            {
                EditorUtility.DisplayDialog(
                    "ProjectMER export",
                    string.Format(CultureInfo.InvariantCulture,
                        "Export stopped: {0} error(s). See the Console for object paths and fixes.", plan.Errors.Count),
                    "OK");
                return;
            }

            string defaultName = SanitizeFileName(root.name) + ".mer.json";
            string path = EditorUtility.SaveFilePanel("Export ProjectMER schematic", string.Empty, defaultName, "json");
            if (string.IsNullOrEmpty(path))
                return;

            string json = WriteJson(plan);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            AssetDatabase.Refresh();

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "ProjectMER: exported {0} blocks from '{1}' to '{2}'.", plan.Nodes.Count, root.name, path), root);
        }

        /// <summary>
        /// Validates and serializes a hierarchy without opening editor UI. Builder scripts and MCP
        /// automation can call this immediately after constructing their GameObjects.
        /// </summary>
        public static ProjectMerExportResult BuildHierarchyJson(GameObject root)
        {
            if (root == null)
            {
                return new ProjectMerExportResult(
                    null,
                    0,
                    new[] { "A non-null root GameObject is required." },
                    new string[0]);
            }

            ExportPlan plan = BuildPlan(root.transform);
            string json = plan.Errors.Count == 0 ? WriteJson(plan) : null;
            return new ProjectMerExportResult(json, plan.Nodes.Count, plan.Errors, plan.Warnings);
        }

        /// <summary>
        /// Validates and exports a hierarchy to a known path without opening a save dialog.
        /// No file is written when validation fails.
        /// </summary>
        public static ProjectMerExportResult ExportHierarchyToFile(GameObject root, string path)
        {
            ProjectMerExportResult result = BuildHierarchyJson(root);
            if (!result.Success)
                return result;
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("An export path is required.", nameof(path));

            File.WriteAllText(path, result.Json, new UTF8Encoding(false));
            return result;
        }

        private static ExportPlan BuildPlan(Transform root)
        {
            ExportPlan plan = new ExportPlan();
            Gather(root, root, plan);
            AssignObjectIds(plan);

            Dictionary<Transform, ExportNode> byTransform = new Dictionary<Transform, ExportNode>();
            foreach (ExportNode node in plan.Nodes)
                byTransform[node.Transform] = node;

            foreach (ExportNode node in plan.Nodes)
            {
                if (node.Transform == root)
                {
                    node.ParentId = -1;
                    continue;
                }

                if (!byTransform.TryGetValue(node.Transform.parent, out ExportNode parent))
                {
                    plan.Errors.Add(PathOf(root, node.Transform) + ": exported parent is missing.");
                    continue;
                }

                node.ParentId = parent.ObjectId;
            }

            return plan;
        }

        private static void Gather(Transform root, Transform current, ExportPlan plan)
        {
            ProjectMerExportMetadata metadata = current.GetComponent<ProjectMerExportMetadata>();
            if (metadata != null && metadata.BlockKind == MerBlockKind.Ignore)
            {
                if (current == root)
                    plan.Errors.Add(PathOf(root, current) + ": the selected root cannot be ignored.");
                return; // Ignore is deliberately subtree-wide.
            }

            MerBlockKind kind = ResolveKind(root, current, metadata, plan);
            ExportNode node = new ExportNode
            {
                Transform = current,
                Metadata = metadata,
                Kind = kind,
                ObjectId = -1,
                ParentId = -1,
            };

            ValidateTransform(root, node, plan);
            if (kind == MerBlockKind.Primitive)
                ResolvePrimitiveType(root, node, plan);
            else if (kind == MerBlockKind.Light)
                ValidateLight(root, node, plan);
            else if (kind == MerBlockKind.Text)
                ValidateText(root, node, plan);

            plan.Nodes.Add(node);
            for (int i = 0; i < current.childCount; i++)
                Gather(root, current.GetChild(i), plan);
        }

        private static MerBlockKind ResolveKind(
            Transform root,
            Transform transform,
            ProjectMerExportMetadata metadata,
            ExportPlan plan)
        {
            if (metadata != null && metadata.BlockKind != MerBlockKind.Auto)
                return metadata.BlockKind;

            if (transform.GetComponent<Light>() != null)
                return MerBlockKind.Light;

            SkinnedMeshRenderer skinned = transform.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null)
            {
                plan.Errors.Add(PathOf(root, transform) +
                    ": SkinnedMeshRenderer geometry is not representable in ProjectMER. Rebuild it from supported primitives or mark this subtree Ignore.");
                return MerBlockKind.Empty;
            }

            MeshFilter meshFilter = transform.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                if (TryMapPrimitive(meshFilter.sharedMesh, out _))
                    return MerBlockKind.Primitive;

                plan.Errors.Add(PathOf(root, transform) + ": mesh '" + meshFilter.sharedMesh.name +
                    "' is not one of Unity's six built-in primitives. ProjectMER cannot serialize arbitrary mesh vertices; add metadata and choose Empty only for a marker, or mark the subtree Ignore.");
                return MerBlockKind.Empty;
            }

            if (transform.GetComponent<Renderer>() != null)
            {
                plan.Errors.Add(PathOf(root, transform) +
                    ": Renderer has no supported built-in MeshFilter. ProjectMER cannot serialize this geometry.");
            }

            return MerBlockKind.Empty;
        }

        private static void ResolvePrimitiveType(Transform root, ExportNode node, ExportPlan plan)
        {
            if (node.Metadata != null && node.Metadata.OverridePrimitiveType)
            {
                node.PrimitiveType = node.Metadata.PrimitiveType;
                return;
            }

            MeshFilter meshFilter = node.Transform.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null ||
                !TryMapPrimitive(meshFilter.sharedMesh, out MerPrimitiveType primitiveType))
            {
                plan.Errors.Add(PathOf(root, node.Transform) +
                    ": Primitive export needs a recognized built-in mesh or an explicit Primitive Type override.");
                node.PrimitiveType = MerPrimitiveType.Cube;
                return;
            }

            node.PrimitiveType = primitiveType;

            Renderer renderer = node.Transform.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 1)
            {
                plan.Warnings.Add(PathOf(root, node.Transform) +
                    ": ProjectMER primitives have one color; only the first usable material color will be exported.");
            }
        }

        private static void ValidateLight(Transform root, ExportNode node, ExportPlan plan)
        {
            Light light = node.Transform.GetComponent<Light>();
            if (light == null)
            {
                plan.Errors.Add(PathOf(root, node.Transform) + ": Light export requires a Unity Light component.");
                return;
            }

            // Unity and ProjectMER use Spot=0, Directional=1, Point=2. Area-like types are not AdminToy lights.
            if ((int)light.type < 0 || (int)light.type > 2)
            {
                plan.Errors.Add(PathOf(root, node.Transform) + ": Unity light type '" + light.type +
                    "' is not supported by the ProjectMER LightSourceToy. Use Spot, Directional, or Point.");
            }

            if (!IsFinite(light.intensity) || !IsFinite(light.range) || !IsFinite(light.spotAngle) ||
                !IsFinite(light.innerSpotAngle) || !IsFinite(light.shadowStrength))
            {
                plan.Errors.Add(PathOf(root, node.Transform) + ": light properties contain NaN or infinity.");
            }
        }

        private static void ValidateText(Transform root, ExportNode node, ExportPlan plan)
        {
            if (node.Metadata == null)
            {
                plan.Errors.Add(PathOf(root, node.Transform) + ": Text export requires ProjectMerExportMetadata.");
                return;
            }

            Vector2 size = node.Metadata.DisplaySize;
            if (!IsFinite(size.x) || !IsFinite(size.y) || size.x <= 0f || size.y <= 0f)
                plan.Errors.Add(PathOf(root, node.Transform) + ": text Display Size must contain two positive finite values.");
        }

        private static void ValidateTransform(Transform root, ExportNode node, ExportPlan plan)
        {
            Vector3 position = node.Transform.localPosition;
            Vector3 rotation = node.Transform.localEulerAngles;
            Vector3 scale = node.Transform.localScale;
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z) ||
                !IsFinite(rotation.x) || !IsFinite(rotation.y) || !IsFinite(rotation.z) ||
                !IsFinite(scale.x) || !IsFinite(scale.y) || !IsFinite(scale.z))
            {
                plan.Errors.Add(PathOf(root, node.Transform) + ": local transform contains NaN or infinity.");
            }

            if (Mathf.Abs(scale.x) < 0.000001f || Mathf.Abs(scale.y) < 0.000001f || Mathf.Abs(scale.z) < 0.000001f)
                plan.Warnings.Add(PathOf(root, node.Transform) + ": near-zero scale may make this block invisible or singular.");
        }

        private static void AssignObjectIds(ExportPlan plan)
        {
            if (plan.Nodes.Count == 0)
                return;

            HashSet<int> used = new HashSet<int> { 0 };
            plan.Nodes[0].ObjectId = 0;

            ProjectMerExportMetadata rootMetadata = plan.Nodes[0].Metadata;
            if (rootMetadata != null && rootMetadata.ObjectId > 0)
                plan.Warnings.Add("Root: Object ID override is ignored; RootObjectId is always 0.");
            if (rootMetadata != null && rootMetadata.ObjectId < -1)
                plan.Errors.Add("Root: Object ID must be -1 (automatic) or 0.");

            for (int i = 1; i < plan.Nodes.Count; i++)
            {
                ProjectMerExportMetadata metadata = plan.Nodes[i].Metadata;
                if (metadata == null || metadata.ObjectId == -1)
                    continue;
                if (metadata.ObjectId <= 0)
                {
                    plan.Errors.Add("Object ID override on '" + plan.Nodes[i].Transform.name +
                        "' must be at least 1, or -1 for automatic assignment.");
                    continue;
                }
                if (!used.Add(metadata.ObjectId))
                {
                    plan.Errors.Add("Duplicate Object ID " + metadata.ObjectId.ToString(CultureInfo.InvariantCulture) +
                        " on '" + plan.Nodes[i].Transform.name + "'.");
                    continue;
                }
                plan.Nodes[i].ObjectId = metadata.ObjectId;
            }

            int nextId = 1;
            for (int i = 1; i < plan.Nodes.Count; i++)
            {
                if (plan.Nodes[i].ObjectId >= 0)
                    continue;
                while (used.Contains(nextId))
                    nextId++;
                plan.Nodes[i].ObjectId = nextId;
                used.Add(nextId);
                nextId++;
            }
        }

        private static bool TryMapPrimitive(Mesh mesh, out MerPrimitiveType primitiveType)
        {
            string name = mesh.name == null ? string.Empty : mesh.name.Trim();
            int suffix = name.IndexOf(' ');
            if (suffix >= 0)
                name = name.Substring(0, suffix);

            return Enum.TryParse(name, true, out primitiveType) &&
                (int)primitiveType >= (int)MerPrimitiveType.Sphere &&
                (int)primitiveType <= (int)MerPrimitiveType.Quad;
        }

        private static string WriteJson(ExportPlan plan)
        {
            StringBuilder json = new StringBuilder(Math.Max(1024, plan.Nodes.Count * 420));
            json.Append("{\n  \"RootObjectId\": 0,\n  \"Blocks\": [\n");
            for (int i = 0; i < plan.Nodes.Count; i++)
            {
                WriteBlock(json, plan.Nodes[i]);
                json.Append(i + 1 == plan.Nodes.Count ? "\n" : ",\n");
            }
            json.Append("  ]\n}\n");
            return json.ToString();
        }

        private static void WriteBlock(StringBuilder json, ExportNode node)
        {
            Transform transform = node.Transform;
            ProjectMerExportMetadata metadata = node.Metadata;
            json.Append("    {\n");
            WriteStringProperty(json, 6, "Name", transform.name, true);
            WriteNumberProperty(json, 6, "ObjectId", node.ObjectId, true);
            WriteNumberProperty(json, 6, "ParentId", node.ParentId, true);
            WriteStringProperty(json, 6, "AnimatorName", metadata == null ? string.Empty : metadata.AnimatorName, true);
            WriteVector3Property(json, 6, "Position", transform.localPosition, true);
            WriteVector3Property(json, 6, "Rotation", transform.localEulerAngles, true);
            WriteVector3Property(json, 6, "Scale", transform.localScale, true);
            WriteNumberProperty(json, 6, "BlockType", BlockTypeOf(node.Kind), true);
            json.Append("      \"Properties\": {\n");

            switch (node.Kind)
            {
                case MerBlockKind.Primitive:
                    WriteNumberProperty(json, 8, "PrimitiveType", (int)node.PrimitiveType, true);
                    WriteStringProperty(json, 8, "Color", ColorOf(node), true);
                    WriteNumberProperty(json, 8, "PrimitiveFlags", FlagsOf(node), true);
                    WriteBoolProperty(json, 8, "Static", IsStatic(node), false);
                    break;
                case MerBlockKind.Light:
                    Light light = transform.GetComponent<Light>();
                    WriteStringProperty(json, 8, "Color", ColorOf(node), true);
                    WriteFloatProperty(json, 8, "Intensity", light.intensity, true);
                    WriteFloatProperty(json, 8, "Range", light.range, true);
                    WriteNumberProperty(json, 8, "ShadowType", (int)light.shadows, true);
                    WriteFloatProperty(json, 8, "ShadowStrength", light.shadowStrength, true);
                    WriteNumberProperty(json, 8, "LightType", (int)light.type, true);
                    WriteNumberProperty(json, 8, "Shape", metadata == null ? 0 : (int)metadata.LightShape, true);
                    WriteFloatProperty(json, 8, "SpotAngle", light.spotAngle, true);
                    WriteFloatProperty(json, 8, "InnerSpotAngle", light.innerSpotAngle, true);
                    WriteBoolProperty(json, 8, "Static", IsStatic(node), false);
                    break;
                case MerBlockKind.Text:
                    WriteStringProperty(json, 8, "Text", metadata.Text ?? string.Empty, true);
                    WriteVector2Property(json, 8, "DisplaySize", metadata.DisplaySize, true);
                    WriteBoolProperty(json, 8, "Static", IsStatic(node), false);
                    break;
                default:
                    WriteBoolProperty(json, 8, "Static", IsStatic(node), false);
                    break;
            }

            json.Append("      }\n    }");
        }

        private static int BlockTypeOf(MerBlockKind kind)
        {
            switch (kind)
            {
                case MerBlockKind.Primitive: return BlockPrimitive;
                case MerBlockKind.Light: return BlockLight;
                case MerBlockKind.Text: return BlockText;
                default: return BlockEmpty;
            }
        }

        private static int FlagsOf(ExportNode node)
        {
            if (node.Metadata != null)
            {
                return (node.Metadata.Collidable ? FlagCollidable : 0) |
                    (node.Metadata.Visible ? FlagVisible : 0);
            }

            Renderer renderer = node.Transform.GetComponent<Renderer>();
            return renderer != null && renderer.enabled ? FlagVisible : 0;
        }

        private static bool IsStatic(ExportNode node)
        {
            return node.Metadata == null || node.Metadata.Static;
        }

        private static string ColorOf(ExportNode node)
        {
            if (node.Metadata != null && node.Metadata.OverrideColor)
                return "#" + ColorUtility.ToHtmlStringRGBA(node.Metadata.Color);

            if (node.Kind == MerBlockKind.Light)
            {
                Light light = node.Transform.GetComponent<Light>();
                return "#" + ColorUtility.ToHtmlStringRGBA(light.color);
            }

            Renderer renderer = node.Transform.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterials != null)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                        continue;
                    if (material.HasProperty("_BaseColor"))
                        return "#" + ColorUtility.ToHtmlStringRGBA(material.GetColor("_BaseColor"));
                    if (material.HasProperty("_Color"))
                        return "#" + ColorUtility.ToHtmlStringRGBA(material.GetColor("_Color"));
                }
            }

            return "#FFFFFFFF";
        }

        private static void LogReport(string rootName, ExportPlan plan)
        {
            foreach (string warning in plan.Warnings)
                Debug.LogWarning("ProjectMER: " + warning);
            foreach (string error in plan.Errors)
                Debug.LogError("ProjectMER: " + error);

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "ProjectMER validation for '{0}': {1} blocks, {2} warning(s), {3} error(s).",
                rootName, plan.Nodes.Count, plan.Warnings.Count, plan.Errors.Count));
        }

        private static string PathOf(Transform root, Transform current)
        {
            Stack<string> parts = new Stack<string>();
            Transform cursor = current;
            while (cursor != null)
            {
                parts.Push(cursor.name);
                if (cursor == root)
                    break;
                cursor = cursor.parent;
            }
            return string.Join("/", parts.ToArray());
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "schematic" : value;
        }

        private static void WriteStringProperty(StringBuilder json, int indent, string name, string value, bool comma)
        {
            Indent(json, indent).Append('"').Append(name).Append("\": \"").Append(Escape(value ?? string.Empty)).Append('"');
            json.Append(comma ? ",\n" : "\n");
        }

        private static void WriteNumberProperty(StringBuilder json, int indent, string name, int value, bool comma)
        {
            Indent(json, indent).Append('"').Append(name).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            json.Append(comma ? ",\n" : "\n");
        }

        private static void WriteFloatProperty(StringBuilder json, int indent, string name, float value, bool comma)
        {
            Indent(json, indent).Append('"').Append(name).Append("\": ").Append(FormatFloat(value));
            json.Append(comma ? ",\n" : "\n");
        }

        private static void WriteBoolProperty(StringBuilder json, int indent, string name, bool value, bool comma)
        {
            Indent(json, indent).Append('"').Append(name).Append("\": ").Append(value ? "true" : "false");
            json.Append(comma ? ",\n" : "\n");
        }

        private static void WriteVector3Property(StringBuilder json, int indent, string name, Vector3 value, bool comma)
        {
            Indent(json, indent).Append('"').Append(name).Append("\": {\n");
            WriteFloatProperty(json, indent + 2, "x", value.x, true);
            WriteFloatProperty(json, indent + 2, "y", value.y, true);
            WriteFloatProperty(json, indent + 2, "z", value.z, false);
            Indent(json, indent).Append('}').Append(comma ? ",\n" : "\n");
        }

        private static void WriteVector2Property(StringBuilder json, int indent, string name, Vector2 value, bool comma)
        {
            Indent(json, indent).Append('"').Append(name).Append("\": {\n");
            WriteFloatProperty(json, indent + 2, "x", value.x, true);
            WriteFloatProperty(json, indent + 2, "y", value.y, false);
            Indent(json, indent).Append('}').Append(comma ? ",\n" : "\n");
        }

        private static StringBuilder Indent(StringBuilder json, int count)
        {
            return json.Append(' ', count);
        }

        private static string FormatFloat(float value)
        {
            if (Mathf.Abs(value) < 0.00000005f)
                value = 0f;
            return value.ToString("G9", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            StringBuilder escaped = new StringBuilder(value.Length + 8);
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': escaped.Append("\\\""); break;
                    case '\\': escaped.Append("\\\\"); break;
                    case '\b': escaped.Append("\\b"); break;
                    case '\f': escaped.Append("\\f"); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\t': escaped.Append("\\t"); break;
                    default:
                        if (character < 32)
                            escaped.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            escaped.Append(character);
                        break;
                }
            }
            return escaped.ToString();
        }
    }
}
