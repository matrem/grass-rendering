using UnityEngine;

namespace Scatter.Rendering.Spike
{
    /// <summary>Which shadow splits (cascades) a blade draw of a given LOD casts into, for a <see cref="SpikeShadowCasters"/> preset.</summary>
    public static class SpikeShadowCasterMask
    {
        public const int PresetCount = 5;

        /// <summary>
        /// BRG split visibility mask of a blade draw of <paramref name="lod"/> in a shadow view of
        /// <paramref name="splitCount"/> splits; 0 when the draw casts nothing.
        /// </summary>
        public static ushort GetSplitMask(SpikeShadowCasters casters, int lod, int splitCount)
        {
            int splits = Mathf.Max(splitCount, 1);
            switch (casters)
            {
                case SpikeShadowCasters.AllLodsAllCascades:
                    return FirstSplits(splits);
                case SpikeShadowCasters.Lod0AllCascades:
                    return lod == 0 ? FirstSplits(splits) : (ushort)0;
                case SpikeShadowCasters.Lod0FirstTwoCascades:
                    return lod == 0 ? FirstSplits(Mathf.Min(2, splits)) : (ushort)0;
                case SpikeShadowCasters.Lod0FirstCascade:
                    return lod == 0 ? FirstSplits(1) : (ushort)0;
                default:
                    return 0;
            }
        }

        /// <summary>Whether a blade draw of <paramref name="lod"/> casts at all (Graphics.Render*Indirect: every cascade or none).</summary>
        public static bool Casts(SpikeShadowCasters casters, int lod)
        {
            return GetSplitMask(casters, lod, 1) != 0;
        }

        /// <summary>Mask of the first <paramref name="count"/> splits.</summary>
        public static ushort FirstSplits(int count)
        {
            return (ushort)((1 << count) - 1);
        }
    }
}
