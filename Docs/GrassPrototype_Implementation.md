# GPU Scatter Prototype - Implementation Plan

Companion to `GrassPrototype_Design.md` (section numbers below refer to it).
Phases are ordered so the riskiest parts are proven first. Each phase ends with something visible and an exit check. One branch/commit series per phase.

## Progress

| Phase | Status |
|---|---|
| 0 - Project setup | Done |
| 1 - Spikes | Not started |
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
**Goal:** confirm that the draw path of design section 6.3 works before building on it.

- [ ] **S1 - BRG indirect:** one compute shader writes a visible-index buffer and indirect args for 1M instances of a simple blade; a `BatchRendererGroup` draws them with `BatchDrawCommandIndirect` (visible instances buffer + args buffer). The URP shader reads per-instance data from a custom StructuredBuffer using the index BRG provides.
  Check: two (layer, LOD) regions in one visible buffer via `visibleInstancesBufferWindowOffset`; shadow caster, DepthOnly and DepthNormals passes work; no per-instance BRG metadata needed.
- [ ] **S2 - RenderMeshIndirect:** same content with `Graphics.RenderMeshIndirect`.
- [ ] **S3 - Timing:** GPU timestamps around the compute and the draws in both paths.
- [ ] Graphics Settings: check that "BatchRendererGroup Variants" keeps the DOTS instancing variants in players (the default strips them when Entities Graphics is not installed); check in a Windows build.

**Exit:** a short findings note: what works, any API limitation, and confirmation of BRG as main path (or fallback to RenderMeshIndirect). **Stop and review before Phase 2.**

---

## Phase 2 - Core pipeline on a flat ground
**Goal:** the full two-stage pipeline (design section 3) running on a trivial flat ground source.

1. [ ] Blade meshes LOD0/1/2 (7/5/3 vertices, tapered, pre-curved), generated by code.
2. [ ] `IGroundSource` interface (`IsResident(area)`, `Changed(area)` event, candidate point generation, 4.4) and a `FlatGroundSource`.
3. [ ] Arena per layer with power-of-two block class pools, block size from the CPU upper bound, pool-full policy (downgrade farthest / release), block table buffer (5.4).
4. [ ] Chunk manager: world grid, rings (5.3), lifecycle table of 5.1, distance-sorted generation queue with per-frame budget, hysteresis.
5. [ ] Placer compute: progressive low-discrepancy points per cluster, rank (5.2), ring density, per-blade random values from the grid-position hash, 16-byte record (5.5), cluster ranges and bounds.
6. [ ] Cull compute (6.2): args reset, cluster frustum test, LOD, N by rank, atomic reserve with region overflow guard, index write.
7. [ ] BRG backend (and RenderMeshIndirect backend behind the same interface).
8. [ ] Blade shader v1: decode record, place and orient blade, density fade by rank (6.4). Includes `DepthOnly` and `DepthNormals` passes from the start (8.1: required with depth priming).
9. [ ] Debug (12.1, 12.2): stats buffer written by the cull + `AsyncGPUReadback`; HUD timings per stage, blade counts, arena and visible-list fill, overflow and pool-full warnings; views by LOD, ring and rank, cluster grid; freeze culling; backend switch.
10. [ ] Tests:
   - CPU: chunk/cluster coordinate maths (negative coordinates, floating-origin shift), ring choice and hysteresis, generation queue order, block allocator and pool-full policy, record encode/decode.
   - No allocation: `ScatterSystem.Tick` and every per-frame entry point, warmed up, pass `Is.Not.AllocatingGCMemory()` (CodingRules 3).
   - GPU (dispatch on small inputs, read back, compare with a C# reference):
     - placer: determinism (identical bits on two runs), subset property (sparse block is a prefix of dense block), rank sorted per cluster, cluster bounds contain their records, per-blade random values unchanged after a floating-origin shift;
     - cull: visible count and draw arguments match the C# reference for a synthetic camera, args reset, overflow guard counts skipped clusters.

**Exit:** a few million stored blades on a flat plane; flying around shows no popping; debug view shows rings, clusters and LODs; backends switchable at runtime.

---

## Phase 3 - Layers
**Goal:** multiple layers through one pipeline (design section 7).
1. [ ] `ScatterLayer` asset and its GPU mirror; scatter profile asset per ground source.
2. [ ] Per-layer arena, density curve, rings, LOD meshes, shadow mode.
3. [ ] Object kind: URP Lit-based shader variant for small rocks and bushes.
4. [ ] Test content: 2 grass types, 1 flower, 1 bush, 1 small rock.
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
2. [ ] Shadow receiving; shadow casting per layer (Off/Near/All); widened frustum for shadow-casting LODs; near casters limited to the first cascades (6.2).
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
