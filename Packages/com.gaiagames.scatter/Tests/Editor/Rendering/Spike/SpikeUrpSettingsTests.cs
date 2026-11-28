using NUnit.Framework;
using Scatter.Rendering.Spike;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Scatter.Tests.Rendering.Spike
{
    /// <summary>On a temporary URP asset, never the project asset.</summary>
    public sealed class SpikeUrpSettingsTests
    {
        private UniversalRenderPipelineAsset _asset;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            _asset.renderScale = 0.8f;
            _asset.mainLightShadowmapResolution = 1024;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_asset);
        }

        [Test]
        public void Apply_SetsBothValues_AndProjectSettingKeepsTheOriginal()
        {
            var settings = new SpikeUrpSettings(_asset);

            settings.Apply(0.5f, 512);
            Assert.That(_asset.renderScale, Is.EqualTo(0.5f));
            Assert.That(_asset.mainLightShadowmapResolution, Is.EqualTo(512));
            Assert.That(settings.RenderScale, Is.EqualTo(0.5f));
            Assert.That(settings.ShadowResolution, Is.EqualTo(512));

            settings.Apply(SpikeUrpSettings.ProjectSetting, 2048);
            Assert.That(_asset.renderScale, Is.EqualTo(0.8f));
            Assert.That(_asset.mainLightShadowmapResolution, Is.EqualTo(2048));
        }

        [Test]
        public void Restore_PutsTheOriginalValuesBack()
        {
            var settings = new SpikeUrpSettings(_asset);
            settings.Apply(0.25f, 256);

            settings.Restore();

            Assert.That(_asset.renderScale, Is.EqualTo(0.8f));
            Assert.That(_asset.mainLightShadowmapResolution, Is.EqualTo(1024));
        }

        [Test]
        public void WithoutAsset_ChangesNothing()
        {
            var settings = new SpikeUrpSettings(null);
            settings.Apply(0.5f, 512);
            settings.Restore();

            Assert.That(settings.IsAvailable, Is.False);
            Assert.That(float.IsNaN(settings.RenderScale), Is.True);
            Assert.That(settings.ShadowResolution, Is.EqualTo(0));
        }

        [Test]
        public void Apply_DoesNotAllocate()
        {
            var settings = new SpikeUrpSettings(_asset);
            settings.Apply(0.5f, 512);

            Assert.That(() => settings.Apply(0.25f, 2048), Is.Not.AllocatingGCMemory());
        }
    }
}
