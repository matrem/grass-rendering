# GPU Scatter Prototype - Implementation Plan

Companion to the [design](../GrassPrototype_Design.md) (section numbers like 6.2 refer to it).
Phases are ordered so the riskiest parts are proven first. Each phase ends with something visible and an exit check. One branch/commit series per phase ([git workflow](../GitWorkflow.md)).

This file holds the progress, the rules and how the plan is kept up to date. Each phase has its own file: goal, items, exit check and Notes.

## Progress

The only place where phase status is written.

| Phase | Goal | Status |
|---|---|---|
| [0 - Project setup](Phase0_Setup.md) | An empty, measurable project | Done |
| [1 - Spikes](Phase1_Spikes.md) | Prove the BRG draw path, choose the draw command types | Done |
| [1b - Cost investigation](Phase1b_CostInvestigation.md) | Understand the scatter GPU cost and the levers that reduce it | Done |
| [2 - Core pipeline on a flat ground](Phase2_CorePipeline.md) | The two-stage pipeline on a flat ground source | Not started |
| [3 - Layers](Phase3_Layers.md) | Several layers through one pipeline | Not started |
| [4 - Terrain adapter and streaming](Phase4_TerrainStreaming.md) | Scatter on a streamed terrain through the adapter | Not started |
| [5 - Mesh ground source](Phase5_MeshGroundSource.md) | The same pipeline on meshes | Not started |
| [6 - Wind](Phase6_Wind.md) | Natural animation | Not started |
| [7 - Player interaction](Phase7_PlayerInteraction.md) | Bending and flattening with recovery | Not started |
| [8 - Lighting, shadows, motion vectors](Phase8_Lighting.md) | Full URP integration | Not started |
| [9 - Measurement and tuning](Phase9_Measurement.md) | Numbers against design 12, on both GPUs | Not started |
| [10 - Smoke tests and report](Phase10_SmokeTestsReport.md) | Smoke tests and the final report | Not started |

Status values: Not started, In progress, Done, Blocked (with the reason).

## How the plan is kept up to date (in the same commit as the code)
- Tick an item `[x]` in its phase file when it is implemented **and** its tests pass. Update the phase status in the table above.
- When the work differs from the plan, or a phase produces findings (spike results, measurements), add a **Notes** list at the end of that phase file: what changed and why.
- If a change affects a design decision, update the design document too (the relevant section and the decision table 16.1). The design describes what is true now; git history keeps the past.
- Update `Docs/ProjectGuide.md` (what exists and how it works) and, when a phase status changes, the status line of `README.md`.
- Update the package README (`Packages/com.gaiagames.scatter/README.md`) when the package's requirements, contents or public usage change.
- New work discovered on the way is added as a new item in the right phase file, not kept elsewhere.
- Items are referred to as "Phase 2 item 11" everywhere; the phase file follows from the phase number.

## Cross-cutting rules
- A phase is done only when its unit tests pass.
- No GPU readback in the steady-state frame. Exceptions: blocking readback in tests, and the non-blocking debug stats readback (design 12.1).
- All GPU buffers allocated at startup from the quality tier and released in `OnDisable`; checked for leaks.
- No per-frame managed allocations (`Docs/CodingRules.md`, section 3).
- Few shader keywords; feature toggles as global uniforms where possible.
