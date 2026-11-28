#ifndef SCATTER_SPIKE_DRAW_DESCRIPTOR_INCLUDED
#define SCATTER_SPIKE_DRAW_DESCRIPTOR_INCLUDED

// One draw of the spike: which region of the visible list it draws and how its arguments are written.
// Mirrored in C# by Runtime/Rendering/Spike/SpikeDrawDescriptor.cs (16 bytes, checked by a unit test).
struct SpikeDrawDescriptor
{
    uint region;              // Index in the region table.
    uint drawType;            // SPIKE_DRAW_*.
    uint elementsPerInstance; // Indices (mesh and indexed draws) or vertices (non-indexed procedural) per instance.
    uint bladesPerInstance;   // K: blades drawn by one instance. 1 for mesh and non-indexed procedural draws.
};

#endif
