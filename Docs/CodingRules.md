# Coding rules - GrassRendering

Rules for all code of the project. Goal: code that is coherent, easy to read, and fast, with **zero managed allocation per frame** on the CPU side.
Design section references (e.g. 12.1) point to `GrassPrototype_Design.md`.

---

## 1. Structure

- **The package is the product.** Scatter code lives in `Packages/com.gaiagames.scatter` and never references `Assets` or terrain code. Prototype-only code (mock terrain, player, test scenes) lives in `Assets`.
- **Namespaces follow folders:** `Scatter.Chunks`, `Scatter.Rendering`, `Scatter.Ground`, `Scatter.Diagnostics`... inside the assemblies of Phase 0 (`Scatter.Runtime`, `Scatter.Editor`, `Scatter.Tests`, `Scatter.Tests.PlayMode`, `Prototype`, `Prototype.Editor`). Never name a namespace `Debug`: it would hide `UnityEngine.Debug`.
- **One type per file**, file name = type name.
- **Thin MonoBehaviours.** A MonoBehaviour holds references and settings, and forwards to plain C# classes that do the work. Plain classes are testable in Edit Mode without a scene.
- **One driver per frame.** A single `ScatterSystem` runs the per-frame work in a fixed order (CPU chunk update, uploads, cull dispatch, draw submission). No `Update()` scattered over many components.
- **Settings in ScriptableObjects** (`ScatterLayer`, `ScatterQuality`...). No magic numbers in code: a value that can be tuned is a setting; a value that cannot is a named `const`.
- **One definition of shared data.** Structs read by both C# and HLSL (blade record, block table entry, layer parameters) are defined once in C# and once in HLSL, in files with the same name, side by side, each pointing to the other in a comment. Their size is checked by a unit test.

## 2. Naming and readability

- C#: `PascalCase` types, methods, properties, constants; `camelCase` locals and parameters; `_camelCase` private fields; `s_PascalCase` private static readonly fields (shader IDs, profiler markers); interfaces start with `I`. C# 9 (Unity's version): no file-scoped namespaces.
- Use the **design vocabulary** (section 2): `Chunk`, `Cluster`, `Ring`, `Block`, `Arena`, `BladeRecord`, `Rank`, `GroundPatch`... Never two names for one concept.
- **Units in names** when not obvious: `rangeMeters`, `gpuTimeMs`, `sizeBytes`, `densityPerSquareMeter`.
- Inspector fields: `[SerializeField] private`, with `[Tooltip]` giving the unit and `[Min]`/`[Range]` where it makes sense. `OnValidate` clamps invalid values.
- Short methods with one job. Early returns rather than deep nesting.
- Comments explain **why**, not what. Public API of the package has XML doc comments (`///`). Non-obvious code points to the design section it implements: `// Design 5.2: keep blades with rank < density.`
- `.editorconfig` at the project root defines formatting and naming; Rider and Visual Studio apply it. Do not fight it.

## 3. CPU performance and garbage collection

**Rule: the steady-state frame allocates 0 bytes of managed memory.** Allocation is allowed at startup, on settings change, and in editor code.

Forbidden in per-frame code:
| Do not | Do instead |
|---|---|
| `new` of classes, arrays, lists, strings | Allocate at startup from the quality tier; reuse; `Clear()` |
| LINQ | Plain `for` loops |
| String concatenation, interpolation, `ToString()`, `string.Format` | Strings built only when the debug HUD is visible, at a low rate (12.1) |
| Lambdas capturing variables; creating delegates (`Method` passed as a delegate allocates each time) | Cache delegates in fields at startup |
| `foreach` over an interface (`IEnumerable<T>`, `IList<T>`) | `for`, or `foreach` over a concrete `List<T>`/array/`NativeArray<T>` |
| Boxing: structs passed as `object` or as an interface; struct dictionary keys without `IEquatable<T>` | Struct keys (`ChunkKey`...) implement `IEquatable<T>` and `GetHashCode` |
| Coroutines, `yield return new WaitForSeconds` | Timers in the driver |
| `params` methods, `Enum.ToString()`, `GetType()`-based logic | Explicit overloads, lookup tables |
| `Debug.Log` in the frame | HUD counters (12.1) |

Unity APIs:
- `Shader.PropertyToID` once, in `static readonly int s_...` fields. Never set shader values by string.
- Use non-allocating overloads: `GeometryUtility.CalculateFrustumPlanes(camera, planes)` with a cached `Plane[]`, `GetComponents(list)`, `Physics.*NonAlloc`.
- Cache `Camera.main` and component references; use `CompareTag`, never `tag ==`.
- `CommandBuffer`s created once, `Clear()`ed and reused.
- GPU uploads from `NativeArray<T>` (`Allocator.Persistent`) with `GraphicsBuffer.SetData`, sized at startup; no managed array copies.
- `AsyncGPUReadback` (debug stats only) with a cached callback, into a persistent `NativeArray`.

CPU work:
- Keep per-frame CPU work proportional to chunks (hundreds), never to blades (millions): blade work belongs to the GPU.
- Plain C# first. Move a loop to Burst + Jobs only when the profiler shows it (design 10.2: measure first).
- Each CPU stage is wrapped in a `static readonly ProfilerMarker` (`using (s_Marker.Auto())`), named like the HUD timing rows.

How it is checked:
- **Unit tests:** every per-frame entry point (`ScatterSystem.Tick`, chunk manager update, cull submission...) has an Edit Mode test wrapping one warmed-up call in `Assert.That(() => ..., Is.Not.AllocatingGCMemory())` (Unity Test Framework).
- **HUD and CSV:** "GC Allocated In Frame" (`ProfilerRecorder`, Memory category) is shown in the HUD and logged in the CSV, in red when > 0. The visible HUD builds a few strings at a low rate, so acceptance runs use the CSV logger with the HUD hidden; the CSV logger writes into a preallocated buffer, flushed at the end of the run.
- Incremental GC stays enabled (Player Settings) as a safety net, not as a solution.

## 4. GPU and shader code

- Shared HLSL in `.hlsl` include files, used by both compute shaders and blade shaders: one decode of the blade record, one hash, one rank test.
- Shader properties and globals: `_PascalCase`. Kernels have descriptive names (`CullClusters`, `PlaceTerrainPoints`), never `CSMain`.
- Thread group sizes are constants defined in one HLSL file and mirrored in C#, checked by a unit test.
- Few keywords: global uniforms and branches on uniforms instead (debug views use one global value, 12.2).
- `half` where precision allows (colours, wind weights), `float` for positions.
- Every `GraphicsBuffer` and `RenderTexture` gets a `name` (visible in RenderDoc/PIX). Every GPU pass is inside a `ProfilingScope` named like the HUD row.
- All GPU resources are created at startup and released in `OnDisable`/`Dispose`. A play-mode smoke test checks for leaks after unload.

## 5. Unity specifics

- Support **Enter Play Mode without domain reload** (faster iteration): reset static state in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method.
- Classes owning native or GPU memory implement `IDisposable`, and their owner disposes them.
- Invariants are checked with `Debug.Assert` / `UnityEngine.Assertions` (stripped from release builds). No exceptions thrown in the frame.
- No `GameObject.Find`, no `FindObjectOfType` at runtime: references are serialized or passed in.

## 6. Before committing

- Unit tests pass (Edit Mode), including the no-allocation tests.
- No new compiler warnings.
- The implementation plan is updated (item ticked, notes), and the design if a decision changed (see `.github/copilot-instructions.md`).
- `Docs/ProjectGuide.md` describes the change (classes, assemblies, folders, settings, debug tools, tests).
