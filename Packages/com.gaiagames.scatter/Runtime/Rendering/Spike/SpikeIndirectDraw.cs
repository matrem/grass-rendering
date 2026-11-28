using UnityEngine;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// One draw submitted by <see cref="SpikeIndirectRenderer"/>: a region of the visible list drawn with one
    /// Graphics.Render*Indirect call, its arguments written by the cull compute. The material holds the region
    /// start and the visible list (_RegionStart, _SpikeVisibleList).
    /// </summary>
    public struct SpikeIndirectDraw
    {
        public SpikeDrawType Type;
        public Material Material;

        /// <summary>Mesh of <see cref="SpikeDrawType.MeshIndirect"/> draws; unused for procedural draws.</summary>
        public Mesh Mesh;

        /// <summary>Index buffer of <see cref="SpikeDrawType.ProceduralIndexedIndirect"/> draws; unused otherwise.</summary>
        public GraphicsBuffer IndexBuffer;

        /// <summary>Index of the draw in the culler draw arguments.</summary>
        public int ArgsDraw;

        public bool CastShadows;
        public bool Enabled;
    }
}
