# GPU Scatter Prototype - Implementation Plan

Companion to `GrassPrototype_Design.md` (section numbers below refer to it).
Phases are ordered so the riskiest parts are proven first. Each phase ends with something visible and an exit check. One branch/commit series per phase.

## Progress

| Phase | Status |
|---|---|
| 0 - Project setup | Done |
| 1 - Spikes | Done |
| 1b - Cost investigation | Done |
| 2 - Core pipeline on a flat ground | Not started |
| 3 - Layers | Not started |
| 4 - Terrain adapter and streaming | Not started |
| 5 - Mesh ground source | Not started |
| 6 - Wind | Not started |
| 7 - Player interaction | Not started |
| 8 - Lighting, shadows, motion vectors | Not started |
| 9 - Measurement and tuning | Not started |
| 10 - Smoke tests and report | Not started |

Status values: Not started, In progress, Done, Blocked (with the reason).

How this document is kept up to date (in the same commit as the code):
- Tick an item `[x]` when it is implemented **and** its tests pass. Update the phase status above.
- When the work differs from the plan, or a phase produces findings (spike results, measurements), add a **Notes** list at the end of that phase: what changed and why.
- If a change affects a design decision, update the design document too (the relevant section and the decision table 16.1). The design describes what is true now; git history keeps the past.
- Update `Docs/ProjectGuide.md` (what exists and how it works) and, when a phase status changes, the status line of `README.md`.
- Update the package README (`Packages/com.gaiagames.scatter/README.md`) when the package's requirements, contents or public usage change.
- New work discovered on the way is added as a new item in the right phase, not kept elsewhere.

---

## Phase 0 - Project setup
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

---

## Phase 1 - Spikes (prove the risky rendering path)
**Goal:** confirm that the draw path of design section 6.3 works before building on it, and choose the draw command type for each usage.

Only the indirect command types are candidates: the instance count is decided on the GPU and never read back (design 10.2), so the direct `BatchDrawCommand` and `BatchDrawCommandProcedural` are excluded. Usages to decide:

| Usage | Candidates |
|---|---|
| Blade layers (grass, flowers, moss): millions of blades of 3 to 7 vertices | Mesh indirect, 1 blade per instance; procedural indirect, 1 blade per instance; procedural indirect indexed, K blades per instance |
| Object layers (small rocks, bushes): fewer instances, meshes of a few hundred vertices | Mesh indirect |
| Shadow, depth and motion-vector passes | The command of the colour pass, sent again for each view |

Spike content: 1M blades on a flat grid, per-blade data (position, yaw) in a custom StructuredBuffer filled at startup. One spike cull compute per frame resets the args, picks LOD0 or LOD1 per blade by distance, reserves entries with an atomic add (with a region overflow guard) and writes 4-byte indices into **one visible list with two regions** (LOD0, LOD1).

