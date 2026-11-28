# Phase 1b measurements (cost investigation)

RTX 3060 Ti, Direct3D 12, 2560x1440 fullscreen, development player of `Spike_Draw` (`Scatter Prototype > Build Spike Development Player`), Unity 6000.6.10f1, URP 17.6. Depth priming Auto, SSAO on, 4 shadow cascades (50 m), no TAA. BRG, procedural indexed draws, K = 16. 60 warm-up frames and 300 measured frames per case, median and 99th percentile. Findings and analysis: Phase 1b notes of the [implementation plan](../../GrassPrototype_Implementation.md).

| File | Run | Content |
|---|---|---|
| `spike_benchmark_resolution_noobjects_20261128_173356.csv` | `-spikeBenchmarkResolution -spikeBenchmarkNoObjects` | **Reference, blades only.** 36 cases: 2 views x render scale {1, 0.5, 0.25} x shadow map {2048, 512} x (no scatter, 1M blades, 4M blades). |
| `spike_benchmark_levers_noobjects_20261128_173509.csv` | `-spikeBenchmarkLevers -spikeBenchmarkNoObjects` | **Reference, blades only.** 32 cases: 2 views x (no scatter, 4M blades x density falloff {off, on, on with widening} x 5 shadow caster presets). |
| `spike_benchmark_resolution_20261128_172148.csv` | `-spikeBenchmarkResolution` | Same cases with the 16k rocks of the spike (Object layer: 515-vertex sphere, no falloff, casting in every cascade). |
| `spike_benchmark_levers_20261128_172401.csv` | `-spikeBenchmarkLevers` | Same cases with the rocks. |

How to read the CSVs:
- Columns: `SpikeBenchmarkCsv` and `SpikeBenchmarkMetric` (ProjectGuide 4.3, 4.4). `renderScale` and `shadowResolution` are the values applied during the case.
- Scatter cost of a case = its value minus the case without scatter of the same view (and, for the resolution run, of the same render scale and shadow map).
- Depth + colour = `cameraGpu` - `shadowsGpu` - `ssaoGpu`: players have no GPU time for the Render Graph depth and colour passes (design 12.1).
- Rocks cost = the run with rocks minus the run without, same case.
