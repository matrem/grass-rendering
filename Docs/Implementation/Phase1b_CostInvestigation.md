# Phase 1b - Cost investigation

Part of the [implementation plan](README.md) (progress, rules, how the plan is kept up to date). Section numbers like 6.2 refer to the [design](../GrassPrototype_Design.md).

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
