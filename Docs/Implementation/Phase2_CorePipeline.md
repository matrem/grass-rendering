# Phase 2 - Core pipeline on a flat ground

Part of the [implementation plan](README.md) (progress, rules, how the plan is kept up to date). Section numbers like 6.2 refer to the [design](../GrassPrototype_Design.md).

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
