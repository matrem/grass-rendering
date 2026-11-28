# Phase 5 - Mesh ground source

Part of the [implementation plan](README.md) (progress, rules, how the plan is kept up to date). Section numbers like 6.2 refer to the [design](../GrassPrototype_Design.md).

**Goal:** same pipeline on meshes (4.2).
1. [ ] Editor baker: area-weighted surface sampling, UV mask or vertex colour, per-layer sample sets, binning by chunk/cluster, progressive ordering.
2. [ ] `MeshGroundSource` runtime: upload baked samples to a GPU buffer on load (residency), copy samples with `rank < ring density` into blocks.
3. [ ] Test scenes: sloped rock, sphere, overhang, rotated and scaled instances; terrain/mesh overlap handled by masks.
4. [ ] Unit tests: baker area weighting (sample count per triangle proportional to area), binning by chunk/cluster, progressive order (every prefix keeps a minimum spacing), samples cover all of a sphere.

**Exit:** correct placement all around a sphere; normals and orientation right; no difference in culling/drawing code vs terrain.
