using NUnit.Framework;
using Scatter.Rendering.Spike;

namespace Scatter.Tests.Rendering.Spike
{
    public sealed class SpikeShadowCasterMaskTests
    {
        [TestCase(SpikeShadowCasters.AllLodsAllCascades, 0b1111, 0b1111)]
        [TestCase(SpikeShadowCasters.Lod0AllCascades, 0b1111, 0)]
        [TestCase(SpikeShadowCasters.Lod0FirstTwoCascades, 0b0011, 0)]
        [TestCase(SpikeShadowCasters.Lod0FirstCascade, 0b0001, 0)]
        [TestCase(SpikeShadowCasters.None, 0, 0)]
        public void FourCascades_MaskPerLod(SpikeShadowCasters casters, int lod0Mask, int lod1Mask)
        {
            Assert.That(SpikeShadowCasterMask.GetSplitMask(casters, 0, 4), Is.EqualTo((ushort)lod0Mask));
            Assert.That(SpikeShadowCasterMask.GetSplitMask(casters, 1, 4), Is.EqualTo((ushort)lod1Mask));
        }

        [TestCase(SpikeShadowCasters.AllLodsAllCascades, 0b1)]
        [TestCase(SpikeShadowCasters.Lod0FirstTwoCascades, 0b1)]
        [TestCase(SpikeShadowCasters.Lod0FirstCascade, 0b1)]
        public void OneCascade_NeverPastTheLastSplit(SpikeShadowCasters casters, int lod0Mask)
        {
            Assert.That(SpikeShadowCasterMask.GetSplitMask(casters, 0, 1), Is.EqualTo((ushort)lod0Mask));
            Assert.That(SpikeShadowCasterMask.GetSplitMask(casters, 0, 0), Is.EqualTo((ushort)lod0Mask), "0 splits counts as 1.");
        }

        [TestCase(SpikeShadowCasters.AllLodsAllCascades, true, true)]
        [TestCase(SpikeShadowCasters.Lod0FirstCascade, true, false)]
        [TestCase(SpikeShadowCasters.None, false, false)]
        public void Casts_PerLod(SpikeShadowCasters casters, bool lod0, bool lod1)
        {
            Assert.That(SpikeShadowCasterMask.Casts(casters, 0), Is.EqualTo(lod0));
            Assert.That(SpikeShadowCasterMask.Casts(casters, 1), Is.EqualTo(lod1));
        }
    }
}
