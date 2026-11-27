# GaiaGames GPU Scatter (`com.gaiagames.scatter`)

GPU-driven procedural scatter (grass, flowers, moss, small rocks, bushes) for URP Forward+, drawn with BatchRendererGroup indirect draws. Blades are generated and culled on the GPU; the CPU only manages chunks and submits a fixed number of draws.

The package has **no dependency on any terrain code**: a terrain connects through a small adapter (section 4.4 of the prototype design document). It is developed in the `grass-rendering` prototype and is meant to be imported as is into the production project.

> Status: version 0.1.0, prototype. Only the diagnostics (debug HUD, CSV logger) and the quality settings exist so far. The scatter pipeline is built phase by phase (see the implementation plan of the prototype repository).

## Requirements

| | |
|---|---|
| Unity | 6000.6 or later |
| Render pipeline | URP 17.6, Forward+ renderer |
| Graphics API | Compute shaders required (Direct3D 12 or Vulkan) |
| Dependencies | `com.unity.render-pipelines.universal` 17.6.0, `com.unity.modules.uielements` |
| Optional | `com.unity.inputsystem`: when installed, the HUD keys use the Input System (define `SCATTER_INPUT_SYSTEM`); otherwise the legacy Input Manager |
| Player Settings | **Frame Timing Stats** enabled, so the HUD can read CPU and GPU times in players |

## Installation

Copy the `com.gaiagames.scatter` folder into the `Packages` folder of the project (embedded package), or add it with **Window > Package Manager > + > Install package from disk** and select `package.json`.

## What the package contains

| Folder | Assembly | Content |
|---|---|---|
| `Runtime/` | `Scatter.Runtime` | `ScatterQuality` (settings of one quality tier), `ScatterShadowMode` |
| `Runtime/Diagnostics/` | `Scatter.Runtime` | Debug HUD (`ScatterDebugHud` + prefab), frame statistics, CSV logger |
| `Runtime/Ground`, `Chunks`, `Compute`, `Rendering`, `Wind`, `Interaction` | `Scatter.Runtime` | Empty for now; filled by the next phases |
| `Shaders/` | | Scatter shaders (from Phase 1) |
| `Editor/` | `Scatter.Editor` | Menu **GameObject > Scatter > Debug HUD** |
| `Tests/Editor`, `Tests/Runtime` | `Scatter.Tests`, `Scatter.Tests.PlayMode` | Edit Mode and Play Mode tests |

## Using it

**Debug HUD**
1. **GameObject > Scatter > Debug HUD** adds the HUD prefab to the open scene. It draws a UI Toolkit label through a Panel Renderer.
2. In the **Scatter Debug Hud** component, fill **Quality Per Level** with one `ScatterQuality` asset per Unity quality level (index = level). Values over the tier targets are shown in red.
3. In Play mode: **F1** shows or hides the HUD, **F2** starts or stops CSV recording. The CSV is written to `Application.persistentDataPath/ScatterLogs/frames_<date>_<time>.csv`.

**Quality tiers**
Create one asset per GPU class with **Assets > Create > Scatter > Scatter Quality**: density multiplier, ring distances, maximum shadow mode, generation budget, and the timing targets of the tier. GPU buffers will be sized from these values at startup.

**Scatter timings from your own code**
`ScatterDebugHud.StageTimings` takes a `ScatterStageTimings` value each frame. Fields left at `NaN` are shown as "-" in the HUD and the CSV.

## Rules the package follows

- No managed allocation per frame; every per-frame entry point has an allocation test.
- No GPU readback in the frame, except the non-blocking debug statistics.
- No reference to terrain code or to anything outside the package.
