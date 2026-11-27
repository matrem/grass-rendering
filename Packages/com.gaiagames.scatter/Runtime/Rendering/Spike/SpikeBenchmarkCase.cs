using System.Globalization;

namespace Scatter.Rendering.Spike
{
    /// <summary>One configuration measured by the Phase 1 benchmark (S3).</summary>
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

        /// <summary>Short name for logs and the CSV. Allocates: startup and end of the run only.</summary>
        public override string ToString()
        {
            if (!ScatterEnabled)
            {
                return "view" + View.ToString(CultureInfo.InvariantCulture) + " off";
            }

            return "view" + View.ToString(CultureInfo.InvariantCulture) + " " + Backend + " " + DrawType
                + " " + BladeCount.ToString(CultureInfo.InvariantCulture) + " K" + BladesPerInstance.ToString(CultureInfo.InvariantCulture);
        }
    }
}
