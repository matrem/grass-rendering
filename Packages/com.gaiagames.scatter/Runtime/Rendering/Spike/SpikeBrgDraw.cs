using UnityEngine;
using UnityEngine.Rendering;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// One indirect draw submitted by <see cref="SpikeBrgRenderer"/>: a region of the visible list drawn with
    /// one command type, its draw arguments written by the cull compute.
    /// </summary>
    public struct SpikeBrgDraw
    {
        public SpikeDrawType Type;
        public BatchMaterialID Material;

        /// <summary>Mesh of <see cref="SpikeDrawType.MeshIndirect"/> draws; unused for procedural draws.</summary>
        public BatchMeshID Mesh;

        /// <summary>Index buffer of <see cref="SpikeDrawType.ProceduralIndexedIndirect"/> draws; unused otherwise.</summary>
        public GraphicsBufferHandle IndexBuffer;

        /// <summary>Region of the visible list in the culler.</summary>
        public int Region;

        /// <summary>Index of the draw in the culler draw arguments.</summary>
        public int ArgsDraw;

        public bool CastShadows;
        public bool Enabled;

        /// <summary>Blade draw: its shadow cascades follow <see cref="SpikeBrgRenderer.ShadowCasters"/>. Other draws cast in every cascade.</summary>
        public bool IsBlade;

        /// <summary>Mesh LOD of a blade draw (0 or 1).</summary>
        public int Lod;
    }
}
