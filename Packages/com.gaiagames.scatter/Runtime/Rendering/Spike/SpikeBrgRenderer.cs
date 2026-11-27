using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Phase 1 spike BRG backend (design 6.3): a <see cref="BatchRendererGroup"/> with one batch and no
    /// per-instance metadata, submitting a fixed set of indirect draws whose instance counts are written by
    /// <see cref="SpikeCuller"/>. Shaders read the instance data through the index BRG provides.
    /// The culling callback writes the commands on the main thread and does not allocate managed memory.
    /// </summary>
    public sealed unsafe class SpikeBrgRenderer : IDisposable
    {
        private const int BatchBufferUInts = 16;
        private const uint AllRenderingLayers = 0xFFFFFFFF;

        private static readonly BatchCullingViewType[] s_ViewTypes = { BatchCullingViewType.Camera, BatchCullingViewType.Light };

        private readonly SpikeCuller _culler;
        private readonly SpikeBrgDraw[] _draws;
        private BatchRendererGroup _rendererGroup;
        private GraphicsBuffer _batchBuffer;
        private BatchID _batchID;
        private int _drawCount;

        public SpikeBrgRenderer(SpikeCuller culler, Bounds worldBounds, int maxDrawCount)
        {
            _culler = culler;
            _draws = new SpikeBrgDraw[maxDrawCount];
            _rendererGroup = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);
            _rendererGroup.SetEnabledViewTypes(s_ViewTypes);
            _rendererGroup.SetGlobalBounds(worldBounds);

            // The shaders use no per-instance BRG metadata: one batch with an empty metadata list and a minimal buffer.
            GraphicsBuffer.Target target = BatchRendererGroup.BufferTarget == BatchBufferTarget.ConstantBuffer
                ? GraphicsBuffer.Target.Constant
                : GraphicsBuffer.Target.Raw;
            _batchBuffer = new GraphicsBuffer(target, BatchBufferUInts, sizeof(uint)) { name = "Scatter Spike BRG Batch" };
            using (var metadata = new NativeArray<MetadataValue>(0, Allocator.Temp))
            {
                _batchID = _rendererGroup.AddBatch(metadata, _batchBuffer.bufferHandle);
            }
        }

        public SpikeRegionOffsetMode OffsetMode { get; set; }

        public int DrawCount => _drawCount;

        public BatchMeshID RegisterMesh(Mesh mesh)
        {
            return _rendererGroup.RegisterMesh(mesh);
        }

        public BatchMaterialID RegisterMaterial(Material material)
        {
            return _rendererGroup.RegisterMaterial(material);
        }

        /// <summary>Adds a draw; returns its index. Startup only.</summary>
        public int AddDraw(in SpikeBrgDraw draw)
        {
            Debug.Assert(_drawCount < _draws.Length, "More spike BRG draws than the renderer was created for.");
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

        public void Dispose()
        {
            if (_rendererGroup != null)
            {
                _rendererGroup.RemoveBatch(_batchID);
                _rendererGroup.Dispose();
                _rendererGroup = null;
            }

            _batchBuffer?.Release();
            _batchBuffer = null;
        }

        /// <summary>
        /// Writes the draw commands of one culling request into <paramref name="output"/>[0]. The arrays are
        /// allocated with <see cref="Allocator.TempJob"/>; Unity frees them after rendering.
        /// </summary>
        internal void WriteDrawCommands(BatchCullingViewType viewType, int splitCount, NativeArray<BatchCullingOutputDrawCommands> output)
        {
            bool isShadowView = viewType == BatchCullingViewType.Light;
            if (!isShadowView && viewType != BatchCullingViewType.Camera)
            {
                output[0] = default;
                return;
            }

            CountDraws(isShadowView, out int indirectCount, out int proceduralCount);
            int rangeCount = (indirectCount > 0 ? 1 : 0) + (proceduralCount > 0 ? 1 : 0);
            var commands = new BatchCullingOutputDrawCommands
            {
                indirectDrawCommandCount = indirectCount,
                indirectDrawCommands = Malloc<BatchDrawCommandIndirect>(indirectCount),
                proceduralIndirectDrawCommandCount = proceduralCount,
                proceduralIndirectDrawCommands = Malloc<BatchDrawCommandProceduralIndirect>(proceduralCount),
                drawRangeCount = rangeCount,
                drawRanges = Malloc<BatchDrawRange>(rangeCount),
            };

            // Shadow views: one command visible in every split (cascade). Design 6.2 limits this to the first cascades later.
            var splitMask = (ushort)((1 << Mathf.Max(splitCount, 1)) - 1);
            int indirectIndex = 0;
            int proceduralIndex = 0;
            for (int i = 0; i < _drawCount; i++)
            {
                ref SpikeBrgDraw draw = ref _draws[i];
                if (!draw.Enabled || (isShadowView && !draw.CastShadows))
                {
                    continue;
                }

                GetVisibleWindow(draw.Region, out uint visibleOffset, out uint windowOffset, out uint windowSizeBytes);
                if (draw.Type == SpikeDrawType.MeshIndirect)
                {
                    commands.indirectDrawCommands[indirectIndex++] = new BatchDrawCommandIndirect
                    {
                        flags = BatchDrawCommandFlags.HasMotion,
                        batchID = _batchID,
                        materialID = draw.Material,
                        meshID = draw.Mesh,
                        topology = MeshTopology.Triangles,
                        splitVisibilityMask = splitMask,
                        visibleOffset = visibleOffset,
                        visibleInstancesBufferHandle = _culler.VisibleList.bufferHandle,
                        visibleInstancesBufferWindowOffset = windowOffset,
                        visibleInstancesBufferWindowSizeBytes = windowSizeBytes,
                        indirectArgsBufferHandle = _culler.Args.bufferHandle,
                        indirectArgsBufferOffset = (uint)SpikeCuller.GetArgsOffsetBytes(draw.ArgsDraw),
                    };
                }
                else
                {
                    commands.proceduralIndirectDrawCommands[proceduralIndex++] = new BatchDrawCommandProceduralIndirect
                    {
                        flags = BatchDrawCommandFlags.HasMotion,
                        batchID = _batchID,
                        materialID = draw.Material,
                        topology = MeshTopology.Triangles,
                        indexBufferHandle = draw.Type == SpikeDrawType.ProceduralIndexedIndirect ? draw.IndexBuffer : default,
                        splitVisibilityMask = splitMask,
                        visibleOffset = visibleOffset,
                        visibleInstancesBufferHandle = _culler.VisibleList.bufferHandle,
                        visibleInstancesBufferWindowOffset = windowOffset,
                        visibleInstancesBufferWindowSizeBytes = windowSizeBytes,
                        indirectArgsBufferHandle = _culler.Args.bufferHandle,
                        indirectArgsBufferOffset = (uint)SpikeCuller.GetArgsOffsetBytes(draw.ArgsDraw),
                    };
                }
            }

            int range = 0;
            if (indirectCount > 0)
            {
                commands.drawRanges[range++] = CreateRange(BatchDrawCommandType.Indirect, indirectCount);
            }

            if (proceduralCount > 0)
            {
                commands.drawRanges[range] = CreateRange(BatchDrawCommandType.ProceduralIndirect, proceduralCount);
            }

            output[0] = commands;
        }

        /// <summary>Frees what <see cref="WriteDrawCommands"/> allocated. Tests only: Unity frees the commands it renders.</summary>
        internal static void FreeDrawCommands(NativeArray<BatchCullingOutputDrawCommands> output)
        {
            BatchCullingOutputDrawCommands commands = output[0];
            Free(commands.indirectDrawCommands);
            Free(commands.proceduralIndirectDrawCommands);
            Free(commands.drawRanges);
            output[0] = default;
        }

        private JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            WriteDrawCommands(cullingContext.viewType, cullingContext.cullingSplits.Length, cullingOutput.drawCommands);
            return default;
        }

        private void CountDraws(bool isShadowView, out int indirectCount, out int proceduralCount)
        {
            indirectCount = 0;
            proceduralCount = 0;
            for (int i = 0; i < _drawCount; i++)
            {
                ref SpikeBrgDraw draw = ref _draws[i];
                if (!draw.Enabled || (isShadowView && !draw.CastShadows))
                {
                    continue;
                }

                if (draw.Type == SpikeDrawType.MeshIndirect)
                {
                    indirectCount++;
                }
                else
                {
                    proceduralCount++;
                }
            }
        }

        private void GetVisibleWindow(int region, out uint visibleOffset, out uint windowOffset, out uint windowSizeBytes)
        {
            var start = (uint)_culler.GetRegionStart(region);
            var capacityBytes = (uint)(_culler.GetRegionCapacity(region) * sizeof(uint));
            switch (OffsetMode)
            {
                case SpikeRegionOffsetMode.WindowOffsetBytes:
                    visibleOffset = 0;
                    windowOffset = start * sizeof(uint);
                    windowSizeBytes = capacityBytes;
                    break;
                case SpikeRegionOffsetMode.WindowOffsetEntries:
                    visibleOffset = 0;
                    windowOffset = start;
                    windowSizeBytes = capacityBytes;
                    break;
                default:
                    visibleOffset = start;
                    windowOffset = 0;
                    windowSizeBytes = 0;
                    break;
            }
        }

        private static BatchDrawRange CreateRange(BatchDrawCommandType type, int count)
        {
            return new BatchDrawRange
            {
                drawCommandsType = type,
                drawCommandsBegin = 0,
                drawCommandsCount = (uint)count,
                filterSettings = new BatchFilterSettings
                {
                    renderingLayerMask = AllRenderingLayers,
                    layer = 0,
                    motionMode = MotionVectorGenerationMode.Object,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    staticShadowCaster = false,
                    allDepthSorted = false,
                },
            };
        }

        private static T* Malloc<T>(int count) where T : unmanaged
        {
            if (count == 0)
            {
                return null;
            }

            return (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>(), Allocator.TempJob);
        }

        private static void Free(void* pointer)
        {
            if (pointer != null)
            {
                UnsafeUtility.Free(pointer, Allocator.TempJob);
            }
        }
    }
}
