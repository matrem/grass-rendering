# Phase 4 - Terrain adapter and streaming

Part of the [implementation plan](README.md) (progress, rules, how the plan is kept up to date). Section numbers like 6.2 refer to the [design](../GrassPrototype_Design.md).

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
