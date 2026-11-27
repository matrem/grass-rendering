# grass-rendering

Prototype of a **GPU-driven procedural scatter system** (grass, flowers, moss, small rocks, bushes) for Unity 6.6 URP Forward+, drawn with BatchRendererGroup indirect draws. It is built to measure the approach and choose the production architecture for a streamed open world.

| | |
|---|---|
| Engine | Unity 6.6 (6000.6.10f1), URP 17.6, Forward+, Linear colour |
| Platform | Windows PC, Direct3D 12 (Vulkan checked) |
| GPUs | Test: RTX 3060 Ti. Target: RTX 5070 Ti |
| Status | Phase 0 (project setup) done. Phase 1 (spikes) in progress. Details in the [implementation plan](Docs/GrassPrototype_Implementation.md#progress). |

## Which document to read

| I want to... | Read |
|---|---|
| Understand what exists today and how it works | [Docs/ProjectGuide.md](Docs/ProjectGuide.md) |
| Understand the target architecture and why | [Docs/GrassPrototype_Design.md](Docs/GrassPrototype_Design.md) (section 2 is the vocabulary) |
| Know what is done and what comes next | [Docs/GrassPrototype_Implementation.md](Docs/GrassPrototype_Implementation.md) |
| Write code in this project | [Docs/CodingRules.md](Docs/CodingRules.md) |
| Use the scatter package in another project | [Packages/com.gaiagames.scatter/README.md](Packages/com.gaiagames.scatter/README.md) |

## Quick start

1. Open the project with Unity **6000.6.10f1** (Git LFS must be installed before cloning).
2. Open `Assets/Scenes/Bootstrap.unity` and press Play.
3. Controls:

| Input | Action |
|---|---|
| W A S D | Move |
| Q / E | Down / up |
| Right mouse button | Look around |
| Shift | Move faster |
| Mouse wheel | Change the base speed |
| F1 | Show or hide the debug HUD |
| F2 | Start or stop CSV recording of frame timings |

## Folder map

```
Packages/com.gaiagames.scatter/   The scatter system (embedded package, no dependency on Assets)
  Runtime/                        Runtime code; Diagnostics/ holds the debug HUD and the CSV logger
  Editor/                         Editor tools
  Shaders/                        Scatter shaders (from Phase 1)
  Tests/                          Edit Mode and Play Mode tests
Assets/Prototype/                 Prototype-only code: fly camera, build script (later: mock terrain, player)
Assets/Scenes/                    Test scenes (Bootstrap for now)
Assets/Settings/                  URP assets and the ScatterQuality asset of each quality tier
Docs/                             Design, implementation plan, project guide, coding rules
```

## Command line

Close the Unity Editor first: batch mode cannot open a project that is already open.

`Unity.exe` is a Windows GUI application: it prints nothing in the terminal, and PowerShell does not wait for it unless you use `Start-Process -Wait`. Everything goes to the log file. Use **absolute** paths for `-testResults` and `-logFile` (relative paths are resolved from the project folder); the commands below write into `Logs/`, which git ignores.

**Tests.** Three ways to run them, all useful:

| Run | `-testPlatform` | What runs | When |
|---|---|---|---|
| Edit Mode | `EditMode` | `Scatter.Tests`, in the Editor, without Play mode | On every change (fast) |
| Play Mode in the Editor | `PlayMode` | `Scatter.Tests.PlayMode`, in the Editor, in Play mode | When code depends on frames (MonoBehaviours, GPU work) |
| Play Mode in a player | `StandaloneWindows64` | `Scatter.Tests.PlayMode`, in a temporary Windows player that Unity builds, runs and closes | Before closing a phase, and for rendering work: shader variant stripping, frame timing and other player-only behaviour |

```powershell
$unity = "<Unity install>\6000.6.10f1\Editor\Unity.exe"
$project = "<path to this repository>"
$out = "$project\Logs"

# Runs the tests of one platform and prints a summary.
# Exit code: 0 = all tests passed, 2 = some tests failed, 1 = error (read the log).
# Never add -nographics: GPU tests need a graphics device.
function Invoke-UnityTests($platform) {
    $xml = "$out\Tests_$platform.xml"; $log = "$out\Tests_$platform.log"
    Remove-Item $xml -ErrorAction SilentlyContinue
    $run = Start-Process $unity -Wait -PassThru -ArgumentList "-batchmode", "-projectPath", "`"$project`"", "-runTests", "-testPlatform", $platform, "-testResults", "`"$xml`"", "-logFile", "`"$log`""
    if (-not (Test-Path $xml)) { "$platform - exit code $($run.ExitCode), no results: read $log"; return }
    $summary = ([xml](Get-Content $xml)).'test-run'
    "$platform - exit code $($run.ExitCode): $($summary.result), $($summary.passed)/$($summary.total) passed"
}

Invoke-UnityTests EditMode
Invoke-UnityTests PlayMode
Invoke-UnityTests StandaloneWindows64
```

**Prototype player.** Builds the scenes of the build settings into `Builds/Windows` (ignored by git); it runs no test. Exit code 0 = success.

```powershell
$run = Start-Process $unity -Wait -PassThru -ArgumentList "-batchmode", "-quit", "-projectPath", "`"$project`"", "-executeMethod", "Prototype.Editor.PrototypeBuild.BuildWindows", "-logFile", "`"$out\Build.log`""
"Exit code $($run.ExitCode)"; Select-String -Path "$out\Build.log" -Pattern "^Build (Succeeded|Failed)"
```

To follow a run live, in a second terminal: `Get-Content "$out\Tests_EditMode.log" -Wait -Tail 20` (or the log of the run in progress).

In the Editor: **Window > General > Test Runner** (Edit Mode and Play Mode tabs; **Run all in player** for the player run), and **Scatter Prototype > Build Windows Player** for the build.
