using NUnit.Framework;
using Scatter.Rendering.Spike;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Scatter.Tests.Rendering.Spike
{
    /// <summary>The body of the BRG culling callback: which commands are written for each view.</summary>
    public sealed unsafe class SpikeBrgRendererTests
    {
        private const string ComputePath = "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeCull.compute";
        private const string ShaderPath = "Packages/com.gaiagames.scatter/Shaders/Spike/SpikeBladeProcedural.shader";

        private SpikeCuller _culler;
        private SpikeBrgRenderer _renderer;
        private Material _material;
        private Mesh _mesh;
        private GraphicsBuffer _indexBuffer;
        private NativeArray<BatchCullingOutputDrawCommands> _output;

        [SetUp]
        public void SetUp()
        {
            _culler = new SpikeCuller(AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath), new[] { 128, 128 }, 4);
            _renderer = new SpikeBrgRenderer(_culler, new Bounds(Vector3.zero, Vector3.one * 100f), 4);
            _material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath));
            _mesh = Scatter.Rendering.BladeMeshFactory.CreateBladeMesh(3, "Test Blade");
            _indexBuffer = Scatter.Rendering.BladeMeshFactory.CreateMultiBladeIndexBuffer(3, 4, "Test Index Buffer");
            BatchMaterialID material = _renderer.RegisterMaterial(_material);
            BatchMeshID mesh = _renderer.RegisterMesh(_mesh);

            // Draw 0: mesh, region 0, casts. Draw 1: mesh, region 1, no shadow.
            // Draw 2: procedural, region 1, casts. Draw 3: procedural indexed, region 0, disabled.
            _renderer.AddDraw(new SpikeBrgDraw { Type = SpikeDrawType.MeshIndirect, Material = material, Mesh = mesh, Region = 0, ArgsDraw = 0, CastShadows = true, Enabled = true });
            _renderer.AddDraw(new SpikeBrgDraw { Type = SpikeDrawType.MeshIndirect, Material = material, Mesh = mesh, Region = 1, ArgsDraw = 1, CastShadows = false, Enabled = true });
            _renderer.AddDraw(new SpikeBrgDraw { Type = SpikeDrawType.ProceduralIndirect, Material = material, Region = 1, ArgsDraw = 2, CastShadows = true, Enabled = true });
            _renderer.AddDraw(new SpikeBrgDraw
            {
                Type = SpikeDrawType.ProceduralIndexedIndirect, Material = material, IndexBuffer = _indexBuffer.bufferHandle, Region = 0, ArgsDraw = 3, CastShadows = true, Enabled = false,
            });
            _output = new NativeArray<BatchCullingOutputDrawCommands>(1, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            SpikeBrgRenderer.FreeDrawCommands(_output);
            _output.Dispose();
            _renderer.Dispose();
            _culler.Dispose();
            _indexBuffer.Release();
            Object.DestroyImmediate(_material);
            Object.DestroyImmediate(_mesh);
        }

        [Test]
        public void CameraView_WritesEveryEnabledDraw_WithItsArgsAndRegion()
        {
            _renderer.WriteDrawCommands(BatchCullingViewType.Camera, 1, _output);

            BatchCullingOutputDrawCommands commands = _output[0];
            Assert.That(commands.indirectDrawCommandCount, Is.EqualTo(2));
            Assert.That(commands.proceduralIndirectDrawCommandCount, Is.EqualTo(1));
            Assert.That(commands.drawRangeCount, Is.EqualTo(2));
            Assert.That(commands.drawRanges[0].drawCommandsType, Is.EqualTo(BatchDrawCommandType.Indirect));
            Assert.That(commands.drawRanges[0].drawCommandsCount, Is.EqualTo(2u));
            Assert.That(commands.drawRanges[1].drawCommandsType, Is.EqualTo(BatchDrawCommandType.ProceduralIndirect));

            BatchDrawCommandIndirect second = commands.indirectDrawCommands[1];
            Assert.That(second.visibleOffset, Is.EqualTo((uint)_culler.GetRegionStart(1)));
            Assert.That(second.indirectArgsBufferOffset, Is.EqualTo((uint)SpikeCuller.GetArgsOffsetBytes(1)));
            Assert.That(second.splitVisibilityMask, Is.EqualTo((ushort)1));
            Assert.That(commands.proceduralIndirectDrawCommands[0].indirectArgsBufferOffset, Is.EqualTo((uint)SpikeCuller.GetArgsOffsetBytes(2)));
        }

        [Test]
        public void ShadowView_SkipsNonCasters_AndCoversEverySplit()
        {
            _renderer.WriteDrawCommands(BatchCullingViewType.Light, 4, _output);

            BatchCullingOutputDrawCommands commands = _output[0];
            Assert.That(commands.indirectDrawCommandCount, Is.EqualTo(1));
            Assert.That(commands.proceduralIndirectDrawCommandCount, Is.EqualTo(1));
            Assert.That(commands.indirectDrawCommands[0].splitVisibilityMask, Is.EqualTo((ushort)0b1111));
        }

        [Test]
        public void OtherViews_GetNoCommand()
        {
            _renderer.WriteDrawCommands(BatchCullingViewType.Picking, 1, _output);

            Assert.That(_output[0].indirectDrawCommandCount + _output[0].proceduralIndirectDrawCommandCount, Is.EqualTo(0));
        }

        [TestCase(SpikeRegionOffsetMode.VisibleOffset, 128u, 0u, 0u)]
        [TestCase(SpikeRegionOffsetMode.WindowOffsetBytes, 0u, 512u, 512u)]
        [TestCase(SpikeRegionOffsetMode.WindowOffsetEntries, 0u, 128u, 512u)]
        public void OffsetMode_PointsAtTheRegion(SpikeRegionOffsetMode mode, uint visibleOffset, uint windowOffset, uint windowSizeBytes)
        {
            _renderer.OffsetMode = mode;
            _renderer.WriteDrawCommands(BatchCullingViewType.Camera, 1, _output);

            BatchDrawCommandIndirect regionOne = _output[0].indirectDrawCommands[1];
            Assert.That(regionOne.visibleOffset, Is.EqualTo(visibleOffset));
            Assert.That(regionOne.visibleInstancesBufferWindowOffset, Is.EqualTo(windowOffset));
            Assert.That(regionOne.visibleInstancesBufferWindowSizeBytes, Is.EqualTo(windowSizeBytes));
        }

        [Test]
        public void IndexedDraw_UsesItsIndexBuffer()
        {
            _renderer.SetDrawEnabled(3, true);
            _renderer.WriteDrawCommands(BatchCullingViewType.Camera, 1, _output);

            BatchCullingOutputDrawCommands commands = _output[0];
            Assert.That(commands.proceduralIndirectDrawCommandCount, Is.EqualTo(2));
            Assert.That(commands.proceduralIndirectDrawCommands[1].indexBufferHandle, Is.EqualTo(_indexBuffer.bufferHandle));
        }

        [Test]
        public void WriteDrawCommands_DoesNotAllocate()
        {
            _renderer.WriteDrawCommands(BatchCullingViewType.Camera, 1, _output);
            SpikeBrgRenderer.FreeDrawCommands(_output);

            Assert.That(() =>
            {
                _renderer.WriteDrawCommands(BatchCullingViewType.Light, 4, _output);
                SpikeBrgRenderer.FreeDrawCommands(_output);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
