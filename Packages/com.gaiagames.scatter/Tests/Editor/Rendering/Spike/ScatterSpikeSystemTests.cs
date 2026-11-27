using NUnit.Framework;
using Scatter.Rendering.Spike;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Scatter.Tests.Rendering.Spike
{
    public sealed class ScatterSpikeSystemTests
    {
        private ScatterSpikeSystem _system;
        private GameObject _cameraObject;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            ScatterSpikeResources resources = ScatterSpikeResources.Load();
            Assert.That(resources, Is.Not.Null, "Resources asset missing: run Scatter Prototype > Create Spike Content.");
            ScatterSpikeConfig config = ScatterSpikeConfig.Default;
            config.MaxBladeCount = 1 << 16;
            config.BladeCount = 1 << 16;
            config.ObjectCount = 1 << 10;
            _system = new ScatterSpikeSystem(resources, config);

            _cameraObject = new GameObject("ScatterSpikeSystemTests Camera");
            _cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.5f, -10f), Quaternion.Euler(10f, 0f, 0f));
            _camera = _cameraObject.AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            _system.Dispose();
            Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void Tick_FillsBothBladeLodRegionsAndTheObjectRegion()
        {
            _system.Tick(_camera, false);

            var counters = new uint[_system.Culler.RegionCount + 1];
            _system.Culler.Counters.GetData(counters);
            Assert.That(counters[ScatterSpikeSystem.BladeLod0Region], Is.GreaterThan(0u));
            Assert.That(counters[ScatterSpikeSystem.BladeLod1Region], Is.GreaterThan(0u));
            Assert.That(counters[ScatterSpikeSystem.ObjectRegion], Is.GreaterThan(0u));
            Assert.That(counters[_system.Culler.OverflowCounterIndex], Is.EqualTo(0u));
        }

        [Test]
        public void Tick_DoesNotAllocate()
        {
            _system.Tick(_camera, false);
            _system.Tick(_camera, false);

            Assert.That(() => _system.Tick(_camera, false), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Tick_WithStatsReadback_DoesNotAllocate()
        {
            _system.Tick(_camera, true);
            UnityEngine.Rendering.AsyncGPUReadback.WaitAllRequests();
            _system.Tick(_camera, true);
            UnityEngine.Rendering.AsyncGPUReadback.WaitAllRequests();

            Assert.That(() => _system.Tick(_camera, true), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Tick_IndirectBackend_DoesNotAllocate()
        {
            _system.SetBackend(SpikeBackend.Indirect);
            _system.Tick(_camera, false);
            _system.Tick(_camera, false);

            Assert.That(() => _system.Tick(_camera, false), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void SettingsChanges_SelectTheDrawsOfOneBladeDrawType()
        {
            _system.SetBladeDrawType(SpikeDrawType.ProceduralIndexedIndirect);
            _system.SetBladesPerInstance(32);
            _system.SetObjectsEnabled(false);

            Assert.That(_system.Config.BladeDrawType, Is.EqualTo(SpikeDrawType.ProceduralIndexedIndirect));
            Assert.That(_system.Config.BladesPerInstance, Is.EqualTo(32));
            Assert.That(_system.Config.ObjectsEnabled, Is.False);
            Assert.That(_system.GetDrawIndex(SpikeDrawType.ProceduralIndexedIndirect, 1), Is.EqualTo(5));
        }
    }
}
