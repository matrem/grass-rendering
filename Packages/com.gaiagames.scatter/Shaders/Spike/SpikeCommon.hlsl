#ifndef SCATTER_SPIKE_COMMON_INCLUDED
#define SCATTER_SPIKE_COMMON_INCLUDED

// Constants of the Phase 1 spike, shared by the cull compute and the spike shaders.
// Mirrored in C# by SpikeCuller and SpikeDrawType (Runtime/Rendering/Spike), checked by SpikeCullerTests.

// Threads per group of every spike kernel. Mirrored by SpikeCuller.ThreadGroupSize.
#define SPIKE_THREAD_GROUP_SIZE 64

// Entry written after the last visible blade of a region, up to the next multiple of K (procedural indexed
// draws draw K blades per instance). The vertex shader collapses blades with this entry.
// Mirrored by SpikeCuller.EmptyEntry.
#define SPIKE_EMPTY_ENTRY 0xFFFFFFFFu

// Draw types, mirrored by SpikeDrawType.
#define SPIKE_DRAW_MESH_INDIRECT 0u
#define SPIKE_DRAW_PROCEDURAL_INDIRECT 1u
#define SPIKE_DRAW_PROCEDURAL_INDEXED_INDIRECT 2u

// Uints per draw in the draw arguments buffer: the size of GraphicsBuffer.IndirectDrawIndexedArgs.
// Non-indexed procedural draws use the first 4. Mirrored by SpikeCuller.ArgsUIntsPerDraw.
#define SPIKE_ARGS_UINTS_PER_DRAW 5u

// PCG hash (Jarzynski and Olano, "Hash Functions for GPU Rendering", JCGT 2020).
uint SpikeHash(uint value)
{
    uint state = value * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

// Uniform value in [0, 1) from the 24 high bits of a hash.
float SpikeHashToUnit(uint hash)
{
    return (float)(hash >> 8u) * (1.0 / 16777216.0);
}

#endif
