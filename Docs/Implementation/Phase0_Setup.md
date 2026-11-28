# Phase 0 - Project setup

Part of the [implementation plan](README.md) (progress, rules, how the plan is kept up to date). Section numbers like 6.2 refer to the [design](../GrassPrototype_Design.md).

**Goal:** an empty, measurable project.
- [x] Git repository with a Unity `.gitignore` (`Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`, generated `.csproj`/`.slnx`) and Git LFS for large binary assets.
- [x] Unity 6.6 (6000.6.10f1), URP 17.6, Forward+ renderer, Linear colour, DX12 (Vulkan checked).
- [x] Quality tiers `RTX3060Ti` and `RTX5070Ti` + a `ScatterQuality` asset (density multiplier, ring distances, shadow mode, generation budget).
- [x] The scatter system is an embedded package (Unity Package Manager, UPM) in `Packages/com.gaiagames.scatter`, with no dependency on any terrain code, so the terrain project can import it as is. Prototype-only content (mock terrain, test scenes, player) stays in `Assets`.
- [x] Assemblies `Scatter.Runtime`, `Scatter.Editor`, `Scatter.Tests` (in the package), `Prototype` (in `Assets`). Package folders: `Runtime/{Ground,Chunks,Compute,Rendering,Wind,Interaction,Diagnostics}`, `Shaders`.
- [x] Debug HUD frame (design 12.1): FPS, CPU/GPU frame time, GC allocated in frame; scatter timing rows ready for later phases. CSV logger (preallocated buffer).
- [x] `.editorconfig` and `Docs/CodingRules.md` in place; Incremental GC enabled; Enter Play Mode without domain reload.
- [x] Unity Test Framework: `Scatter.Tests` (Edit Mode) and `Scatter.Tests.PlayMode` assemblies; command-line run `Unity.exe -batchmode -projectPath <path> -runTests -testPlatform EditMode -testResults <file>.xml`, without `-nographics` (GPU tests need a graphics device).

**Exit:** empty scene builds; HUD shows FPS and GPU time; an empty test runs from the command line.

**Notes**
- Package `com.gaiagames.scatter`. The HUD code is in `Runtime/Diagnostics` (namespace `Scatter.Diagnostics`): a `Scatter.Debug` namespace would hide `UnityEngine.Debug` in every `Scatter.*` file.
- Extra assembly `Prototype.Editor` (`Assets/Prototype/Editor`) for prototype editor scripts. Windows build from the command line: `Unity.exe -batchmode -quit -projectPath <path> -executeMethod Prototype.Editor.PrototypeBuild.BuildWindows -logFile <file>` (output in `Builds/Windows`, ignored by git).
- Quality: the template `Mobile` tier and its URP assets are removed. Both tiers use `PC_RPAsset` for now (it is also the default render pipeline). `ScatterQuality_RTX3060Ti` and `ScatterQuality_RTX5070Ti` are in `Assets/Settings/Scatter`. `ScatterQuality` also holds the per-tier targets of design 12 (frame time, scatter GPU, generation GPU, scatter CPU main), which the HUD uses to show values in red. All values are starting points for Phase 9.
- Windows graphics APIs: Direct3D12 first, Vulkan second. Player Settings "Frame Timing Stats" is enabled: players need it for the CPU and GPU times.
- Player Settings: company name `GaiaGames`; "Strict shader variant matching" enabled, so a variant missing in a player draws with the error shader and logs its keywords instead of silently falling back. Used to check the BRG variants in Phase 1.
- HUD: UI Toolkit label drawn by a **Panel Renderer** (UI Document is superseded in 6000.6). The HUD gets the panel root through `RegisterUIReloadCallback`, which runs at once if the panel is already loaded, and attaches its label again after each reload. The prefab `ScatterDebugHud.prefab`, its panel settings and its theme ship in the package (menu GameObject > Scatter > Debug HUD). F1 shows or hides it; F2 starts or stops CSV recording (`ScatterCsvLogger`, written to `persistentDataPath/ScatterLogs`, also on disable). The text is rebuilt at 4 Hz, only while visible. Sampling and recording do not allocate (tests).
- "GC Allocated In Frame" is not provided in release players (the HUD shows "-"). Measure GC allocation in the Editor or in a development build (Phase 9).
- `FlyCamera` (WASD/QE, right mouse to look, Shift, wheel for speed) added to `Prototype` for the test scenes. Scene `Assets/Scenes/Bootstrap.unity` (camera, light, ground plane, HUD) is the only scene in the build.
- Git LFS rules are in `.gitattributes`; the template's `Assets/TutorialInfo/Icons/URP.png`, committed before them, stays in plain git.
- Test GPU corrected from RTX 3060 to **RTX 3060 Ti** (the development PC) in the design and the docs; the tier is renamed `RTX3060Ti` (quality level and `ScatterQuality_RTX3060Ti`, same GUID). Its targets are scaled by about 1.25 (3060 Ti vs 3060: +36% shader cores, +24% bandwidth): scatter GPU 2.5 ms, generation GPU 0.4 ms, stress scene >= 4M visible blades. The whole frame stays >= 60 FPS (product target); CPU budgets are unchanged.
- Validation on the development PC (RTX 3060 Ti, D3D12): Edit Mode 16/16, Play Mode 7/7 in the Editor and Play Mode 7/7 in a Windows player (`-testPlatform StandaloneWindows64`) passed from the command line; the Windows player builds with 0 errors and 0 warnings and shows the HUD with FPS, CPU and GPU times.
- The player log reports `Hidden/Universal Render Pipeline/DBufferClear shader is not supported` at startup (URP template content, decals unused). To check with DepthNormals and decals in Phase 8.
