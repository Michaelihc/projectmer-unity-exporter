# Changelog

All notable changes to this package are documented here.

## [0.1.1] - 2026-08-18

### Fixed

- Accept Project MER colors both with and without a leading `#` during import.
- Preserve unsupported gameplay block types and their raw properties for re-export.
- Preserve unmodeled properties on supported blocks, including `MovementSmoothing`, light flicker data, and future Project MER fields, while merging Unity-edited values.

## [0.1.0] - 2026-08-16

### Added

- Import Project MER JSON schematics into an editable Unity hierarchy.
- Validate and export a selected Unity hierarchy as `.mer.json`.
- Automatic support for Unity's six built-in primitives.
- Project MER metadata for empty transforms, primitives, lights, text, ignored subtrees, stable object IDs, primitive flags, and static/movable objects.
- Validation for unsupported meshes, skinned meshes, light types, duplicate IDs, invalid transforms, and malformed hierarchy data.
- Editor tests for importer and exporter behavior.
- English and Simplified Chinese setup and usage documentation.
