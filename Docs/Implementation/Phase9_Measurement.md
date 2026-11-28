# Phase 9 - Measurement and tuning

Part of the [implementation plan](README.md) (progress, rules, how the plan is kept up to date). Section numbers like 6.2 refer to the [design](../GrassPrototype_Design.md).

**Goal:** numbers against design section 12, on both GPUs.
1. [ ] Scripted camera paths (walk, run, fast fly) in the stress and open-world load scenes. Each path also runs **with scatter disabled**, as the baseline for the worst-frame metric.
2. [ ] Density sweep; CSV per quality tier, per backend, native vs STP. The CSV summary gives median, 99th percentile and worst frame time, and the maximum of each correctness counter (design 12).
3. [ ] 10-minute loop of the open-world path: GPU memory and GC allocation must not grow.
4. [ ] Tune: chunk/cluster sizes, ring distances, LOD distances, blade widths, generation budget, block pool sizes, depth priming on/off (8.1), number of cascades receiving near casters.
5. [ ] RenderDoc/PIX: rasterisation and shadow cost, compute timings, memory.
6. [ ] Optional experiments of design section 14, only if the profile points to them.

**Exit:** results table against every metric of design 12 (speed, stability, correctness), recommended settings per tier, backend decision.
