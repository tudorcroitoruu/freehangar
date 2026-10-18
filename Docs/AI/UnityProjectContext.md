# Unity project context

Focused rendering inspection, 2026-10-18.

- Project: D:/Projects/freehangar; Unity 6000.3.20f1.
- URP 17.3.0 is installed. Graphics and Mobile quality now select Assets/Settings/Mobile_RPAsset.asset, with Assets/Settings/Mobile_Renderer.asset at default index 0.
- Graphics URP global settings resolve to Assets/Settings/UniversalRenderPipelineGlobalSettings.asset.
- Assets/Settings/URP contains additional copies with distinct GUIDs and internally remapped references. Keep GUIDs unique.
- Old HDRP resources remain in Assets/HDRPDefaultResources, but are not selected as the project pipeline. Material and shader migration has not been audited.
- Input System 1.19.0, uGUI 2.0.0, AI Navigation 2.0.13, and Unity Test Framework 1.6.0 are installed (Packages/manifest.json).
- Enabled build scene: Assets/Scenes/ExampleScene.unity (ProjectSettings/EditorBuildSettings.asset). Other scenes include Bootstrap, LoadingScene, Menu, and Gameplay; runtime startup flow was not investigated.
- First-party scripts: Assets/_Scripts; editor tools: Assets/Editor. Architecture and assembly boundaries were outside this narrow repair.
- Unity Editor is running, but no general Unity Editor automation/Console connection is available. Validation completed at serialized configuration level only; runtime rendering and player builds remain unverified.
- Working tree already contained extensive unrelated additions/deletions and settings changes before this repair; preserve them.

Evidence: ProjectSettings/ProjectVersion.txt, GraphicsSettings.asset, QualitySettings.asset, EditorBuildSettings.asset; Packages/manifest.json; Assets/Settings pipeline, renderer, global-settings assets and metadata. Baseline Editor.log contained repeated missing default renderer errors.