# Phase 7 - Player interaction

Part of the [implementation plan](README.md) (progress, rules, how the plan is kept up to date). Section numbers like 6.2 refer to the [design](../GrassPrototype_Design.md).

**Goal:** bending and flattening with recovery (8.4).
1. [ ] Displacement texture (512 x 512 RGBA16F), whole-texel follow, shift, decay.
2. [ ] `ScatterInteractor` component and the splat pass.
3. [ ] Shader integration with per-layer weight.
4. [ ] Player controller and physics test objects.
5. [ ] Debug: interaction texture view.
6. [ ] Unit tests: whole-texel follow and shift maths (no sub-texel drift), decay toward rest on the GPU.

**Exit:** walking leaves a trail that recovers smoothly; no shimmer while moving.
