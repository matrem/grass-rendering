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
        private long _callbackTicks;

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

        /// <summary>Shadow cascades of the blade draws (Phase 1b, design 6.2).</summary>
        public SpikeShadowCasters ShadowCasters { get; set; }

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

            CountDraws(isShadowView, splitCount, out int indirectCount, out int proceduralCount);
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

            int indirectIndex = 0;
            int proceduralIndex = 0;
            for (int i = 0; i < _drawCount; i++)
            {
                ref SpikeBrgDraw draw = ref _draws[i];
                ushort splitMask = GetSplitMask(ref draw, isShadowView, splitCount);
                if (splitMask == 0)
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

        /// <summary>CPU time spent in the culling callback (every view) since the previous call, in milliseconds; resets it.</summary>
        public float TakeCallbackCpuMs()
        {
            float milliseconds = (float)(_callbackTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            _callbackTicks = 0;
            return milliseconds;
        }

        private JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            WriteDrawCommands(cullingContext.viewType, cullingContext.cullingSplits.Length, cullingOutput.drawCommands);
            _callbackTicks += System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
            return default;
        }

        /// <summary>
        /// Splits the draw is visible in; 0 = not submitted. Camera: the single split. Shadow views: every split
        /// (cascade) for casters, except blade draws, which follow <see cref="ShadowCasters"/> (design 6.2).
        /// </summary>
        private ushort GetSplitMask(ref SpikeBrgDraw draw, bool isShadowView, int splitCount)
        {
            if (!draw.Enabled)
            {
                return 0;
            }

            if (!isShadowView)
            {
                return SpikeShadowCasterMask.FirstSplits(Mathf.Max(splitCount, 1));
            }

            if (!draw.CastShadows)
            {
                return 0;
            }

            return draw.IsBlade
                ? SpikeShadowCasterMask.GetSplitMask(ShadowCasters, draw.Lod, splitCount)
                : SpikeShadowCasterMask.FirstSplits(Mathf.Max(splitCount, 1));
        }

        private void CountDraws(bool isShadowView, int splitCount, out int indirectCount, out int proceduralCount)
        {
            indirectCount = 0;
            proceduralCount = 0;
            for (int i = 0; i < _drawCount; i++)
            {
                ref SpikeBrgDraw draw = ref _draws[i];
                if (GetSplitMask(ref draw, isShadowView, splitCount) == 0)
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
