using System.Runtime.InteropServices;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// One draw of the spike, read by the args finalize kernel. Mirrored in HLSL by
    /// Shaders/Spike/SpikeDrawDescriptor.hlsl; the size is checked by a unit test.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SpikeDrawDescriptor
    {
        public const int SizeBytes = 16;

        /// <summary>Index in the region table of the visible list.</summary>
        public uint Region;

        /// <summary>A <see cref="SpikeDrawType"/> value.</summary>
        public uint DrawType;

        /// <summary>Indices (mesh and indexed draws) or vertices (non-indexed procedural) per instance.</summary>
        public uint ElementsPerInstance;

        /// <summary>K: blades drawn by one instance; 1 for mesh and non-indexed procedural draws.</summary>
        public uint BladesPerInstance;

        public SpikeDrawDescriptor(int region, SpikeDrawType drawType, int elementsPerInstance, int bladesPerInstance)
        {
            Region = (uint)region;
            DrawType = (uint)drawType;
            ElementsPerInstance = (uint)elementsPerInstance;
            BladesPerInstance = (uint)bladesPerInstance;
        }
    }
}
