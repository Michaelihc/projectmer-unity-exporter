# Project MER Unity Exporter

[简体中文](README.zh-CN.md)

An editor package for importing, authoring, validating, and exporting [Project MER (Map Editor Reborn)](https://github.com/Michal78900/ProjectMER) JSON schematics in Unity.

It turns a selected Unity hierarchy made from supported primitives, lights, text, and empty transforms into a `.mer.json` file. It can also load an existing Project MER JSON schematic back into Unity for editing.

> [!IMPORTANT]
> This is an independent community tool, not an official Project MER or Northwood Studios project. It does not install Project MER on your SCP:SL server.

## What it supports

- Unity built-in Sphere, Capsule, Cylinder, Cube, Plane, and Quad objects
- Empty GameObjects used as hierarchy roots, parents, or animation markers
- Spot, Directional, and Point lights
- Project MER text blocks configured through the metadata component
- Local position, rotation, scale, hierarchy, names, and stable object IDs
- Primitive color, visibility, collision, and static/movable flags
- Project MER animator names
- Importing existing Project MER JSON for editing or inspection
- Lossless pass-through of unmodeled properties and unsupported gameplay block types during import/re-export
- Strict validation before a file is written

## What it cannot export

- Imported FBX, OBJ, GLB, or other arbitrary meshes
- `SkinnedMeshRenderer` geometry
- Textures, UVs, custom shaders, or multiple material appearances
- Unity scripts, Animator Controllers, animation clips, particles, audio, or physics components
- Any asset that a normal SCP:SL client does not already know how to render

Project MER schematics describe networked SCP:SL objects; they do not contain arbitrary mesh vertices or an asset bundle. Rebuild or approximate custom models with the six supported primitives before exporting.

## Requirements

- Unity 2021.3 or newer
- Git installed if using Unity Package Manager's Git URL option
- A separate SCP:SL server with a compatible [Project MER](https://github.com/Michal78900/ProjectMER) installation for in-game use

The package depends on Unity's `com.unity.nuget.newtonsoft-json` package, which Unity Package Manager installs automatically.

## Installation

### Recommended: install from the tagged Git URL

1. Open your Unity project.
2. Select **Window → Package Manager**.
3. Click the **+** button.
4. Choose **Install package from git URL…**.
5. Enter:

   ```text
   https://github.com/Michaelihc/projectmer-unity-exporter.git#v0.1.1
   ```

6. Click **Install** and wait for Unity to finish compiling.
7. Confirm that **Tools → ProjectMER** appears in the Unity menu bar.

Pinning `#v0.1.1` keeps every team member on the same version. To follow the latest `main` branch instead, omit the suffix.

### Install by editing `Packages/manifest.json`

Add this entry inside the project's `dependencies` object:

```json
"com.scpsl.projectmer-authoring": "https://github.com/Michaelihc/projectmer-unity-exporter.git#v0.1.1"
```

Remember to add a comma to the preceding entry when required by JSON syntax.

### Install a downloaded local copy

1. Download and extract the repository.
2. In **Window → Package Manager**, click **+**.
3. Choose **Install package from disk…**.
4. Select the repository's `package.json` file.

Do not place a second copy under `Assets/` while the UPM version is installed; duplicate assembly definitions will cause compile errors.

## Create and export a schematic

### 1. Build a clean hierarchy

1. Create an empty GameObject and give it the schematic's desired name.
2. Keep the root at local position `(0, 0, 0)`, rotation `(0, 0, 0)`, and scale `(1, 1, 1)` unless an intentional root transform is required.
3. Add children with **GameObject → 3D Object** using Unity's built-in primitives.
4. Organize objects under empty GameObjects as needed.
5. Use each child's local transform to position it relative to its parent.

The selected root itself is exported as object ID `0`. Children receive deterministic IDs unless you explicitly assign IDs through metadata.

### 2. Configure Project MER-specific behavior

Select an object and add **ProjectMER → Export Metadata** in the Inspector's **Add Component** menu when you need an override.

`Block Kind` controls how the object is treated:

| Value | Behavior |
| --- | --- |
| `Auto` | Infers a built-in primitive, supported Unity Light, or empty transform. |
| `Empty` | Exports only the transform and hierarchy node. Useful for parents and markers. |
| `Primitive` | Exports a Project MER primitive. Enable `Override Primitive Type` if there is no recognizable built-in Unity mesh. |
| `Light` | Exports a Unity Light as a Project MER light block. |
| `Text` | Exports the metadata's text and display size. |
| `Ignore` | Excludes this object and its entire subtree. The selected root cannot be ignored. |

Other useful fields:

- `Object Id`: leave at `-1` for automatic assignment. Explicit child IDs must be unique and greater than zero.
- `Animator Name`: writes Project MER's animator-name field. This does not export a Unity animation clip or controller.
- `Override Color`: uses the metadata color instead of the first usable material color.
- `Visible`: enables the primitive's visible flag.
- `Collidable`: enables Project MER collision. This is deliberately opt-in.
- `Static`: disable this for parts that Project MER will move at runtime.
- `Light Shape`: selects the MER light-source shape metadata.
- `Text` and `Display Size`: configure a text block.

### 3. Validate

1. Select the hierarchy root in the Hierarchy window.
2. Choose **Tools → ProjectMER → Validate Selected Hierarchy**.
3. Read warnings and errors in the Console.
4. Fix every error before exporting.

Validation catches common failures such as custom meshes, skinned meshes, unsupported light types, duplicate IDs, missing parents, and non-finite transforms.

### 4. Export

1. Keep the hierarchy root selected.
2. Choose **Tools → ProjectMER → Export Selected Hierarchy…**.
3. Save the generated `<name>.mer.json` file.

No file is written if validation fails.

### 5. Install the schematic on a server

1. Install and start Project MER on the SCP:SL server by following the [official Project MER instructions](https://github.com/Michal78900/ProjectMER).
2. Copy the exported file into:

   ```text
   SCP Secret Laboratory/LabAPI/configs/ProjectMER/Schematics/
   ```

3. Restart the server if the schematic is not detected immediately.
4. Load or spawn the schematic using the commands supported by your installed Project MER version.

Project MER's current documentation places maps and schematics under `LabAPI/configs/ProjectMER`. Server commands can change between releases, so consult the documentation or Discord associated with your exact server plugin build.

## Import an existing Project MER schematic

1. Choose **Tools → ProjectMER → Load JSON…**.
2. Select a `.json` or `.mer.json` schematic.
3. The package creates an editable hierarchy in the current scene.
4. Inspect any warnings in the Console.

Supported blocks become Unity primitives, lights, text previews, or empty transforms. Unknown block types become transform-only placeholders with a warning, but their original block type and properties are stored in hidden metadata for re-export. Extra properties on supported blocks are also retained, so fields such as `MovementSmoothing`, light flicker settings, and future Project MER properties survive a round trip. Temporary preview materials are generated in memory and are not saved as project assets.

## Troubleshooting

### The `Tools → ProjectMER` menu is missing

- Wait for Unity compilation to finish.
- Open **Window → General → Console** and fix all compile errors, including unrelated project errors.
- Verify that the package appears in Package Manager.
- Remove duplicate copies from `Assets/` or from another local package path.
- If installed from Git, confirm that Git is available from the environment used to start Unity.

### Unity says it cannot add the package

- Use the exact URL including `.git`.
- Verify that GitHub is reachable from the machine.
- Try the tagged URL rather than `main`.
- As a fallback, download the release source archive and use **Install package from disk…**.

### Export reports that a mesh is unsupported

The object is not using one of Unity's original six built-in primitive meshes. Recreate it with built-in primitives, mark it as `Empty` only when it should be a marker, or set it to `Ignore` to omit the subtree. Changing an arbitrary mesh's name to “Cube” does not convert its geometry and is not a supported workflow.

### The color differs in SCP:SL

Project MER primitives have one color, while Unity renderers may have multiple materials and shader properties. The exporter uses the metadata override when enabled; otherwise it uses the first usable material color. Textures and shader graphs are not exported.

### Collision differs from Unity

Unity adds colliders automatically to built-in primitives, but this exporter treats Project MER collision as opt-in. Add the metadata component and enable `Collidable` on the objects that need collision. Avoid making every decorative primitive collidable, as that increases server and client workload.

### The imported hierarchy does not exactly match the original schematic

The importer is an authoring preview. Unsupported Project MER block types become placeholders and cannot be configured visually in Unity, although their source properties are preserved for re-export. Converting a placeholder to another block kind intentionally replaces its original type-specific data. Unity materials/lights cannot reproduce every SCP:SL rendering detail exactly. Always perform the final check on a private SCP:SL test server.

## Automation API

Editor scripts can export without opening a save dialog:

```csharp
using Scpsl.ProjectMer.Authoring.Editor;
using UnityEngine;

ProjectMerExportResult result = ProjectMerSceneExporter.ExportHierarchyToFile(
    rootGameObject,
    @"C:\schematics\example.mer.json");

if (!result.Success)
{
    foreach (string error in result.Errors)
        Debug.LogError(error);
}
```

Use `BuildHierarchyJson(rootGameObject)` when you need the validated JSON string without writing a file.

## Running tests

1. Open **Window → General → Test Runner**.
2. Select **EditMode**.
3. Enable package tests if your Unity version hides them by default.
4. Run the `Scpsl.ProjectMer.Authoring.Editor.Tests` assembly.

## License

[MIT](LICENSE)

SCP: Secret Laboratory, Project MER, Unity, and GitHub are trademarks or projects of their respective owners. This repository is not affiliated with or endorsed by Northwood Studios, the Project MER maintainers, or Unity Technologies.
