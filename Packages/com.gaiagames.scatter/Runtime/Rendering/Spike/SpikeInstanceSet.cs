using System;
using UnityEngine;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// GPU buffer of spike instances (xyz = world position, w = yaw), filled once on the GPU by
    /// <see cref="SpikeCuller.RecordPlacement"/>, or by <see cref="GraphicsBuffer.SetData(Array)"/> in tests.
    /// </summary>
    public sealed class SpikeInstanceSet : IDisposable
    {
        public const int StrideBytes = 16;

        public SpikeInstanceSet(string name, int capacity)
        {
            Capacity = capacity;
            Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, StrideBytes) { name = name };
        }

        public GraphicsBuffer Buffer { get; private set; }

        public int Capacity { get; }

        public void Dispose()
        {
            Buffer?.Release();
            Buffer = null;
        }
    }
}