- [x] **S1 - BRG:** a `BatchRendererGroup` draws the spike content three ways: mesh indirect (`BatchDrawCommandIndirect`), procedural indirect with 1 blade per instance, procedural indirect indexed with K blades per instance (blade index from `SV_VertexID`). Plus one Object layer (a simple imported mesh) drawn with mesh indirect. The hand-written URP shader reads the blade data from the custom StructuredBuffer through the visible list.
  Check, for each variant: the two regions are read through `visibleInstancesBufferWindowOffset` (or the shader's own offset for procedural); `UniversalForward`, `ShadowCaster` (shadow cascades with the split visibility mask), `DepthOnly`, `DepthNormals` (SSAO and depth priming on) and `MotionVectors` passes work; whether per-instance BRG metadata or the DOTS instancing variant is needed.
- [x] **S2 - Baseline:** the same Blade variants with `Graphics.RenderMeshIndirect` and `Graphics.RenderPrimitivesIndexedIndirect`, same compute output.
- [x] **S3 - Timing:** GPU time of the cull compute and of the draws, per variant and per backend, at the same blade count, on the RTX 3060 Ti; sweep of K for the procedural indexed variant.
- [x] Graphics Settings: check that "BatchRendererGroup Variants" keeps the DOTS instancing variants in players (the default strips them when Entities Graphics is not installed); check in a Windows build.
- [x] Debug: HUD cull and colour timing rows fed by the spike; keys to switch backend, draw variant and blade count.
- [x] Tests: cull compute vs a C# reference (region counts and offsets, args reset, overflow guard); no allocation in the per-frame entry points (dispatch submission, `OnPerformCulling`); Play Mode render of each backend and variant to a render texture, in the Editor and in a Windows player: blade pixels present, no error shader.

**Exit:** a findings note: what works, any API limitation, and a decision table (usage, command type, backend, K) confirming BRG as main path (or fallback to RenderMeshIndirect). If Blade layers become procedural, the change to design 6.3, Phase 2 item 1 and the 16.1 row is presented at the review. **Stop and review before Phase 2.**

**Notes**
- Spike code in the package: `Runtime/Rendering/Spike` (namespace `Scatter.Rendering.Spike`, replaced in Phase 2), `Shaders/Spike`; the blade mesh factory `Runtime/Rendering/BladeMeshFactory.cs` is kept for Phase 2. Scene `Assets/Scenes/Spike_Draw.unity` (second scene of the build), created with **Scatter Prototype > Create Spike Content**.
- Spike content: 4M blades placed on an R2 sequence (any prefix is evenly spread, so the active count can change), plus 16k small rocks for the Object layer. **Three regions** in one visible list: blade LOD0, blade LOD1, objects.
- **BRG indirect with a custom per-instance buffer works** for the three blade draw types and the Object layer (design 16.2 item): the batch has an empty metadata list, the shader reads its own StructuredBuffer with `GetDOTSInstanceIndex()`. For K blades per instance, the shader reads the K entries directly from the visible list BRG binds (`GetDOTSIndirectVisibleIndex()` and `LoadDOTSIndirectInstanceIndex`).
- **`visibleInstancesBufferWindowOffset` cannot be used on D3D12 and Vulkan.** There BRG works with raw buffers (`BatchRendererGroup.BufferTarget == RawBuffer`) and rejects a command with a window: it prints "BatchRendererGroup had non-zero ConstantBuffer mode parameters when BufferTarget is RawBuffer" and draws nothing. The window is for the constant buffer mode (OpenGL ES, WebGL). Regions are addressed with **`visibleOffset`**, as the GPU Resident Drawer does. Design 6.3 updated; the play mode test `Render_WindowOffset_IsRejectedInRawBufferMode` keeps the finding checked.
- **The default "BatchRendererGroup Variants" setting strips the `DOTS_INSTANCING_ON` variants from players**: the first player run failed with "variant DOTS_INSTANCING_ON not found" (strict variant matching). Set to **Keep All** (`m_BrgStripping: 2`; 1 does not keep them) by **Scatter Prototype > Create Spike Content**: the editor property `EditorGraphicsSettings.batchRendererGroupShaderStrippingMode` is read-only, so the tool writes the serialized Graphics Settings. Design 6.3 updated.
- `Scatter.Runtime` and `Scatter.Tests` allow unsafe code: BRG draw commands are written through native pointers (`BatchCullingOutputDrawCommands`). `InternalsVisibleTo("Scatter.Tests")` gives the tests the body of the culling callback.
- Draw arguments are written by a small finalize kernel after the cull (one thread per draw), for every draw type: mesh and procedural indexed use 5 uints, non-indexed procedural the first 4. Draws are **80 bytes apart** in the arguments buffer: the `Graphics.Render*Indirect` calls (S2) take a command index, not a byte offset, in 20-byte (`IndirectDrawIndexedArgs`) or 16-byte (`IndirectDrawArgs`) commands, and 80 is a multiple of both. BRG takes a byte offset and is not affected.
- S2 baseline (`SpikeIndirectRenderer`, F9 switches the backend): the same materials and cull output as BRG, drawn with `Graphics.RenderMeshIndirect` (mesh and rocks), `RenderPrimitivesIndirect` (procedural) and `RenderPrimitivesIndexedIndirect` (K blades per instance), queued every frame after the cull. The shaders read the visible list through the material (`_SpikeVisibleList` from `_RegionStart`) in their non-DOTS variant; `SV_InstanceID` starts at 0 because the arguments set the start instance to 0. Shadows and motion vectors through `RenderParams`. For K blades per instance it rounds the instance count up and pads the region with an empty entry (`0xFFFFFFFF`) that the vertex shader collapses; region capacities are multiples of the largest K (64).
- **The two backends first shaded differently** (seen in the Editor: Indirect lighter). The play mode test `Render_BothBackends_ShadeTheSame` now renders both and compares the mean colour per term, with two debug views added for it (F8: main light only, ambient only). Two shader mistakes, no Unity limitation:
  - BRG had no ambient: `SampleSH` was called in the fragment shader, but under BRG it reads values sampled by `UNITY_SETUP_INSTANCE_ID` (`unity_DOTS_Sampled_SH*`), which only ran in the vertex shader. Fix: ambient evaluated per vertex (both sides of the blade), right after the setup. URP Lit runs the setup in the fragment shader instead; per vertex is cheaper for grass.
  - The Indirect backend had no main light once `distanceAttenuation` was applied: in Forward+ URP does not fill the per-object `unity_LightData`, and `GetMainLight()` ignores it only on the cluster path. Fix: the forward pass declares `_CLUSTER_LIGHT_LOOP`, as URP Lit does. Under BRG the value comes from an instance flag, which is why BRG had the main light.
  Design 8.1 and 8.2 updated.
- S2 passes checked together in the Editor with the Indirect backend: same picture as BRG for every debug view, our 3 draws in the shadow cascades (development player), DepthNormals prepass, depth priming (ZTest Equal) and MotionVectors (TAA, no trails).
- **Known difference, players only:** with the Indirect backend, blades receive slightly less main light shadow from other blades (shadow term 0.979 vs 0.959 on average in the test view, about 3,000 pixels of 65,536, rocks identical). The test is repeatable (two BRG renders are identical), diffuse and ambient terms match, and both backends submit the same draws in every cascade (checked in the Frame Debugger of a development player) with the same instance counts: the GPU work is the same, only the depth the blade casters leave in the shadow map differs. In the Editor both backends match exactly. Not investigated further: BRG is the main path; Phase 8 item 2 checks it. The cases of `Render_BothBackends_ShadeTheSame` that contain the shadow term (`Off`, `MainLight`, `Shadow`) are ignored in players with this reason; `Ambient` and `Diffuse` stay strict.
- **S3 measurements** (2026-11-27, files and conditions in `Docs/Measurements/Phase1`; reference run `spike_benchmark_full_20261127_232809.csv`, values below from the first run, within 0.2 ms): RTX 3060 Ti, D3D12, 2560x1440, development player, depth priming Auto, SSAO on, 4 shadow cascades (50 m), no TAA. 300 measured frames per case, median. 4M blades on 150 m x 150 m, about 2.1M visible (no density falloff in the spike: every blade up to 120 m is full size). Scatter GPU = frame GPU minus the run without scatter (0.96 ms):

  | View 0 (1.8 m high), 4M blades | BRG | Indirect |
  |---|---|---|
  | Mesh indirect, 1 blade per instance | 14.7 ms | 14.8 ms |
  | Procedural indirect, 1 blade per instance | 16.9 ms | 17.2 ms |
  | Procedural indexed indirect, K = 16 | **11.9 ms** | 12.1 ms |

  - K sweep (BRG, 4M, view 0): K2 16.7, K4 15.3, K8 13.9, K16 11.9, K32 12.0, K64 12.0 ms: a plateau from K = 16.
  - Procedural indexed K16 is about 19% faster than mesh indirect: drawing one blade per instance wastes vertex work. Non-indexed procedural is the slowest: 15 vertex shader runs per blade instead of 7 (no vertex reuse).
  - BRG is 0.1 to 0.3 ms faster than the Indirect baseline in every case. Draw submission CPU is 0.003 to 0.015 ms for both; spike CPU (cull recording) 0.03 to 0.09 ms. Cull compute GPU 0.09 ms (1M) to 0.22 ms (4M).
  - View 1 (10 m high) is 0.5 to 0.7 ms slower, same ranking. 99th percentile within 0.5 ms of the median everywhere.
  - Cost is not proportional to the blade count: 1M blades (0.53M visible) already cost 6.7 ms with mesh indirect, 4M (2.1M visible) 14.7 ms.
  - **Far above the budget of design 12** (scatter GPU 2.5 ms): about 12 ms for 2.1M visible blades. Every visible blade is drawn 6 times (4 shadow cascades, DepthNormals, colour), at full size up to 120 m.
  - The URP pass GPU times (`UrpPassTimings`) are empty in this run: it listened to the Render Graph pass markers, which have no GPU time in players (next note).
- **Breakdown of the scatter cost** (reference run, BRG, view 0; scatter cost = value minus the run without scatter, which has 0.01 ms of shadows and 0.4 ms of depth + colour):

  | | Shadows (4 cascades) | Depth + colour | Cull | Total |
  |---|---|---|---|---|
  | Mesh indirect, 4M (2.1M visible) | 8.4 ms | 6.4 ms | 0.22 ms | 14.8 ms |
  | Procedural indexed K16, 4M | 6.1 ms | 5.8 ms | 0.22 ms | 11.9 ms |
  | Procedural indexed K16, 1M (0.53M visible) | 3.2 ms | 2.8 ms | 0.09 ms | 6.0 ms |

  - Shadows are about half of the cost: every visible blade is drawn into the 4 cascades (one command visible in every split).
  - K16 gains mostly in the shadow pass (-28%, vertex work only) and less in depth + colour (-9%).
  - Not proportional to the blade count: from 0.5M to 2.1M visible, shadows grow by about 2.9 ms per million blades, but 0.5M already cost 3.2 ms: a large part does not depend on the blade count (likely pixel fill of the dense grass, in the shadow map and on screen).
  - SSAO itself stays at 0.55 ms with or without blades. View 1 (10 m high) gives the same split, 0.3 to 0.5 ms higher.
- **Which GPU times exist in a player** (`GpuMarkerProbe`, `-spikeTimingProbe`): of 1,062 markers, 12 have a GPU time: `ExecuteRenderGraph` (camera), `MainLightShadow`, `SSAO`, our `Scatter Cull`, and small setup scopes. The Render Graph pass markers (`DrawOpaqueObjects`, `DrawDepthNormalPrepass`, `DrawMotionVectors`) and the engine's `RenderLoop.ScheduleDraw` have none, though the Profiler window shows the latter. Same list with graphics jobs (`kGfxThreadingModeSplitJobs`, default), without (`-force-gfx-mt`) and on the main thread (`-force-gfx-direct`). Unity's Profiler warns that URP and HDRP do not show most GPU markers. So `UrpPassTimings` reads camera, shadows and SSAO everywhere, and depth, colour and motion vectors in the Editor only; design 12.1 updated. The first probe (1M blades, view 0, mesh indirect) already shows the shadow cascades at 3.9 ms of 7.9 ms.
- The benchmark first had a breakdown mode switching shadows and SSAO off; it was removed once shadows were measurable directly. Switching SSAO off at runtime also needs the Lit variants without SSAO, which URP strips from builds whose renderers all use SSAO (the ground drew with the error shader).
- **Decisions (accepted at the review, 2026-11-27):**

  | Usage | Command type | Backend | K |
  |---|---|---|---|
  | Blade layers (grass, flowers, moss) | Procedural indexed indirect (blade built from `SV_VertexID`, K-blade index buffer) | BRG | 16 (plateau from 16 to 64) |
  | Object layers (small rocks, bushes) | Mesh indirect | BRG | - |
  | Shadow, depth, motion-vector passes | The command of the colour pass, sent again for each view | BRG | - |

  BRG is confirmed as the main path: 0.1 to 0.3 ms faster than the Graphics.Render*Indirect baseline, same CPU cost (submission under 0.02 ms). Applied to design 2 (vocabulary: blade template, K), 6.3, 6.4, 7 and the 16.1 row; the two Phase 1 open items of 16.2 are closed; Phase 2 item 1 updated.
- **To discuss at the review: cost.** About 12 ms of scatter GPU for 2.1M visible blades at 1440p on the RTX 3060 Ti, against 2.5 ms in design 12; design 10.1 expected 1.5 to 2M visible blades within the budget. The spike has no density falloff (5.3) and casts every blade in every cascade (6.2 plans near casters in the first cascades only): both directly reduce the two halves measured. Phase 2 can measure them on the real pipeline.
- Final validation (RTX 3060 Ti, D3D12): Edit Mode 61/61, Play Mode 28/28 in the Editor, Play Mode in a Windows player 25/28 with 3 ignored (the cases of `Render_BothBackends_ShadeTheSame` containing the shadow term, known difference above).
- Debug views (F8, `SpikeDebugView`): off, colour per draw, main light only, ambient only, shadow only, diffuse only. Added to compare the backends term by term.
- `Scatter Prototype > Build Spike Development Player` (`PrototypeBuild.BuildSpikeDevelopment`): development build of `Spike_Draw` alone, for the Frame Debugger and the Profiler attached to a player.
- The cull reserves entries with one atomic add per thread group and LOD (group shared counters), not one per instance.
- Passes checked together in the Editor (Frame Debugger, scene `Spike_Draw`), with the mesh and procedural indexed blade draw types:
  - BRG draws are named "Hybrid Batch Group" in the Frame Debugger; the shader shows only in the Shader section. Vertex and index counts show 0: they are in the GPU draw arguments.
  - `ShadowCaster`: our 3 draws (blade LOD0, LOD1, rocks) in each of the 4 cascades. One GPU draw per command (no batching across commands), as designed: the draw count is fixed (design 12).
  - `DepthNormals`: our 3 draws in the DepthNormals prepass that SSAO requests.
  - Depth priming (Forced): URP primes from the DepthNormals prepass (no separate depth prepass); the opaque pass draws our blades with ZTest Equal and ZWrite Off and they stay visible, so every pass computes the same vertex positions.
  - `MotionVectors` (TAA on the camera): our 3 draws in the pass, no visible trails when moving. Blades are static in the spike; wind and interaction come in Phases 6 to 8.

---

## Phase 1b - Cost investigation
**Goal:** understand the scatter GPU cost measured in Phase 1 (about 12 ms for 2.1M visible blades at 1440p on the RTX 3060 Ti, design 12 budget 2.5 ms) before building Phase 2, and know which design levers reduce it. Work in the spike (`Spike_Draw`), measured with the S3 benchmark in a development player (Phase 1 notes, `Docs/Measurements`).

1. [x] **Vertex vs fragment:** benchmark mode `-spikeBenchmarkResolution`: BRG, procedural indexed K16 at 1M and 4M blades, 2 views x URP render scale {1, 0.5, 0.25} x main light shadow map resolution {2048, 512}, with the baseline without scatter for each setting (36 cases). Both settings are changed at runtime on the URP asset (in memory in players, the project asset in the Editor) and restored at the end (`SpikeUrpSettings`); CSV columns `renderScale` and `shadowResolution`. The part of depth + colour that does not shrink with the pixel count is vertex and triangle work; same for the shadow pass with its map resolution. The two blade counts separate the cost per blade from the part independent of the blade count.
2. [x] **Density falloff** (design 5.2, 5.3) in the spike cull: keep a blade when its rank is below the density at its distance (100% up to 30 m, 25% at 80 m, 6% at 200 m, linear in between). The rank is the instance index over the count (the R2 placement order: any prefix is evenly spread). **Widening** (design 10.1, far blades fewer and wider): the vertex shader multiplies the blade width by 1/sqrt(density), at most 3, from the distance to the cull camera (the same in every pass). Measure visible blades and cost against Phase 1, with and without widening.
3. [x] **Shadow casters** (design 6.2): five presets of the blade casters (split visibility mask): all LODs in 4 cascades (Phase 1), LOD0 in 4 cascades, LOD0 in the first 2, LOD0 in the first, none. BRG only (Graphics.Render*Indirect cannot limit the cascades). Benchmark mode `-spikeBenchmarkLevers`: BRG, K16, 4M blades, 2 views x (baseline + falloff off, on, on with widening x 5 presets) = 32 cases.
4. [x] Tests: new benchmark plans and CSV columns, density rule against a C# reference on the GPU, density curve and width factor, caster split masks per preset, URP settings applied and restored; no allocation.
5. [x] Debug: F11 density falloff (off, on, on with widening), F12 caster preset; HUD state of both, counters of visible blades per LOD.
6. [x] Vertex shader cleanups that keep the image (added at the review): K-blade index buffers with a power-of-two vertex stride (8), so the shader splits `SV_VertexID` with a shift and a mask instead of an integer division; procedural draws read their visible entry without a branch on K, so the read of the instance setup is dropped by the compiler in the passes without ambient; the ambient of both blade sides from one SH evaluation (L0 and L2 once). Tests: index layout and stride constants mirrored in HLSL; the render tests check the image. Measured before and after on the levers benchmark (blades only).

**Exit:** a cost model (vertex, fragment and shadow cost per visible blade, and the part independent of the blade count), a revised estimate for design 10.1, and the resulting adjustments to Phase 2 (and design 5.3, 6.2 if needed). **Stop and review before Phase 2.**

**Notes**
- Item 1 measures procedural indexed K16 only (the Phase 1 decision), at 1M and 4M blades instead of mesh indirect and K16 at 4M: two blade counts give the cost per blade and the part independent of the blade count. Same number of cases (36).
- Widening added to item 2: far blades without it cover less of the screen, so the falloff gain alone would be optimistic. Full compensation (width x 1/density) would make far blades up to 16 times wider; 1/sqrt(density) capped at 3 keeps them blade-like.
- Why limiting the casters matters even with the per-cluster cull of Phase 2: the cull tests the **camera** frustum, and the shadow passes reuse the camera visible list (design 6.2). Every blade in it runs the vertex shader once per cascade it is submitted to, even far outside that cascade (the URP cascades end at 6.1, 14.6, 26.8 and 50 m; the spike draws blades up to 120 m).
- The Object layer (rocks) is not thinned and always casts in every cascade: the presets and the falloff measure the blades only.
- **The rocks were a large part of the Phase 1 cost.** The first runs with rocks showed a shadow pass of 2.4 ms with no blade caster at all. The spike rocks are 16k built-in spheres (515 vertices, 768 triangles), about 8,250 visible up to 120 m, with no falloff, no mesh LOD, casting in every cascade. Added flag `-spikeBenchmarkNoObjects` (the Object layer is off for the whole run, CSV name `_noobjects`) and ran both benchmarks again. Rocks cost (difference of the two runs, view 0): **4.0 ms** (shadows 2.3 ms, depth + colour 1.7 ms), so the Phase 1 figure of 12 ms was about 8 ms of blades and 4 ms of rocks. Reference runs: the blades-only CSVs (`Docs/Measurements/Phase1b`).
- The shader density function first had early returns, which FXC reported as "use of potentially uninitialized variable" once inlined in the cull kernel; rewritten with a single return.
- **Measurements** (2026-11-28, blades only, RTX 3060 Ti, D3D12, 2560x1440, development player, depth priming Auto, SSAO on, 4 cascades over 50 m, no TAA; BRG, K16; median of 300 frames, cost = value minus the run without scatter; view 0 unless noted):

  | 4M blades stored | Visible blades | Shadows | Depth + colour | Cull | Scatter GPU | 99th pct |
  |---|---|---|---|---|---|---|
  | Phase 1 behaviour (no falloff, all LODs cast in 4 cascades) | 2.12M | 3.78 ms | 4.21 ms | 0.22 ms | 8.05 ms | 8.60 ms |
  | Density falloff | 0.86M | 1.71 ms | 2.21 ms | 0.21 ms | 3.99 ms | 4.31 ms |
  | Casters LOD0 only, 4 cascades | 2.12M | 0.24 ms | 4.27 ms | 0.22 ms | 4.58 ms | 5.08 ms |
  | Falloff + LOD0 in 4 cascades | 0.86M | 0.25 ms | 2.25 ms | 0.20 ms | 2.58 ms | 2.88 ms |
  | Falloff + LOD0 in the first 2 cascades | 0.86M | 0.12 ms | 2.24 ms | 0.20 ms | **2.45 ms** | 2.78 ms |
  | Falloff + LOD0 in the first cascade | 0.86M | 0.07 ms | 2.24 ms | 0.20 ms | 2.40 ms | 2.74 ms |
  | Falloff + no blade caster | 0.86M | 0 | 2.23 ms | 0.20 ms | 2.32 ms | 2.61 ms |
  | Falloff + widening + LOD0 in the first 2 cascades | 0.86M | 0.12 ms | 2.31 ms | 0.20 ms | 2.53 ms | 2.84 ms |

  - View 1 (10 m high) is 0.3 to 0.4 ms higher in every case (falloff + LOD0 in the first 2 cascades: 2.82 ms).
  - LOD0 (< 20 m) holds 64k visible blades; the falloff keeps them all (density 1 up to 30 m) and removes 61% of LOD1.
  - Widening costs 0.06 to 0.08 ms (depth + colour), shadows unchanged.
- **Cost model** (blades only, per million visible blades, 1440p, RTX 3060 Ti), from 1M and 4M blades (0.53M and 2.12M visible) at render scales 1, 0.5, 0.25 (3.69, 0.92, 0.23 million pixels):

  | Part | Cost | How it was found |
  |---|---|---|
  | Shadows | 0.44 ms per cascade (1.76 ms for 4) | Proportional to visible blades (fixed part 0.02 ms); the same with a 512 or 2048 shadow map: vertex and triangle work only |
  | Depth + colour, vertex and triangles | 1.18 ms (DepthNormals prepass + colour pass) | Cost per blade extrapolated to 0 pixels: 1.88 ms at scale 1, 1.48 at 0.5, 1.22 at 0.25 |
  | Depth + colour, pixels | 0.18 ms per million pixels: 0.66 ms at native 1440p | Same fit |
  | Independent of the blade count | about 0 (0.02 ms shadows, 0.19 ms or less depth + colour) | Intercepts of the fits |
  | Cull (per instance, spike) | 0.05 ms per million stored blades | 0.08 ms at 1M, 0.21 ms at 4M; the cluster cull of Phase 2 is lighter |

  - The cost is dominated by **vertex and triangle work**: tiny triangles in two camera passes and in each cascade. Pixels are a third of depth + colour at native 1440p.
  - Blades kept by the falloff cost more each in depth + colour (2.6 ms per million instead of 1.9): they are nearer, so bigger on screen.
- **Revised estimate for design 10.1**: with the falloff and LOD0-only casters, one dense layer of **about 0.86M visible blades costs 2.45 to 2.8 ms**, at the 2.5 ms budget of the RTX 3060 Ti at native 1440p. Design 10.1 expected 1.5 to 2M visible blades within the budget; the design 12 stress reference (>= 4M visible blades) would cost about 8 ms even with LOD0-only casters.
- **Decisions (accepted at the review, 2026-11-28)**, applied to design 2 (widening), 6.2, 6.4, 7, 10.1, 12, 13 and the 16.1 row:
  1. Blade layers cast shadows from LOD0 only by default (shadow mode Near). Limiting the near casters to the first cascades saves only 0.12 ms (2 cascades) or 0.17 ms (1), and the cascades end at 6.1 and 14.6 m while LOD0 ends at 20 m: it stays an option of the quality tier (lower hardware), tested on the full system (Phases 8 and 9).
  2. The example density curve is kept (61% fewer far blades, scatter cost halved), with widening (0.07 ms). Both are layer settings, tuned in Phase 9.
  3. Design 10.1 and 12 use the cost model above: about 0.9M visible blades within the RTX 3060 Ti budget; the stress scene is a load reference above the budget.
  4. Phase 2 measures the far blade LOD (LOD2 template, 3 vertices, 1 triangle) and its distance early, and the cost of the DepthNormals prepass (new Phase 2 item 11).
  5. Every layer, Blade or Object, has mesh LODs, a density curve and a shadow mode from the start (Phase 3 items 2 and 4).
- **The arithmetic of the vertex shader is not the limit.** At 1M visible blades a camera pass is about 5M vertices and 3M triangles in 0.6 ms, and the shadow pass (simplest shader) reaches about 7 billion triangles per second, close to the triangle setup rate of the GPU. Item 6 checks it: the cleanups (no integer division, no extra visible list read, half the ambient SH work) change nothing measurable. Levers benchmark without rocks, the build before and after alternated twice (`Docs/Measurements/Phase1b/vertex_cleanup`): mean difference +0.02 ms over the 30 cases, while two runs of the same build differ by 0.06 to 0.2 ms; the shadow pass with every LOD casting (2.1M blades in 4 cascades, the most vertex work) moves by -0.001 ms. The cleanups are kept: same image, simpler code, and they may count on a GPU that is less limited by triangles. The levers are fewer triangles (blade LODs, Phase 2 item 11) and fewer passes; Nsight Graphics (GPU Trace) can show the triangle setup limit directly.
- After a shader change, the first Play Mode run in the Editor can fail `Runner_MeasuresEveryCase_WithVisibleBladesAndPassTimings` (no colour pass GPU time in one case): the Editor compiles the new variants asynchronously and the test measures after 8 warm-up frames. The next run passes.
- Buffers checked: every buffer of the draw path is GPU-only (default usage flags: no `LockBufferForWrite`, no `Dynamic`), written on the GPU or once with `SetData`; visible list and blade data are read in increasing address order. The spike draws blades in placement (R2) order, scattered on screen; Phase 2 stores them per cluster, which keeps consecutive triangles close on screen.

---

## Phase 2 - Core pipeline on a flat ground
**Goal:** the full two-stage pipeline (design section 3) running on a trivial flat ground source.

1. [ ] Blade templates LOD0/1/2 (3/2/1 segments = 7/5/3 vertices, tapered, pre-curved) built in the vertex shader from `SV_VertexID`, and the index buffers of K = 16 blades per instance (design 6.3); `BladeMeshFactory` meshes for the baseline backend.
2. [ ] `IGroundSource` interface (`IsResident(area)`, `Changed(area)` event, candidate point generation, 4.4) and a `FlatGroundSource`.
3. [ ] Arena per layer with power-of-two block class pools, block size from the CPU upper bound, pool-full policy (downgrade farthest / release), block table buffer (5.4).
4. [ ] Chunk manager: world grid, rings (5.3), lifecycle table of 5.1, distance-sorted generation queue with per-frame budget, hysteresis.
5. [ ] Placer compute: progressive low-discrepancy points per cluster, rank (5.2), ring density, per-blade random values from the grid-position hash, 16-byte record (5.5), cluster ranges and bounds.
6. [ ] Cull compute (6.2): args reset, cluster frustum test, LOD, N by rank, atomic reserve with region overflow guard, index write.
7. [ ] BRG backend (and RenderMeshIndirect backend behind the same interface).
8. [ ] Blade shader v1: decode record, place and orient blade, density fade by rank and widening (6.4). Includes `DepthOnly` and `DepthNormals` passes from the start (8.1: required with depth priming).
9. [ ] Debug (12.1, 12.2): stats buffer written by the cull + `AsyncGPUReadback`; HUD timings per stage, blade counts, arena and visible-list fill, overflow and pool-full warnings; views by LOD, ring and rank, cluster grid; freeze culling; backend switch.
   - Debug controls panel: design 12.2 lists more controls than free function keys. Choose between URP's Rendering Debugger (`DebugManager`, custom panel with toggles and enums, Editor and development players, mouse, keyboard or gamepad) and buttons in the HUD (UI Toolkit); decide with the user before building it.
10. [ ] Tests:
   - CPU: chunk/cluster coordinate maths (negative coordinates, floating-origin shift), ring choice and hysteresis, generation queue order, block allocator and pool-full policy, record encode/decode.
   - No allocation: `ScatterSystem.Tick` and every per-frame entry point, warmed up, pass `Is.Not.AllocatingGCMemory()` (CodingRules 3).
   - GPU (dispatch on small inputs, read back, compare with a C# reference):
     - placer: determinism (identical bits on two runs), subset property (sparse block is a prefix of dense block), rank sorted per cluster, cluster bounds contain their records, per-blade random values unchanged after a floating-origin shift;
     - cull: visible count and draw arguments match the C# reference for a synthetic camera, args reset, overflow guard counts skipped clusters.
11. [ ] Measurements (Phase 1b decision 4), on the real pipeline, with the S3 benchmark method: the blade LOD distances and the LOD2 template (1 triangle) for far blades, against the cost model of design 10.1; the cost of the DepthNormals prepass (depth priming Auto vs off, SSAO on vs off).

**Exit:** a few million stored blades on a flat plane; flying around shows no popping; debug view shows rings, clusters and LODs; backends switchable at runtime.

---

## Phase 3 - Layers
**Goal:** multiple layers through one pipeline (design section 7).
1. [ ] `ScatterLayer` asset and its GPU mirror; scatter profile asset per ground source.
2. [ ] Per-layer arena, density curve with widening, rings, LOD meshes, shadow mode. Every layer, Blade or Object, has them from the start (Phase 1b: an Object layer without them cost 4 ms).
3. [ ] Object kind: URP Lit-based shader variant for small rocks and bushes.
4. [ ] Test content: 2 grass types, 1 flower, 1 bush, 1 small rock. Object meshes are low-poly with mesh LODs; the cost of each layer is measured against design 10.1.
5. [ ] Debug: per-layer HUD rows, view by layer, layer toggles.
6. [ ] Tests: C# structs mirrored to the GPU have the HLSL size and layout; per-layer density and range applied by the placer and the cull.

**Exit:** all five layers on the plane with independent density, size and range.

---

## Phase 4 - Terrain adapter and streaming
**Goal:** scatter on a streamed hierarchical-LOD terrain through the generic adapter (4.1, 4.3, 4.4), and prove that the scatter code does not depend on the terrain.
1. [ ] In the package: `ITerrainAdapter`, ground patch pool, `TerrainGroundSource` (patch to candidate points, normal from gradient, slope/altitude limits, root albedo tint).
2. [ ] In `Assets`: `MockHlodTerrain`, a quadtree of tiles with a heightmap per LOD, asynchronous loads with random latency, LOD chosen by distance, its own mesh rendering and procedural masks. Its adapter follows the contract of 4.4.
3. [ ] In `Assets`: `UnityTerrainAdapter` for Unity Terrain tiles (heightmap + splatmaps). It gives hand-painted masks for test scenes and proves the interface with a second, different terrain.
4. [ ] In the package: adapter checker (debug tool), which:
   - draws the patch heights as dots over the terrain, coloured by the error against a physics raycast when the terrain has colliders;
   - logs `IsResident`/`Changed` calls;
   - times `FillPatch`.

   Plus the HUD streaming rows (chunks per state, generations, `Changed` events) and the chunk-state view.
5. [ ] Test scenes: large tiles (1 km) and small tiles (128 m); fast camera through LOD switches and loads.
6. [ ] Unit tests:
   - patch fill and placement with a test adapter returning an analytic height field (heights and normals vs the formula);
   - lifecycle: `Changed` queues regeneration and keeps old blocks, loss of residency releases them;
   - **adapter contract test suite** (abstract test fixture in the package) checking rules 2 to 5 of design 4.4, run against both adapters. The terrain project reuses it for its own adapter.

**Exit:** grass follows the terrain and respects masks with both adapters; no floating blades or holes during simulated streaming; chunk seams invisible; `FillPatch` cost measured; no scatter code change between the two adapters.

---

## Phase 5 - Mesh ground source
**Goal:** same pipeline on meshes (4.2).
1. [ ] Editor baker: area-weighted surface sampling, UV mask or vertex colour, per-layer sample sets, binning by chunk/cluster, progressive ordering.
2. [ ] `MeshGroundSource` runtime: upload baked samples to a GPU buffer on load (residency), copy samples with `rank < ring density` into blocks.
3. [ ] Test scenes: sloped rock, sphere, overhang, rotated and scaled instances; terrain/mesh overlap handled by masks.
4. [ ] Unit tests: baker area weighting (sample count per triangle proportional to area), binning by chunk/cluster, progressive order (every prefix keeps a minimum spacing), samples cover all of a sphere.

**Exit:** correct placement all around a sphere; normals and orientation right; no difference in culling/drawing code vs terrain.

---

## Phase 6 - Wind
**Goal:** natural animation (8.3).
1. [ ] Wind settings component (globals), tileable noise texture.
2. [ ] Vertex shader: base wind, gusts, length-preserving bend, per-blade phase/stiffness, per-layer weight.
3. [ ] HUD controls (wind settings, pause wind) and wind field view.

**Exit:** convincing motion at all distances; no visible noise tiling; blade length preserved.

---

## Phase 7 - Player interaction
**Goal:** bending and flattening with recovery (8.4).
1. [ ] Displacement texture (512 x 512 RGBA16F), whole-texel follow, shift, decay.
2. [ ] `ScatterInteractor` component and the splat pass.
3. [ ] Shader integration with per-layer weight.
4. [ ] Player controller and physics test objects.
5. [ ] Debug: interaction texture view.
6. [ ] Unit tests: whole-texel follow and shift maths (no sub-texel drift), decay toward rest on the GPU.

**Exit:** walking leaves a trail that recovers smoothly; no shimmer while moving.

---

## Phase 8 - Lighting, shadows, motion vectors
**Goal:** full URP integration (8.1, 8.2, 8.5).
1. [ ] Final blade shading: gradient, tint, root AO, translucency, ground-normal blending, Forward+ additional lights.
2. [ ] Shadow receiving; shadow casting per layer (Off/Near/All, Near = LOD0 only); widened frustum for shadow-casting LODs; optional limit of the near casters to the first cascades, per quality tier (6.2): visual check and cost on the full system. Check the Phase 1 difference: in players, the Graphics.Render*Indirect backend gives slightly less blade self-shadowing than BRG.
3. [ ] DepthOnly/DepthNormals checked with SSAO and depth priming.
4. [ ] Motion-vector pass with previous-frame wind and interaction; check TAA and STP.
5. [ ] Debug: shadow-casters view; shadow mode and depth priming toggles.

**Exit:** grass consistent with terrain lighting; shadow cost measured separately; no ghosting with STP.

---

## Phase 9 - Measurement and tuning
**Goal:** numbers against design section 12, on both GPUs.
1. [ ] Scripted camera paths (walk, run, fast fly) in the stress and open-world load scenes. Each path also runs **with scatter disabled**, as the baseline for the worst-frame metric.
2. [ ] Density sweep; CSV per quality tier, per backend, native vs STP. The CSV summary gives median, 99th percentile and worst frame time, and the maximum of each correctness counter (design 12).
3. [ ] 10-minute loop of the open-world path: GPU memory and GC allocation must not grow.
4. [ ] Tune: chunk/cluster sizes, ring distances, LOD distances, blade widths, generation budget, block pool sizes, depth priming on/off (8.1), number of cascades receiving near casters.
5. [ ] RenderDoc/PIX: rasterisation and shadow cost, compute timings, memory.
6. [ ] Optional experiments of design section 14, only if the profile points to them.

**Exit:** results table against every metric of design 12 (speed, stability, correctness), recommended settings per tier, backend decision.

---

## Phase 10 - Smoke tests and report
Unit tests are written in each phase (Phases 2 to 7), not here.

- [ ] Play-mode smoke tests: each scene loads, blade counts > 0, no GPU errors, no buffer leaks after unload.
- [ ] Final report: architecture as built, measurements, known issues, an adapter integration guide (contract of 4.4 + checker), production gaps (moving ground, streamed large objects, production terrain adapter).

---

## Cross-cutting rules
- A phase is done only when its unit tests pass.
- No GPU readback in the steady-state frame. Exceptions: blocking readback in tests, and the non-blocking debug stats readback (design 12.1).
- All GPU buffers allocated at startup from the quality tier and released in `OnDisable`; checked for leaks.
- No per-frame managed allocations (`Docs/CodingRules.md`, section 3).
- Few shader keywords; feature toggles as global uniforms where possible.
