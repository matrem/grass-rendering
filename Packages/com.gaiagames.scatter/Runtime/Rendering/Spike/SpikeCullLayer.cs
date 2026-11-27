namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Cull settings of one spike layer: which regions receive its LOD0 and LOD1 instances, and its distances.
    /// A single-LOD layer uses the same region for both.
    /// </summary>
    public struct SpikeCullLayer
    {
        public int Lod0Region;
        public int Lod1Region;
        public float Lod1DistanceMeters;
        public float MaxDistanceMeters;

        /// <summary>Radius of the sphere tested against the frustum around each instance position.</summary>
        public float BoundingRadiusMeters;

        public SpikeCullLayer(int lod0Region, int lod1Region, float lod1DistanceMeters, float maxDistanceMeters, float boundingRadiusMeters)
        {
            Lod0Region = lod0Region;
            Lod1Region = lod1Region;
            Lod1DistanceMeters = lod1DistanceMeters;
            MaxDistanceMeters = maxDistanceMeters;
            BoundingRadiusMeters = boundingRadiusMeters;
        }
    }
}
