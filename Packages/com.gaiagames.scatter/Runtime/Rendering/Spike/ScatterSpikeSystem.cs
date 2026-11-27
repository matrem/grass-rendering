using System;
using System.Diagnostics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// The Phase 1 spike (Implementation Phase 1, S1 and S2): 1M+ blades and an Object layer, culled every frame into
    /// one visible list with three regions (blade LOD0, blade LOD1, objects) and drawn with one of three blade draw
    /// types, by BRG or by the Graphics.Render*Indirect baseline. Everything is allocated in the constructor;
    /// <see cref="Tick"/> does not allocate.
    /// </summary>
    public sealed class ScatterSpikeSystem : IDisposable
    {
        public const int BladeLod0Region = 0;
        public const int BladeLod1Region = 1;
        public const int ObjectRegion = 2;
        public const int RegionCount = 3;

        public const int Lod0SegmentCount = 3;
        public const int Lod1SegmentCount = 2;
        public const int LodCount = 2;

        /// <summary>Draws: 2 LODs for each of the 3 blade draw types, then the Object layer.</summary>
        public const int ObjectDraw = 6;
        public const int DrawCount = 7;

        private const float BladeBoundingRadiusMeters = 0.6f;
        private const float ObjectBoundingRadiusMeters = 0.5f;
        private const float BoundsHeightMeters = 10f;
        private const uint BladeSeed = 0x9E3779B9u;
        private const uint ObjectSeed = 0x85EBCA6Bu;

        private static readonly int s_Instances = Shader.PropertyToID("_SpikeInstances");
        private static readonly int s_VisibleList = Shader.PropertyToID("_SpikeVisibleList");
        private static readonly int s_BladeWidth = Shader.PropertyToID("_BladeWidth");
        private static readonly int s_BladeHeight = Shader.PropertyToID("_BladeHeight");
        private static readonly int s_BladeBend = Shader.PropertyToID("_BladeBend");
        private static readonly int s_SegmentCount = Shader.PropertyToID("_SegmentCount");
        private static readonly int s_BladesPerInstance = Shader.PropertyToID("_BladesPerInstance");
        private static readonly int s_Indexed = Shader.PropertyToID("_Indexed");
        private static readonly int s_RegionStart = Shader.PropertyToID("_RegionStart");
        private static readonly int s_DebugColor = Shader.PropertyToID("_DebugColor");

        // Debug view colours per draw (design 12.2): one hue per draw type, lighter for LOD1.
        private static readonly Color[] s_DebugColors =
        {
            new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.6f, 0.5f),
            new Color(0.2f, 0.8f, 0.2f), new Color(0.6f, 1f, 0.6f),
            new Color(0.2f, 0.4f, 1f), new Color(0.6f, 0.75f, 1f),
            new Color(0.9f, 0.9f, 0.9f),
        };

        private static readonly ProfilingSampler s_CullSampler = new ProfilingSampler("Scatter Cull");

        private readonly SpikeCuller _culler;
        private readonly SpikeBrgRenderer _brg;
        private readonly SpikeIndirectRenderer _indirect;
        private readonly Bounds _worldBounds;
        private readonly SpikeInstanceSet _blades;
        private readonly SpikeInstanceSet _objects;
        private readonly SpikeDrawDescriptor[] _drawDescriptors = new SpikeDrawDescriptor[DrawCount];
        private readonly Material[] _materials = new Material[DrawCount];
        private readonly Mesh[] _bladeMeshes = new Mesh[LodCount];
        private readonly GraphicsBuffer[] _multiBladeIndexBuffers = new GraphicsBuffer[LodCount];
        private readonly Plane[] _frustumPlanes = new Plane[SpikeCuller.FrustumPlaneCount];
        private readonly CommandBuffer _cmd = new CommandBuffer { name = "Scatter Spike" };
        private readonly Action<AsyncGPUReadbackRequest> _onCountersRead;
        private NativeArray<uint> _counterReadback;
        private NativeArray<uint> _counters;
        private bool _readbackPending;
        private ScatterSpikeConfig _config;

        public ScatterSpikeSystem(ScatterSpikeResources resources, in ScatterSpikeConfig config)
        {
            _config = config;
            _config.MaxBladeCount = Mathf.Max(config.MaxBladeCount, 1);
            _config.BladeCount = Mathf.Clamp(config.BladeCount, 0, _config.MaxBladeCount);
            _config.ObjectCount = Mathf.Max(config.ObjectCount, 1);
            _config.BladesPerInstance = Mathf.Clamp(config.BladesPerInstance, 1, SpikeCuller.MaxBladesPerInstance);

            // A region can hold every instance of its layer: the spike never overflows unless a test asks for it.
            var capacities = new int[RegionCount];
            capacities[BladeLod0Region] = _config.MaxBladeCount;
            capacities[BladeLod1Region] = _config.MaxBladeCount;
            capacities[ObjectRegion] = _config.ObjectCount;
            _culler = new SpikeCuller(resources.CullCompute, capacities, DrawCount);

            _blades = new SpikeInstanceSet("Scatter Spike Blades", _config.MaxBladeCount);
            _objects = new SpikeInstanceSet("Scatter Spike Objects", _config.ObjectCount);
            _cmd.Clear();
            _culler.RecordPlacement(_cmd, _blades, _config.MaxBladeCount, _config.AreaXZ, BladeSeed);
            _culler.RecordPlacement(_cmd, _objects, _config.ObjectCount, _config.AreaXZ, ObjectSeed);
            Graphics.ExecuteCommandBuffer(_cmd);

            Rect area = _config.AreaXZ;
            _worldBounds = new Bounds(
                new Vector3(area.center.x, BoundsHeightMeters * 0.5f, area.center.y),
                new Vector3(area.width, BoundsHeightMeters, area.height));
            _brg = new SpikeBrgRenderer(_culler, _worldBounds, DrawCount) { OffsetMode = _config.OffsetMode };
            _indirect = new SpikeIndirectRenderer(_culler, DrawCount);

            _bladeMeshes[0] = BladeMeshFactory.CreateBladeMesh(Lod0SegmentCount, "Scatter Spike Blade LOD0");
            _bladeMeshes[1] = BladeMeshFactory.CreateBladeMesh(Lod1SegmentCount, "Scatter Spike Blade LOD1");
            _multiBladeIndexBuffers[0] = BladeMeshFactory.CreateMultiBladeIndexBuffer(Lod0SegmentCount, SpikeCuller.MaxBladesPerInstance, "Scatter Spike K Blades LOD0");
            _multiBladeIndexBuffers[1] = BladeMeshFactory.CreateMultiBladeIndexBuffer(Lod1SegmentCount, SpikeCuller.MaxBladesPerInstance, "Scatter Spike K Blades LOD1");

            CreateBladeDraws(resources, SpikeDrawType.MeshIndirect);
            CreateBladeDraws(resources, SpikeDrawType.ProceduralIndirect);
            CreateBladeDraws(resources, SpikeDrawType.ProceduralIndexedIndirect);
            CreateObjectDraw(resources);
            _culler.SetDraws(_drawDescriptors, DrawCount);

            ApplyDrawSelection();
            _counterReadback = new NativeArray<uint>(RegionCount + 1, Allocator.Persistent);
            _counters = new NativeArray<uint>(RegionCount + 1, Allocator.Persistent);
            _onCountersRead = OnCountersRead;
            s_CullSampler.enableRecording = true;
        }

        public ScatterSpikeConfig Config => _config;

        public SpikeCuller Culler => _culler;

        /// <summary>GPU time of the cull compute (reset, culls, args), a few frames late; 0 until measured.</summary>
        public float CullGpuMs => s_CullSampler.gpuElapsedTime;

        /// <summary>CPU time of the last <see cref="Tick"/> (cull recording, and draw submission for the Indirect backend), in milliseconds.</summary>
        public float TickCpuMs { get; private set; }

        /// <summary>
        /// CPU time of the draw submission, in milliseconds: the Graphics.Render*Indirect calls of the last
        /// <see cref="Tick"/> (Indirect backend), or the BRG culling callbacks since the previous Tick (all views).
        /// </summary>
        public float SubmitCpuMs { get; private set; }

        /// <summary>
        /// Debug stats read back without blocking (design 12.1), a few frames late: requested entries per region,
        /// then entries skipped by overflow. Only refreshed while <see cref="Tick"/> is asked for stats.
        /// </summary>
        public uint GetCounter(int index)
        {
            return _counters[index];
        }

        public int GetDrawIndex(SpikeDrawType type, int lod)
        {
            return (int)type * LodCount + lod;
        }

        public void SetBladeDrawType(SpikeDrawType type)
        {
            _config.BladeDrawType = type;
            ApplyDrawSelection();
        }

        public void SetObjectsEnabled(bool enabled)
        {
            _config.ObjectsEnabled = enabled;
            ApplyDrawSelection();
        }

        public void SetBackend(SpikeBackend backend)
        {
            _config.Backend = backend;
            ApplyDrawSelection();
        }

        /// <summary>Off: no cull and no draw (baseline of the measurements); the buffers stay allocated.</summary>
        public void SetScatterEnabled(bool enabled)
        {
            _config.ScatterEnabled = enabled;
            ApplyDrawSelection();
        }

        public void SetOffsetMode(SpikeRegionOffsetMode mode)
        {
            _config.OffsetMode = mode;
            _brg.OffsetMode = mode;
        }

        public void SetBladeCount(int count)
        {
            _config.BladeCount = Mathf.Clamp(count, 0, _config.MaxBladeCount);
        }

        /// <summary>Sets K of the procedural indexed draws. Settings change: uploads the draw descriptors.</summary>
        public void SetBladesPerInstance(int bladesPerInstance)
        {
            _config.BladesPerInstance = Mathf.Clamp(bladesPerInstance, 1, SpikeCuller.MaxBladesPerInstance);
            for (int lod = 0; lod < LodCount; lod++)
            {
                int draw = GetDrawIndex(SpikeDrawType.ProceduralIndexedIndirect, lod);
                int segmentCount = lod == 0 ? Lod0SegmentCount : Lod1SegmentCount;
                _drawDescriptors[draw] = new SpikeDrawDescriptor(lod, SpikeDrawType.ProceduralIndexedIndirect,
                    BladeMeshFactory.GetIndexCount(segmentCount) * _config.BladesPerInstance, _config.BladesPerInstance);
                _materials[draw].SetFloat(s_BladesPerInstance, _config.BladesPerInstance);
            }

            _culler.SetDraws(_drawDescriptors, DrawCount);
        }

        /// <summary>Per-frame work: cull for <paramref name="camera"/>, then BRG draws the result in every view.</summary>
        public void Tick(Camera camera, bool readStats)
        {
            long startTicks = Stopwatch.GetTimestamp();
            float brgCallbackMs = _brg.TakeCallbackCpuMs();
            if (!_config.ScatterEnabled)
            {
                TickCpuMs = 0f;
                SubmitCpuMs = 0f;
                return;
            }

            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            _culler.SetView(camera.transform.position, _frustumPlanes);

            _cmd.Clear();
            using (new ProfilingScope(_cmd, s_CullSampler))
            {
                _culler.RecordReset(_cmd);
                _culler.RecordCull(_cmd, _blades, _config.BladeCount, new SpikeCullLayer(BladeLod0Region, BladeLod1Region,
                    _config.Lod1DistanceMeters, _config.MaxDistanceMeters, BladeBoundingRadiusMeters));
                if (_config.ObjectsEnabled)
                {
                    _culler.RecordCull(_cmd, _objects, _config.ObjectCount, new SpikeCullLayer(ObjectRegion, ObjectRegion,
                        float.MaxValue, _config.MaxDistanceMeters, ObjectBoundingRadiusMeters));
                }

                _culler.RecordFinalize(_cmd);
            }

            Graphics.ExecuteCommandBuffer(_cmd);
            if (_config.Backend == SpikeBackend.Indirect)
            {
                // BRG submits its draws itself in OnPerformCulling; the baseline queues them every frame.
                long submitTicks = Stopwatch.GetTimestamp();
                _indirect.Submit(_worldBounds);
                SubmitCpuMs = TicksToMs(Stopwatch.GetTimestamp() - submitTicks);
            }
            else
            {
                SubmitCpuMs = brgCallbackMs;
            }

            if (readStats && !_readbackPending)
            {
                _readbackPending = true;
                AsyncGPUReadback.RequestIntoNativeArray(ref _counterReadback, _culler.Counters, _onCountersRead);
            }

            TickCpuMs = TicksToMs(Stopwatch.GetTimestamp() - startTicks);
        }

        internal static float TicksToMs(long ticks)
        {
            return (float)(ticks * 1000.0 / Stopwatch.Frequency);
        }

        public void Dispose()
        {
            AsyncGPUReadback.WaitAllRequests();
            _brg.Dispose();
            _culler.Dispose();
            _blades.Dispose();
            _objects.Dispose();
            for (int lod = 0; lod < LodCount; lod++)
            {
                DestroyObject(_bladeMeshes[lod]);
                _multiBladeIndexBuffers[lod]?.Release();
            }

            for (int draw = 0; draw < DrawCount; draw++)
            {
                DestroyObject(_materials[draw]);
            }

            _cmd.Release();
            if (_counterReadback.IsCreated)
            {
                _counterReadback.Dispose();
            }

            if (_counters.IsCreated)
            {
                _counters.Dispose();
            }
        }

        private void CreateBladeDraws(ScatterSpikeResources resources, SpikeDrawType type)
        {
            Shader shader = type == SpikeDrawType.MeshIndirect ? resources.BladeMeshShader : resources.BladeProceduralShader;
            for (int lod = 0; lod < LodCount; lod++)
            {
                int region = lod == 0 ? BladeLod0Region : BladeLod1Region;
                int segmentCount = lod == 0 ? Lod0SegmentCount : Lod1SegmentCount;
                int k = type == SpikeDrawType.ProceduralIndexedIndirect ? _config.BladesPerInstance : 1;
                int elements = type == SpikeDrawType.ProceduralIndexedIndirect
                    ? BladeMeshFactory.GetIndexCount(segmentCount) * k
                    : BladeMeshFactory.GetIndexCount(segmentCount); // mesh: indices; non-indexed procedural: 3 vertices per triangle
                int draw = GetDrawIndex(type, lod);
                _drawDescriptors[draw] = new SpikeDrawDescriptor(region, type, elements, k);

                Material material = CreateMaterial(shader, draw, _blades, region);
                material.SetFloat(s_BladeWidth, _config.BladeWidthMeters);
                material.SetFloat(s_BladeHeight, _config.BladeHeightMeters);
                material.SetFloat(s_BladeBend, _config.BladeBend);
                material.SetFloat(s_SegmentCount, segmentCount);
                material.SetFloat(s_BladesPerInstance, k);
                material.SetFloat(s_Indexed, type == SpikeDrawType.ProceduralIndexedIndirect ? 1f : 0f);

                AddDraw(type, material, type == SpikeDrawType.MeshIndirect ? _bladeMeshes[lod] : null,
                    type == SpikeDrawType.ProceduralIndexedIndirect ? _multiBladeIndexBuffers[lod] : null, region, draw);
            }
        }

        private void CreateObjectDraw(ScatterSpikeResources resources)
        {
            Mesh mesh = resources.ObjectMesh;
            _drawDescriptors[ObjectDraw] = new SpikeDrawDescriptor(ObjectRegion, SpikeDrawType.MeshIndirect, (int)mesh.GetIndexCount(0), 1);
            Material material = CreateMaterial(resources.ObjectShader, ObjectDraw, _objects, ObjectRegion);
            AddDraw(SpikeDrawType.MeshIndirect, material, mesh, null, ObjectRegion, ObjectDraw);
        }

        /// <summary>Adds the same draw to both backends: draw indices match <see cref="GetDrawIndex"/> in both.</summary>
        private void AddDraw(SpikeDrawType type, Material material, Mesh mesh, GraphicsBuffer indexBuffer, int region, int draw)
        {
            _brg.AddDraw(new SpikeBrgDraw
            {
                Type = type,
                Material = _brg.RegisterMaterial(material),
                Mesh = mesh != null ? _brg.RegisterMesh(mesh) : default,
                IndexBuffer = indexBuffer != null ? indexBuffer.bufferHandle : default,
                Region = region,
                ArgsDraw = draw,
                CastShadows = true,
            });
            _indirect.AddDraw(new SpikeIndirectDraw
            {
                Type = type,
                Material = material,
                Mesh = mesh,
                IndexBuffer = indexBuffer,
                ArgsDraw = draw,
                CastShadows = true,
            });
        }

        private Material CreateMaterial(Shader shader, int draw, SpikeInstanceSet instances, int region)
        {
            var material = new Material(shader) { name = shader.name + " " + draw, hideFlags = HideFlags.DontSave };
            material.SetBuffer(s_Instances, instances.Buffer);
            material.SetBuffer(s_VisibleList, _culler.VisibleList);
            material.SetFloat(s_RegionStart, _culler.GetRegionStart(region));
            material.SetColor(s_DebugColor, s_DebugColors[draw]);
            _materials[draw] = material;
            return material;
        }

        private void ApplyDrawSelection()
        {
            bool brg = _config.Backend == SpikeBackend.Brg;
            for (int draw = 0; draw < DrawCount; draw++)
            {
                bool selected = _config.ScatterEnabled && (draw == ObjectDraw
                    ? _config.ObjectsEnabled
                    : _drawDescriptors[draw].DrawType == (uint)_config.BladeDrawType);
                _brg.SetDrawEnabled(draw, selected && brg);
                _indirect.SetDrawEnabled(draw, selected && !brg);
            }
        }

        private void OnCountersRead(AsyncGPUReadbackRequest request)
        {
            _readbackPending = false;
            if (!request.hasError && _counters.IsCreated && _counterReadback.IsCreated)
            {
                _counters.CopyFrom(_counterReadback);
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
