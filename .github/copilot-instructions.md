# Copilot instructions - GrassRendering

Unity 6.6 (6000.6.10f1) prototype of a GPU-driven procedural scatter system (grass, flowers, moss, small rocks, bushes) for URP Forward+, rendered with BatchRendererGroup indirect draws. Target PC GPUs: RTX 3060 (test), RTX 5070 Ti (target).

## Read first
Before any work, read both documents:
- `Docs/GrassPrototype_Design.md`: **what** is built and **why**. Source of truth for the architecture. Section 2 is the vocabulary; section 16 lists the decisions taken and the open items.
- `Docs/GrassPrototype_Implementation.md`: **how** and **in which order**, phase by phase, with the progress table and checkboxes.
- `Docs/CodingRules.md`: coding rules (structure, naming, zero allocation per frame, GPU code). Follow them for every change; `.editorconfig` enforces formatting and naming.

## Workflow
- Work phase by phase, in order. Do not start a phase before the previous one meets its exit check. Phase 1 ends with a **stop and review** with the user before Phase 2.
- An item is done only when its unit tests pass (Unity Test Framework, Edit Mode; GPU tests need a graphics device, so never use `-nographics`).
- Keep the documents in sync **in the same change as the code**:
  - tick the item and update the phase status in the implementation plan;
  - record deviations and findings in a **Notes** list at the end of the phase;
  - if a design decision changes, update the design section and its row in design 16.1. Do not reopen a 16.1 decision without new facts; ask the user first.
- Use the vocabulary of design section 2 in code, comments and documents (chunk, cluster, ring, block, arena, rank, blade record...). Add new terms to the vocabulary.

## Code rules (summary of `Docs/CodingRules.md`)
- The scatter system lives in the embedded package `Packages/com.<company>.scatter` and must not reference terrain code or anything in `Assets`. Prototype-only content (mock terrain, test scenes, player) stays in `Assets`.
- **Zero managed allocation per frame** (no `new`, LINQ, strings, capturing lambdas, boxing in per-frame code). Every per-frame entry point has an `Is.Not.AllocatingGCMemory()` test.
- No GPU readback in the frame (exceptions: tests, and the non-blocking debug stats readback of design 12.1).
- No per-frame managed allocations; GPU buffers allocated at startup from the quality tier and released in `OnDisable`.
- Few shader keywords: prefer global uniforms. Hand-written HLSL, no alpha clipping on blades. Every scatter shader has `UniversalForward`, `ShadowCaster`, `DepthOnly`, `DepthNormals` and `MotionVectors` passes.
- Every new feature comes with its debug support (HUD values, debug view or control, design 12.1/12.2).

## Writing the documents
- Keep them easy to read: describe only the recommended solution; alternatives go only in the short "Optional experiments" list.
- Diagrams in Mermaid, top-to-bottom, default font size.
- When citing Unity documentation, use the version of this project (6000.6).
