using UnityEngine;

namespace Scpsl.ProjectMer.Authoring
{
    public enum MerBlockKind
    {
        Auto,
        Empty,
        Primitive,
        Light,
        Text,
        Ignore,
    }

    // Values deliberately match UnityEngine.PrimitiveType and ProjectMER.
    public enum MerPrimitiveType
    {
        Sphere = 0,
        Capsule = 1,
        Cylinder = 2,
        Cube = 3,
        Plane = 4,
        Quad = 5,
    }

    // Values match AdminToys.LightShape in ProjectMER's serialized properties.
    public enum MerLightShape
    {
        Cone = 0,
        Pyramid = 1,
        Box = 2,
    }

    /// <summary>
    /// Optional overrides for a GameObject exported to a ProjectMER schematic.
    /// Built-in primitives and Unity Light components normally need no metadata.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ProjectMER/Export Metadata")]
    public sealed class ProjectMerExportMetadata : MonoBehaviour
    {
        [Tooltip("Auto infers built-in primitives, supported Unity lights, or an empty transform.")]
        public MerBlockKind BlockKind = MerBlockKind.Auto;

        [Tooltip("Optional stable ID. Use -1 for deterministic automatic assignment. The selected root is always ID 0.")]
        public int ObjectId = -1;

        public string AnimatorName = string.Empty;

        [Header("Primitive")]
        [Tooltip("Use this type instead of inferring it from the built-in MeshFilter mesh.")]
        public bool OverridePrimitiveType;
        public MerPrimitiveType PrimitiveType = MerPrimitiveType.Cube;

        [Tooltip("Use the color below instead of the Renderer material color.")]
        public bool OverrideColor;
        public Color Color = Color.white;

        [Tooltip("MER collision is opt-in because Unity built-in primitives receive colliders automatically.")]
        public bool Visible = true;
        public bool Collidable;

        [Tooltip("Sets ProjectMER's network Static property. Disable for parts moved by runtime animation.")]
        public bool Static = true;

        [Header("Light")]
        public MerLightShape LightShape = MerLightShape.Cone;

        [Header("Text")]
        [TextArea(2, 8)]
        public string Text = "Custom Text";
        public Vector2 DisplaySize = new Vector2(200f, 50f);
    }
}
