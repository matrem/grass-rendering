# Phase 3 - Layers

Part of the [implementation plan](README.md) (progress, rules, how the plan is kept up to date). Section numbers like 6.2 refer to the [design](../GrassPrototype_Design.md).

**Goal:** multiple layers through one pipeline (design section 7).
1. [ ] `ScatterLayer` asset and its GPU mirror; scatter profile asset per ground source.
2. [ ] Per-layer arena, density curve with widening, rings, LOD meshes, shadow mode. Every layer, Blade or Object, has them from the start (Phase 1b: an Object layer without them cost 4 ms).
3. [ ] Object kind: URP Lit-based shader variant for small rocks and bushes.
4. [ ] Test content: 2 grass types, 1 flower, 1 bush, 1 small rock. Object meshes are low-poly with mesh LODs; the cost of each layer is measured against design 10.1.
5. [ ] Debug: per-layer HUD rows, view by layer, layer toggles.
6. [ ] Tests: C# structs mirrored to the GPU have the HLSL size and layout; per-layer density and range applied by the placer and the cull.

**Exit:** all five layers on the plane with independent density, size and range.
