# Phase 1 measurements (S3)

RTX 3060 Ti, Direct3D 12, 2560x1440 fullscreen, development player of `Spike_Draw` (`Scatter Prototype > Build Spike Development Player`), Unity 6000.6.10f1, URP 17.6. Depth priming Auto, SSAO on, 4 shadow cascades (50 m), no TAA. Findings and analysis: Phase 1 notes of the [implementation plan](../../GrassPrototype_Implementation.md).

| File | Run | Content |
|---|---|---|
| `spike_benchmark_full_20261127_232809.csv` | `-spikeBenchmark` | **Reference run.** 58 cases: 2 views x (no scatter, BRG and Indirect x 3 blade draw types x 1M/2M/4M blades, K sweep 2 to 64 at 4M). Median and 99th percentile of 300 frames. Includes the URP camera, shadow and SSAO GPU times. |
| `spike_benchmark_20261127_222542.csv` | `-spikeBenchmark` | First full run, before the URP marker names were fixed: same cases, no URP pass times. Frame GPU times match the reference run within 0.2 ms. |
| `spike_benchmark_quick_20261127_223732.csv` | `-spikeBenchmarkQuick` | Tooling check: 1M blades, 60 frames per case. |
| `gpu_markers_20261127_225823.txt` | `-spikeTimingProbe` | Markers with a GPU time in a player, default threading (graphics jobs, `kGfxThreadingModeSplitJobs`). |
| `gpu_markers_20261127_231739.txt` | `-spikeTimingProbe -force-gfx-mt` | Same, render thread without graphics jobs (`kGfxThreadingModeThreaded`). |
| `gpu_markers_20261127_231853.txt` | `-spikeTimingProbe -force-gfx-direct` | Same, everything on the main thread (`kGfxThreadingModeDirect`). |

CSV columns: `SpikeBenchmarkCsv` and `SpikeBenchmarkMetric` (ProjectGuide 4.3). Scatter cost of a case = its value minus the case without scatter of the same view.
