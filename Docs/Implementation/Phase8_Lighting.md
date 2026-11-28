# Phase 8 - Lighting, shadows, motion vectors

Part of the [implementation plan](README.md) (progress, rules, how the plan is kept up to date). Section numbers like 6.2 refer to the [design](../GrassPrototype_Design.md).

**Goal:** full URP integration (8.1, 8.2, 8.5).
1. [ ] Final blade shading: gradient, tint, root AO, translucency, ground-normal blending, Forward+ additional lights.
2. [ ] Shadow receiving; shadow casting per layer (Off/Near/All, Near = LOD0 only); widened frustum for shadow-casting LODs; optional limit of the near casters to the first cascades, per quality tier (6.2): visual check and cost on the full system. Check the Phase 1 difference: in players, the Graphics.Render*Indirect backend gives slightly less blade self-shadowing than BRG.
3. [ ] DepthOnly/DepthNormals checked with SSAO and depth priming.
4. [ ] Motion-vector pass with previous-frame wind and interaction; check TAA and STP.
5. [ ] Debug: shadow-casters view; shadow mode and depth priming toggles.

**Exit:** grass consistent with terrain lighting; shadow cost measured separately; no ghosting with STP.
