using UnityEngine;
using UnityEngine.Rendering;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Phase 1 spike baseline backend (design 6.3): the same draws as <see cref="SpikeBrgRenderer"/>, submitted every
    /// frame with Graphics.RenderMeshIndirect, RenderPrimitivesIndirect and RenderPrimitivesIndexedIndirect. The shaders
    /// read the visible list through the material (non-DOTS path of SpikeVisibleEntry.hlsl). Submitting does not allocate.
    /// </summary>
    public sealed class SpikeIndirectRenderer
    {
        private readonly SpikeCuller _culler;
        private readonly SpikeIndirectDraw[] _draws;
        private int _drawCount;

        public SpikeIndirectRenderer(SpikeCuller culler, int maxDrawCount)
        {
            _culler = culler;
            _draws = new SpikeIndirectDraw[maxDrawCount];
        }

        public int DrawCount => _drawCount;

        /// <summary>Adds a draw; returns its index. Startup only.</summary>
        public int AddDraw(in SpikeIndirectDraw draw)
        {
            Debug.Assert(_drawCount < _draws.Length, "More spike indirect draws than the renderer was created for.");
            _draws[_drawCount] = draw;
            return _drawCount++;
        }

        public void SetDrawEnabled(int draw, bool enabled)
        {
            _draws[draw].Enabled = enabled;
        }

        public bool IsDrawEnabled(int draw)
        {
            return _draws[draw].Enabled;
        }

        /// <summary>Shadow casting of one draw: in every cascade or none (RenderParams cannot limit the cascades).</summary>
        public void SetDrawCastShadows(int draw, bool castShadows)
        {
            _draws[draw].CastShadows = castShadows;
        }

        public bool DoesDrawCastShadows(int draw)
        {
            return _draws[draw].CastShadows;
        }

        /// <summary>
        /// Queues the enabled draws for every camera of this frame. Call once per frame, after the cull was recorded.
        /// <paramref name="worldBounds"/> is only used by Unity to cull the whole draw; the cull compute does the real work.
        /// </summary>
        public void Submit(Bounds worldBounds)
        {
            GraphicsBuffer args = _culler.Args;
            for (int i = 0; i < _drawCount; i++)
            {
                ref SpikeIndirectDraw draw = ref _draws[i];
                if (!draw.Enabled)
                {
                    continue;
                }

                var renderParams = new RenderParams(draw.Material)
                {
                    worldBounds = worldBounds,
                    shadowCastingMode = draw.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    motionVectorMode = MotionVectorGenerationMode.Object,
                };

                switch (draw.Type)
                {
                    case SpikeDrawType.MeshIndirect:
                        Graphics.RenderMeshIndirect(renderParams, draw.Mesh, args, 1,
                            SpikeCuller.GetCommandIndex(draw.ArgsDraw, GraphicsBuffer.IndirectDrawIndexedArgs.size));
                        break;
                    case SpikeDrawType.ProceduralIndirect:
                        Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Triangles, args, 1,
                            SpikeCuller.GetCommandIndex(draw.ArgsDraw, GraphicsBuffer.IndirectDrawArgs.size));
                        break;
                    default:
                        Graphics.RenderPrimitivesIndexedIndirect(renderParams, MeshTopology.Triangles, draw.IndexBuffer, args, 1,
                            SpikeCuller.GetCommandIndex(draw.ArgsDraw, GraphicsBuffer.IndirectDrawIndexedArgs.size));
                        break;
                }
            }
        }
    }
}
