namespace Scatter.Rendering.Spike
{
    /// <summary>Runs of the Phase 1 benchmark (S3), chosen by the command line flag.</summary>
    public enum SpikeBenchmarkMode
    {
        /// <summary><c>-spikeBenchmark</c>: every backend x draw type x blade count, then the K sweep.</summary>
        Full = 0,

        /// <summary><c>-spikeBenchmarkQuick</c>: 1M blades, every backend x draw type, few frames (checks the tooling).</summary>
        Quick = 1,
    }
}
