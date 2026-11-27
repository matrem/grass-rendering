using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Phase 1 spike cull (design 6.2, per instance instead of per cluster): owns the visible list with its
    /// regions, the region counters and the draw arguments, and records the kernels of SpikeCull.compute.
    /// Buffers are allocated in the constructor; recording a frame does not allocate.
    /// </summary>
    public sealed class SpikeCuller : IDisposable
    {
        /// <summary>SPIKE_THREAD_GROUP_SIZE in SpikeCommon.hlsl.</summary>
        public const int ThreadGroupSize = 64;

        /// <summary>
        /// SPIKE_ARGS_UINTS_PER_DRAW in SpikeCommon.hlsl: uints between two draws in <see cref="Args"/>. Each draw
        /// writes <see cref="ArgsUIntsWritten"/> uints. 80 bytes is a multiple of the 20-byte
        /// <see cref="GraphicsBuffer.IndirectDrawIndexedArgs"/> and the 16-byte <see cref="GraphicsBuffer.IndirectDrawArgs"/>,
        /// which the Graphics.Render*Indirect calls address by command index (<see cref="GetCommandIndex"/>).
        /// </summary>
        public const int ArgsUIntsPerDraw = 20;

        /// <summary>Uints written per draw: an <see cref="GraphicsBuffer.IndirectDrawIndexedArgs"/>.</summary>
        public const int ArgsUIntsWritten = 5;

        /// <summary>SPIKE_EMPTY_ENTRY in SpikeCommon.hlsl: visible list padding after the last blade of a K-blade draw.</summary>
        public const uint EmptyEntry = 0xFFFFFFFF;

        /// <summary>Largest K (blades per instance) of procedural indexed draws. Region capacities are multiples of it.</summary>
        public const int MaxBladesPerInstance = 64;

        public const int FrustumPlaneCount = 6;

        private const int VisibleEntryBytes = sizeof(uint);
        private const int MaxDispatchWidthGroups = 1024;

        private static readonly int s_InstancesOut = Shader.PropertyToID("_SpikeInstancesOut");
        private static readonly int s_Instances = Shader.PropertyToID("_SpikeInstances");
        private static readonly int s_InstanceCount = Shader.PropertyToID("_SpikeInstanceCount");
        private static readonly int s_DispatchWidthGroups = Shader.PropertyToID("_SpikeDispatchWidthGroups");
        private static readonly int s_PlacementArea = Shader.PropertyToID("_SpikePlacementArea");
        private static readonly int s_PlacementSeed = Shader.PropertyToID("_SpikePlacementSeed");
        private static readonly int s_Regions = Shader.PropertyToID("_SpikeRegions");
        private static readonly int s_Counters = Shader.PropertyToID("_SpikeCounters");
        private static readonly int s_CounterCount = Shader.PropertyToID("_SpikeCounterCount");
        private static readonly int s_OverflowCounter = Shader.PropertyToID("_SpikeOverflowCounter");
        private static readonly int s_VisibleList = Shader.PropertyToID("_SpikeVisibleList");
        private static readonly int s_FrustumPlanes = Shader.PropertyToID("_SpikeFrustumPlanes");
        private static readonly int s_CameraPosition = Shader.PropertyToID("_SpikeCameraPosition");
        private static readonly int s_Lod1Distance = Shader.PropertyToID("_SpikeLod1DistanceMeters");
        private static readonly int s_MaxDistance = Shader.PropertyToID("_SpikeMaxDistanceMeters");
        private static readonly int s_BoundingRadius = Shader.PropertyToID("_SpikeBoundingRadiusMeters");
        private static readonly int s_Lod0Region = Shader.PropertyToID("_SpikeLod0Region");
        private static readonly int s_Lod1Region = Shader.PropertyToID("_SpikeLod1Region");
        private static readonly int s_Draws = Shader.PropertyToID("_SpikeDraws");
        private static readonly int s_Args = Shader.PropertyToID("_SpikeArgs");
        private static readonly int s_DrawCount = Shader.PropertyToID("_SpikeDrawCount");

        private readonly ComputeShader _compute;
        private readonly int _placeKernel;
        private readonly int _resetKernel;
        private readonly int _cullKernel;
        private readonly int _finalizeKernel;
        private readonly int[] _regionStarts;
        private readonly int[] _regionCapacities;
        private readonly Vector4[] _frustumPlanes = new Vector4[FrustumPlaneCount];
        private Vector4 _cameraPosition;
        private GraphicsBuffer _regions;
        private GraphicsBuffer _draws;
        private int _drawCount;

        /// <param name="regionCapacities">Entries per region, rounded up to a multiple of <see cref="MaxBladesPerInstance"/>.</param>
        /// <param name="maxDrawCount">Largest number of draws given to <see cref="SetDraws"/>.</param>
        public SpikeCuller(ComputeShader compute, int[] regionCapacities, int maxDrawCount)
        {
            _compute = compute;
            _placeKernel = compute.FindKernel("PlaceSpikeInstances");
            _resetKernel = compute.FindKernel("ResetSpikeCounters");
            _cullKernel = compute.FindKernel("CullSpikeInstances");
            _finalizeKernel = compute.FindKernel("FinalizeSpikeArgs");

            RegionCount = regionCapacities.Length;
            _regionStarts = new int[RegionCount];
            _regionCapacities = new int[RegionCount];
            var regionTable = new uint[RegionCount * 2];
            int totalEntries = 0;
            for (int region = 0; region < RegionCount; region++)
            {
                int capacity = RoundUp(Mathf.Max(regionCapacities[region], 1), MaxBladesPerInstance);
                _regionStarts[region] = totalEntries;
                _regionCapacities[region] = capacity;
                regionTable[region * 2] = (uint)totalEntries;
                regionTable[region * 2 + 1] = (uint)capacity;
                totalEntries += capacity;
            }

            VisibleEntryCount = totalEntries;
            // Raw: BRG binds the visible list as a ByteAddressBuffer (unity_DOTSInstancing_IndirectInstanceVisibility).
            VisibleList = new GraphicsBuffer(GraphicsBuffer.Target.Raw, totalEntries, VisibleEntryBytes) { name = "Scatter Spike Visible List" };
            _regions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, RegionCount, 2 * sizeof(uint)) { name = "Scatter Spike Regions" };
            _regions.SetData(regionTable);
            Counters = new GraphicsBuffer(GraphicsBuffer.Target.Structured, RegionCount + 1, sizeof(uint)) { name = "Scatter Spike Counters" };
            Counters.SetData(new uint[RegionCount + 1]);

            MaxDrawCount = Mathf.Max(maxDrawCount, 1);
            _draws = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxDrawCount, SpikeDrawDescriptor.SizeBytes) { name = "Scatter Spike Draws" };
            Args = new GraphicsBuffer(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.IndirectArguments, MaxDrawCount * ArgsUIntsPerDraw, sizeof(uint))
            {
                name = "Scatter Spike Draw Args",
            };
            Args.SetData(new uint[MaxDrawCount * ArgsUIntsPerDraw]);
        }

        public int RegionCount { get; }

        public int MaxDrawCount { get; }

        public int VisibleEntryCount { get; }

        /// <summary>Visible list: 4-byte instance indices, one region per (layer, LOD).</summary>
        public GraphicsBuffer VisibleList { get; private set; }

        /// <summary>Draw arguments, <see cref="ArgsUIntsPerDraw"/> uints per draw.</summary>
        public GraphicsBuffer Args { get; private set; }

        /// <summary>Per region: entries requested this frame (may exceed the capacity); last element: entries skipped by overflow.</summary>
        public GraphicsBuffer Counters { get; private set; }

        /// <summary>Index of the overflow counter in <see cref="Counters"/>.</summary>
        public int OverflowCounterIndex => RegionCount;

        public int GetRegionStart(int region)
        {
            return _regionStarts[region];
        }

        public int GetRegionCapacity(int region)
        {
            return _regionCapacities[region];
        }

        public static int GetArgsOffsetBytes(int draw)
        {
            return draw * ArgsUIntsPerDraw * sizeof(uint);
        }

        /// <summary>
        /// Index of the arguments of <paramref name="draw"/> for the Graphics.Render*Indirect calls, which count in
        /// commands of <paramref name="commandSizeBytes"/> (<see cref="GraphicsBuffer.IndirectDrawIndexedArgs.size"/> or
        /// <see cref="GraphicsBuffer.IndirectDrawArgs.size"/>).
        /// </summary>
        public static int GetCommandIndex(int draw, int commandSizeBytes)
        {
            return GetArgsOffsetBytes(draw) / commandSizeBytes;
        }

        /// <summary>Sets the draws whose arguments the finalize kernel writes. Settings change only: uploads data.</summary>
        public void SetDraws(SpikeDrawDescriptor[] draws, int count)
        {
            Debug.Assert(count <= MaxDrawCount, "More spike draws than the culler was created for.");
            _drawCount = Mathf.Min(count, MaxDrawCount);
            _draws.SetData(draws, 0, 0, _drawCount);
        }

        /// <summary>Places <paramref name="count"/> instances evenly (R2 sequence) over an XZ area. Startup only.</summary>
        public void RecordPlacement(CommandBuffer cmd, SpikeInstanceSet instances, int count, Rect areaXZ, uint seed)
        {
            count = Mathf.Min(count, instances.Capacity);
            if (count <= 0)
            {
                return;
            }

            cmd.SetComputeBufferParam(_compute, _placeKernel, s_InstancesOut, instances.Buffer);
            cmd.SetComputeIntParam(_compute, s_InstanceCount, count);
            cmd.SetComputeVectorParam(_compute, s_PlacementArea, new Vector4(areaXZ.x, areaXZ.y, areaXZ.width, areaXZ.height));
            cmd.SetComputeIntParam(_compute, s_PlacementSeed, unchecked((int)seed));
            DispatchPerInstance(cmd, _placeKernel, count);
        }

        /// <summary>Camera of the next <see cref="RecordCull"/> calls: position and frustum planes.</summary>
        public void SetView(Vector3 cameraPosition, Plane[] frustumPlanes)
        {
            _cameraPosition = cameraPosition;
            for (int i = 0; i < FrustumPlaneCount; i++)
            {
                Plane plane = frustumPlanes[i];
                _frustumPlanes[i] = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            }
        }

        /// <summary>Step 0 of design 6.2: resets the region counters. Record once per frame, before the culls.</summary>
        public void RecordReset(CommandBuffer cmd)
        {
            cmd.SetComputeBufferParam(_compute, _resetKernel, s_Counters, Counters);
            cmd.SetComputeIntParam(_compute, s_CounterCount, RegionCount + 1);
            cmd.DispatchCompute(_compute, _resetKernel, DivideRoundUp(RegionCount + 1, ThreadGroupSize), 1, 1);
        }

        /// <summary>Culls the first <paramref name="count"/> instances of a layer into its regions.</summary>
        public void RecordCull(CommandBuffer cmd, SpikeInstanceSet instances, int count, in SpikeCullLayer layer)
        {
            count = Mathf.Min(count, instances.Capacity);
            if (count <= 0)
            {
                return;
            }

            cmd.SetComputeBufferParam(_compute, _cullKernel, s_Instances, instances.Buffer);
            cmd.SetComputeBufferParam(_compute, _cullKernel, s_Regions, _regions);
            cmd.SetComputeBufferParam(_compute, _cullKernel, s_Counters, Counters);
            cmd.SetComputeBufferParam(_compute, _cullKernel, s_VisibleList, VisibleList);
            cmd.SetComputeIntParam(_compute, s_InstanceCount, count);
            cmd.SetComputeIntParam(_compute, s_OverflowCounter, OverflowCounterIndex);
            cmd.SetComputeVectorArrayParam(_compute, s_FrustumPlanes, _frustumPlanes);
            cmd.SetComputeVectorParam(_compute, s_CameraPosition, _cameraPosition);
            cmd.SetComputeFloatParam(_compute, s_Lod1Distance, layer.Lod1DistanceMeters);
            cmd.SetComputeFloatParam(_compute, s_MaxDistance, layer.MaxDistanceMeters);
            cmd.SetComputeFloatParam(_compute, s_BoundingRadius, layer.BoundingRadiusMeters);
            cmd.SetComputeIntParam(_compute, s_Lod0Region, layer.Lod0Region);
            cmd.SetComputeIntParam(_compute, s_Lod1Region, layer.Lod1Region);
            DispatchPerInstance(cmd, _cullKernel, count);
        }

        /// <summary>Writes the draw arguments of every draw from the region counters. Record after the culls.</summary>
        public void RecordFinalize(CommandBuffer cmd)
        {
            if (_drawCount == 0)
            {
                return;
            }

            cmd.SetComputeBufferParam(_compute, _finalizeKernel, s_Draws, _draws);
            cmd.SetComputeBufferParam(_compute, _finalizeKernel, s_Regions, _regions);
            cmd.SetComputeBufferParam(_compute, _finalizeKernel, s_Counters, Counters);
            cmd.SetComputeBufferParam(_compute, _finalizeKernel, s_VisibleList, VisibleList);
            cmd.SetComputeBufferParam(_compute, _finalizeKernel, s_Args, Args);
            cmd.SetComputeIntParam(_compute, s_DrawCount, _drawCount);
            cmd.DispatchCompute(_compute, _finalizeKernel, DivideRoundUp(_drawCount, ThreadGroupSize), 1, 1);
        }

        public void Dispose()
        {
            VisibleList?.Release();
            VisibleList = null;
            Args?.Release();
            Args = null;
            Counters?.Release();
            Counters = null;
            _regions?.Release();
            _regions = null;
            _draws?.Release();
            _draws = null;
        }

        private void DispatchPerInstance(CommandBuffer cmd, int kernel, int count)
        {
            // 2D dispatch: millions of instances exceed the 65535 groups allowed in one dimension.
            int groups = DivideRoundUp(count, ThreadGroupSize);
            int width = Mathf.Min(groups, MaxDispatchWidthGroups);
            cmd.SetComputeIntParam(_compute, s_DispatchWidthGroups, width);
            cmd.DispatchCompute(_compute, kernel, width, DivideRoundUp(groups, width), 1);
        }

        private static int DivideRoundUp(int value, int divisor)
        {
            return (value + divisor - 1) / divisor;
        }

        private static int RoundUp(int value, int multiple)
        {
            return DivideRoundUp(value, multiple) * multiple;
        }
    }
}
