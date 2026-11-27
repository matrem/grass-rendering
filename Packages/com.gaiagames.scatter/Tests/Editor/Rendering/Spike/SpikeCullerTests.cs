using System.Collections.Generic;
using NUnit.Framework;
using Scatter.Rendering.Spike;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Scatter.Tests.Rendering.Spike
{
    /// <summary>
    /// GPU tests of SpikeCull.compute: dispatch on small inputs, read back, compare with a C# reference.
    /// Need a graphics device (never run with -nographics).
    /// </summary>
    public sealed class SpikeCullerTests
    {
        private const string ComputePath = "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeCull.compute";
        private const int Lod0Region = 0;
        private const int Lod1Region = 1;
        private const float Lod1DistanceMeters = 15f;
        private const float MaxDistanceMeters = 40f;
        private const float RadiusMeters = 0.5f;

        // Instances closer than this to a cull boundary may fall on either side on the GPU: not compared.
        private const float BoundaryToleranceMeters = 1e-3f;
        private const uint UntouchedEntry = 0xDEADBEEF;

        private ComputeShader _compute;
        private CommandBuffer _cmd;
        private readonly List<System.IDisposable> _disposables = new List<System.IDisposable>();

        [SetUp]
        public void SetUp()
        {
            _compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            Assert.That(_compute, Is.Not.Null, ComputePath);
            _cmd = new CommandBuffer { name = "SpikeCullerTests" };
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _disposables.Count; i++)
            {
                _disposables[i].Dispose();
            }

            _disposables.Clear();
            _cmd.Release();
        }

        [TestCase("PlaceSpikeInstances")]
        [TestCase("ResetSpikeCounters")]
        [TestCase("CullSpikeInstances")]
        [TestCase("FinalizeSpikeArgs")]
        public void ThreadGroupSize_MatchesCSharp(string kernelName)
        {
            _compute.GetKernelThreadGroupSizes(_compute.FindKernel(kernelName), out uint x, out uint y, out uint z);

            Assert.That(x, Is.EqualTo((uint)SpikeCuller.ThreadGroupSize));
            Assert.That(y, Is.EqualTo(1u));
            Assert.That(z, Is.EqualTo(1u));
        }

        [Test]
        public void DrawDescriptor_HasTheHlslSize()
        {
            Assert.That(UnsafeUtility.SizeOf<SpikeDrawDescriptor>(), Is.EqualTo(SpikeDrawDescriptor.SizeBytes));
        }

        [Test]
        public void RegionCapacities_AreRoundedToTheLargestK()
        {
            SpikeCuller culler = CreateCuller(new[] { 100, 1 }, 1);

            Assert.That(culler.GetRegionCapacity(0), Is.EqualTo(128));
            Assert.That(culler.GetRegionCapacity(1), Is.EqualTo(SpikeCuller.MaxBladesPerInstance));
            Assert.That(culler.GetRegionStart(1), Is.EqualTo(128));
        }

        [Test]
        public void Cull_MatchesReference_PerRegion()
        {
            Vector4[] instances = CreateGrid(41, 1f);
            SpikeCuller culler = CreateCuller(new[] { instances.Length, instances.Length }, 2);
            culler.SetDraws(new[]
            {
                new SpikeDrawDescriptor(Lod0Region, SpikeDrawType.MeshIndirect, 15, 1),
                new SpikeDrawDescriptor(Lod1Region, SpikeDrawType.MeshIndirect, 9, 1),
            }, 2);
            SpikeInstanceSet set = CreateInstanceSet(instances);
            Vector3 cameraPosition = new Vector3(0f, 1.5f, -25f);
            Plane[] planes = CreateFrustumPlanes(cameraPosition);

            RunCull(culler, set, instances.Length, cameraPosition, planes);

            uint[] counters = Read(culler.Counters, culler.RegionCount + 1);
            uint[] visible = Read(culler.VisibleList, culler.VisibleEntryCount);
            uint[] args = Read(culler.Args, 2 * SpikeCuller.ArgsUIntsPerDraw);
            Reference(instances, cameraPosition, planes, out HashSet<uint> lod0, out HashSet<uint> lod1, out HashSet<uint> ambiguous);

            Assert.That(lod0.Count, Is.GreaterThan(0), "Test setup: no instance in LOD0.");
            Assert.That(lod1.Count, Is.GreaterThan(0), "Test setup: no instance in LOD1.");
            AssertRegion(visible, culler.GetRegionStart(Lod0Region), counters[Lod0Region], lod0, ambiguous, "LOD0");
            AssertRegion(visible, culler.GetRegionStart(Lod1Region), counters[Lod1Region], lod1, ambiguous, "LOD1");
            Assert.That(counters[culler.OverflowCounterIndex], Is.EqualTo(0u));
            Assert.That(args, Is.EqualTo(new uint[] { 15, counters[Lod0Region], 0, 0, 0, 9, counters[Lod1Region], 0, 0, 0 }));
        }

        [Test]
        public void Reset_ClearsCountersEveryFrame()
        {
            Vector4[] instances = CreateGrid(21, 1f);
            SpikeCuller culler = CreateCuller(new[] { instances.Length, instances.Length }, 1);
            SpikeInstanceSet set = CreateInstanceSet(instances);
            Vector3 cameraPosition = new Vector3(0f, 1.5f, -25f);
            Plane[] planes = CreateFrustumPlanes(cameraPosition);

            RunCull(culler, set, instances.Length, cameraPosition, planes);
            uint[] first = Read(culler.Counters, culler.RegionCount + 1);
            RunCull(culler, set, instances.Length, cameraPosition, planes);
            uint[] second = Read(culler.Counters, culler.RegionCount + 1);

            Assert.That(first[Lod0Region] + first[Lod1Region], Is.GreaterThan(0u));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Overflow_CountsSkippedEntries_AndNeverWritesPastTheRegion()
        {
            // 400 instances in front of the camera, all in LOD0, for a LOD0 region of 64 entries.
            var instances = new Vector4[400];
            for (int i = 0; i < instances.Length; i++)
            {
                instances[i] = new Vector4((i % 20) * 0.2f - 2f, 0f, (i / 20) * 0.2f + 2f, 0f);
            }

            SpikeCuller culler = CreateCuller(new[] { 1, 1 }, 1);
            culler.SetDraws(new[] { new SpikeDrawDescriptor(Lod0Region, SpikeDrawType.MeshIndirect, 15, 1) }, 1);
            SpikeInstanceSet set = CreateInstanceSet(instances);
            Fill(culler.VisibleList, culler.VisibleEntryCount, UntouchedEntry);
            Vector3 cameraPosition = new Vector3(0f, 1.5f, -2f);

            RunCull(culler, set, instances.Length, cameraPosition, CreateFrustumPlanes(cameraPosition));

            uint[] counters = Read(culler.Counters, culler.RegionCount + 1);
            uint[] visible = Read(culler.VisibleList, culler.VisibleEntryCount);
            uint[] args = Read(culler.Args, SpikeCuller.ArgsUIntsPerDraw);
            int capacity = culler.GetRegionCapacity(Lod0Region);
            Assert.That(counters[Lod0Region], Is.EqualTo((uint)instances.Length));
            Assert.That(counters[culler.OverflowCounterIndex], Is.EqualTo((uint)(instances.Length - capacity)));
            Assert.That(args[1], Is.EqualTo((uint)capacity), "Instance count clamped to the region capacity.");
            for (int entry = culler.GetRegionStart(Lod1Region); entry < culler.VisibleEntryCount; entry++)
            {
                Assert.That(visible[entry], Is.EqualTo(UntouchedEntry), $"Entry {entry} of the next region was written.");
            }
        }

        [TestCase(1, 37u)]
        [TestCase(16, 3u)]
        [TestCase(64, 1u)]
        public void Finalize_KBladeDraws_RoundUpInstancesAndPadWithEmptyEntries(int bladesPerInstance, uint expectedInstances)
        {
            // 37 instances visible in LOD0.
            var instances = new Vector4[37];
            for (int i = 0; i < instances.Length; i++)
            {
                instances[i] = new Vector4(i * 0.1f - 1.8f, 0f, 3f, 0f);
            }

            SpikeCuller culler = CreateCuller(new[] { 256, 1 }, 1);
            culler.SetDraws(new[] { new SpikeDrawDescriptor(Lod0Region, SpikeDrawType.ProceduralIndexedIndirect, 15 * bladesPerInstance, bladesPerInstance) }, 1);
            SpikeInstanceSet set = CreateInstanceSet(instances);
            Fill(culler.VisibleList, culler.VisibleEntryCount, UntouchedEntry);
            Vector3 cameraPosition = new Vector3(0f, 1.5f, 0f);

            RunCull(culler, set, instances.Length, cameraPosition, CreateFrustumPlanes(cameraPosition));

            uint[] counters = Read(culler.Counters, culler.RegionCount + 1);
            uint[] visible = Read(culler.VisibleList, culler.VisibleEntryCount);
            uint[] args = Read(culler.Args, SpikeCuller.ArgsUIntsPerDraw);
            Assert.That(counters[Lod0Region], Is.EqualTo(37u), "Test setup: all instances visible.");
            Assert.That(args, Is.EqualTo(new uint[] { (uint)(15 * bladesPerInstance), expectedInstances, 0, 0, 0 }));
            uint padded = expectedInstances * (uint)bladesPerInstance;
            for (uint entry = 37; entry < padded; entry++)
            {
                Assert.That(visible[entry], Is.EqualTo(SpikeCuller.EmptyEntry), $"Padding entry {entry}.");
            }

            Assert.That(visible[padded], Is.EqualTo(UntouchedEntry), "Padding stops at the last instance.");
        }

        [Test]
        public void Placement_IsDeterministic_InsideTheArea_AndEvenForAnyPrefix()
        {
            const int count = 4096;
            var area = new Rect(-10f, 20f, 40f, 40f);
            SpikeCuller culler = CreateCuller(new[] { 1 }, 1);
            var first = new SpikeInstanceSet("Placement A", count);
            var second = new SpikeInstanceSet("Placement B", count);
            _disposables.Add(first);
            _disposables.Add(second);

            _cmd.Clear();
            culler.RecordPlacement(_cmd, first, count, area, 1234u);
            culler.RecordPlacement(_cmd, second, count, area, 1234u);
            Graphics.ExecuteCommandBuffer(_cmd);
            var a = new Vector4[count];
            var b = new Vector4[count];
            first.Buffer.GetData(a);
            second.Buffer.GetData(b);

            Assert.That(b, Is.EqualTo(a), "Same seed, same bits.");
            const int cellsPerSide = 4;
            const int prefix = 1024;
            var cells = new int[cellsPerSide * cellsPerSide];
            for (int i = 0; i < count; i++)
            {
                Assert.That(a[i].x, Is.InRange(area.xMin, area.xMax));
                Assert.That(a[i].z, Is.InRange(area.yMin, area.yMax));
                Assert.That(a[i].w, Is.InRange(0f, 2f * Mathf.PI));
                if (i < prefix)
                {
                    int cellX = Mathf.Min((int)((a[i].x - area.xMin) / area.width * cellsPerSide), cellsPerSide - 1);
                    int cellZ = Mathf.Min((int)((a[i].z - area.yMin) / area.height * cellsPerSide), cellsPerSide - 1);
                    cells[cellZ * cellsPerSide + cellX]++;
                }
            }

            const int expectedPerCell = prefix / (cellsPerSide * cellsPerSide);
            for (int cell = 0; cell < cells.Length; cell++)
            {
                Assert.That(cells[cell], Is.InRange(expectedPerCell * 3 / 4, expectedPerCell * 5 / 4), $"Cell {cell} of the first {prefix} instances.");
            }
        }

        [Test]
        public void RecordFrame_DoesNotAllocate()
        {
            Vector4[] instances = CreateGrid(11, 1f);
            SpikeCuller culler = CreateCuller(new[] { instances.Length, instances.Length }, 1);
            culler.SetDraws(new[] { new SpikeDrawDescriptor(Lod0Region, SpikeDrawType.MeshIndirect, 15, 1) }, 1);
            SpikeInstanceSet set = CreateInstanceSet(instances);
            Vector3 cameraPosition = new Vector3(0f, 1.5f, -25f);
            Plane[] planes = CreateFrustumPlanes(cameraPosition);
            var layer = new SpikeCullLayer(Lod0Region, Lod1Region, Lod1DistanceMeters, MaxDistanceMeters, RadiusMeters);
            RecordFrame(culler, set, instances.Length, cameraPosition, planes, layer);

            Assert.That(() => RecordFrame(culler, set, instances.Length, cameraPosition, planes, layer), Is.Not.AllocatingGCMemory());
        }

        private void RecordFrame(SpikeCuller culler, SpikeInstanceSet set, int count, Vector3 cameraPosition, Plane[] planes, in SpikeCullLayer layer)
        {
            _cmd.Clear();
            culler.SetView(cameraPosition, planes);
            culler.RecordReset(_cmd);
            culler.RecordCull(_cmd, set, count, layer);
            culler.RecordFinalize(_cmd);
        }

        private void RunCull(SpikeCuller culler, SpikeInstanceSet set, int count, Vector3 cameraPosition, Plane[] planes)
        {
            RecordFrame(culler, set, count, cameraPosition, planes, new SpikeCullLayer(Lod0Region, Lod1Region, Lod1DistanceMeters, MaxDistanceMeters, RadiusMeters));
            Graphics.ExecuteCommandBuffer(_cmd);
        }

        private SpikeCuller CreateCuller(int[] capacities, int maxDraws)
        {
            var culler = new SpikeCuller(_compute, capacities, maxDraws);
            _disposables.Add(culler);
            return culler;
        }

        private SpikeInstanceSet CreateInstanceSet(Vector4[] instances)
        {
            var set = new SpikeInstanceSet("SpikeCullerTests Instances", instances.Length);
            set.Buffer.SetData(instances);
            _disposables.Add(set);
            return set;
        }

        /// <summary>Square grid of side x side instances centred on the origin, yaw 0.</summary>
        private static Vector4[] CreateGrid(int side, float spacingMeters)
        {
            var instances = new Vector4[side * side];
            float offset = (side - 1) * spacingMeters * 0.5f;
            for (int i = 0; i < instances.Length; i++)
            {
                instances[i] = new Vector4((i % side) * spacingMeters - offset, 0f, (i / side) * spacingMeters - offset, 0f);
            }

            return instances;
        }

        /// <summary>Planes of a camera at <paramref name="position"/> looking along +Z: 60 degrees, square, 0.1 to 100 m.</summary>
        private static Plane[] CreateFrustumPlanes(Vector3 position)
        {
            Matrix4x4 view = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(1f, 1f, -1f)).inverse;
            Matrix4x4 projection = Matrix4x4.Perspective(60f, 1f, 0.1f, 100f);
            var planes = new Plane[SpikeCuller.FrustumPlaneCount];
            GeometryUtility.CalculateFrustumPlanes(projection * view, planes);
            return planes;
        }

        private static void Reference(Vector4[] instances, Vector3 cameraPosition, Plane[] planes, out HashSet<uint> lod0, out HashSet<uint> lod1, out HashSet<uint> ambiguous)
        {
            lod0 = new HashSet<uint>();
            lod1 = new HashSet<uint>();
            ambiguous = new HashSet<uint>();
            for (uint i = 0; i < instances.Length; i++)
            {
                var position = (Vector3)instances[i];
                float distance = Vector3.Distance(position, cameraPosition);
                bool nearBoundary = Mathf.Abs(distance - MaxDistanceMeters) < BoundaryToleranceMeters
                    || Mathf.Abs(distance - Lod1DistanceMeters) < BoundaryToleranceMeters;
                bool inside = distance < MaxDistanceMeters;
                for (int p = 0; p < planes.Length; p++)
                {
                    float planeDistance = planes[p].GetDistanceToPoint(position) + RadiusMeters;
                    nearBoundary |= Mathf.Abs(planeDistance) < BoundaryToleranceMeters;
                    inside &= planeDistance >= 0f;
                }

                if (nearBoundary)
                {
                    ambiguous.Add(i);
                }
                else if (inside)
                {
                    (distance < Lod1DistanceMeters ? lod0 : lod1).Add(i);
                }
            }
        }

        private static void AssertRegion(uint[] visible, int start, uint count, HashSet<uint> expected, HashSet<uint> ambiguous, string label)
        {
            var actual = new HashSet<uint>();
            for (int entry = 0; entry < count; entry++)
            {
                uint index = visible[start + entry];
                Assert.That(actual.Add(index), Is.True, $"{label}: instance {index} written twice.");
                if (!ambiguous.Contains(index))
                {
                    Assert.That(expected.Contains(index), Is.True, $"{label}: instance {index} should not be visible here.");
                }
            }

            foreach (uint index in expected)
            {
                Assert.That(actual.Contains(index), Is.True, $"{label}: visible instance {index} is missing.");
            }
        }

        private static uint[] Read(GraphicsBuffer buffer, int count)
        {
            var data = new uint[count];
            buffer.GetData(data);
            return data;
        }

        private static void Fill(GraphicsBuffer buffer, int count, uint value)
        {
            var data = new uint[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = value;
            }

            buffer.SetData(data);
        }
    }
}
