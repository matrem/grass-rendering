using System.Globalization;

namespace Scatter.Rendering.Spike
{
    /// <summary>One configuration measured by the spike benchmark (Phase 1 S3, Phase 1b).</summary>
    public struct SpikeBenchmarkCase
    {
        /// <summary>Index of the camera view.</summary>
        public int View;

        /// <summary>False for the baseline: no cull, no draw.</summary>
        public bool ScatterEnabled;

        public SpikeBackend Backend;
        public SpikeDrawType DrawType;
        public int BladeCount;

        /// <summary>K of <see cref="SpikeDrawType.ProceduralIndexedIndirect"/>; 1 for the other draw types.</summary>
        public int BladesPerInstance;

        /// <summary>URP render scale; <see cref="SpikeUrpSettings.ProjectSetting"/> (0) keeps the project value.</summary>
        public float RenderScale;

        /// <summary>Main light shadow map resolution; <see cref="SpikeUrpSettings.ProjectSetting"/> (0) keeps the project value.</summary>
        public int ShadowResolution;

        public SpikeDensityFalloff DensityFalloff;
        public SpikeShadowCasters ShadowCasters;

        /// <summary>Short name for logs and the CSV. Allocates: startup and end of the run only.</summary>
        public override string ToString()
        {
            string name = "view" + View.ToString(CultureInfo.InvariantCulture);
            name += ScatterEnabled
                ? " " + Backend + " " + DrawType + " " + BladeCount.ToString(CultureInfo.InvariantCulture) + " K" + BladesPerInstance.ToString(CultureInfo.InvariantCulture)
                : " off";
            if (RenderScale > 0f)
            {
                name += " scale" + RenderScale.ToString("0.##", CultureInfo.InvariantCulture);
            }

            if (ShadowResolution > 0)
            {
                name += " shadow" + ShadowResolution.ToString(CultureInfo.InvariantCulture);
            }

            if (ScatterEnabled && DensityFalloff != SpikeDensityFalloff.Off)
            {
                name += " falloff" + DensityFalloff;
            }

            if (ScatterEnabled && ShadowCasters != SpikeShadowCasters.AllLodsAllCascades)
            {
                name += " casters" + ShadowCasters;
            }

            return name;
        }
    }
}
