# GaiaGames GPU Scatter (`com.gaiagames.scatter`)

GPU-driven procedural scatter (grass, flowers, moss, small rocks, bushes) for URP Forward+, drawn with BatchRendererGroup indirect draws. Blades are generated and culled on the GPU; the CPU only manages chunks and submits a fixed number of draws.

The package has **no dependency on any terrain code**: a terrain connects through a small adapter (section 4.4 of the prototype design document). It is developed in the `grass-rendering` prototype and is meant to be imported as is into the production project.

> Status: version 0.1.0, prototype. Phase 1 (spikes) is done: the BRG draw path is proven (procedural indexed draws, K = 16 blades per instance, for Blade layers; mesh indirect for Object layers), with its measurement tools. The scatter pipeline itself (chunks, generation, layers) starts in Phase 2 (see the implementation plan of the prototype repository). The `Rendering/Spike` code is test code of Phase 1, replaced in Phase 2.

## Requirements

| | |
|---|---|
| Unity | 6000.6 or later |
| Render pipeline | URP 17.6, **Forward+** renderer (the scatter shaders use the Forward+ light path, `_CLUSTER_LIGHT_LOOP`) |
| Graphics API | Compute shaders required: Direct3D 12 or Vulkan. BRG uses raw buffers there |
| Dependencies | `com.unity.render-pipelines.universal` 17.6.0, `com.unity.modules.uielements` |
| Optional | `com.unity.inputsystem`: when installed, the HUD and spike keys use the Input System (define `SCATTER_INPUT_SYSTEM`); otherwise the legacy Input Manager |
| Graphics Settings | **BatchRendererGroup Variants = Keep All** (serialized `m_BrgStripping: 2`). The default strips the `DOTS_INSTANCING_ON` shader variants BRG draws with when Entities Graphics is not installed: players then draw nothing correct |
| Player Settings | **Frame Timing Stats** enabled, so the HUD can read CPU and GPU times in players. "Strict shader variant matching" recommended: a stripped variant then shows as the error shader instead of a silent fallback |

## Installation

Copy the `com.gaiagames.scatter` folder into the `Packages` folder of the project (embedded package), or add it with **Window > Package Manager > + > Install package from disk** and select `package.json`. Then set the Graphics Settings above.

## What the package contains

| Folder | Assembly | Content |
|---|---|---|
| `Runtime/` | `Scatter.Runtime` (unsafe code allowed: BRG draw commands are native pointers) | `ScatterQuality` (settings of one quality tier), `ScatterShadowMode` |
| `Runtime/Diagnostics/` | `Scatter.Runtime` | Debug HUD (`ScatterDebugHud` + prefab), frame statistics, CSV logger, URP GPU timings (`UrpPassTimings`), GPU marker probe (`GpuMarkerProbe`) |
| `Runtime/Rendering/` | `Scatter.Runtime` | `BladeMeshFactory`: blade mesh LODs and K-blade index buffers |
| `Runtime/Rendering/Spike/` | `Scatter.Runtime` | Phase 1 test code: BRG and Graphics.Render*Indirect backends on a GPU cull, benchmark. Its `Resources/` folder holds the spike shaders asset (included in every build while it exists) |
| `Runtime/Ground`, `Chunks`, `Compute`, `Wind`, `Interaction` | `Scatter.Runtime` | Empty for now; filled by the next phases |
| `Shaders/Spike/` | | Spike cull compute and spike shaders (five passes: forward, shadow caster, depth, depth normals, motion vectors) |
| `Editor/` | `Scatter.Editor` | Menu **GameObject > Scatter > Debug HUD** |
| `Tests/Editor`, `Tests/Runtime` | `Scatter.Tests`, `Scatter.Tests.PlayMode` | Edit Mode and Play Mode tests (GPU tests need a graphics device) |

## Using it

**Debug HUD**
1. **GameObject > Scatter > Debug HUD** adds the HUD prefab to the open scene. It draws a UI Toolkit label through a Panel Renderer.
2. In the **Scatter Debug Hud** component, fill **Quality Per Level** with one `ScatterQuality` asset per Unity quality level (index = level). Values over the tier targets are shown in red.
3. In Play mode: **F1** shows or hides the HUD, **F2** starts or stops CSV recording. The CSV is written to `Application.persistentDataPath/ScatterLogs/frames_<date>_<time>.csv`.

**Quality tiers**
Create one asset per GPU class with **Assets > Create > Scatter > Scatter Quality**: density multiplier, ring distances, maximum shadow mode, generation budget, and the timing targets of the tier. GPU buffers will be sized from these values at startup.

**Scatter timings and state from your own code**
- `ScatterDebugHud.StageTimings` takes a `ScatterStageTimings` value each frame: CPU main, generation, cull, camera, shadows, SSAO, depth, colour, motion vectors. Fields left at `NaN` are shown as "-" in the HUD and the CSV.
- `ScatterDebugHud.StatusText` shows extra lines of your driver under the HUD header (set it on change only).
- `UrpPassTimings.Fill` writes the URP GPU times. In development players only the camera (`ExecuteRenderGraph`), the main light shadows (`MainLightShadow`) and SSAO have a GPU time; depth, colour and motion vectors are measured in the Editor only. Release players have no profiler markers: all NaN.
- `GpuMarkerProbe` lists the markers that have a GPU time in the running build; run it again after a Unity or URP upgrade to check the marker names.

## Rules the package follows

- No managed allocation per frame; every per-frame entry point has an allocation test.
- No GPU readback in the frame, except the non-blocking debug statistics.
- No reference to terrain code or to anything outside the package.
- Hand-written HLSL; every scatter shader has the five passes above.
