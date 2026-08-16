# Contributing

Issues and pull requests are welcome.

Before opening a pull request:

1. Keep runtime metadata independent of SCP:SL server assemblies.
2. Keep Unity editor-only code under `Editor/`.
3. Add or update EditMode tests for behavior changes.
4. Run the package's EditMode tests in the oldest Unity version you intend to support.
5. Do not claim support for arbitrary imported meshes: normal SCP:SL clients can only render the supported networked objects represented by Project MER.

Please include the Unity version, Project MER version, a minimal schematic or hierarchy, and the complete Console error when reporting a bug.
