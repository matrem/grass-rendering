using UnityEngine;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Shaders of the Phase 1 spike. The asset lives in a Resources folder of the package so that Play Mode
    /// tests also find it in players (<see cref="Load"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Scatter/Spike Resources", fileName = ResourceName)]
    public sealed class ScatterSpikeResources : ScriptableObject
    {
        public const string ResourceName = "ScatterSpikeResources";

        [SerializeField, Tooltip("SpikeCull.compute: placement, cull and draw arguments.")]
        private ComputeShader _cullCompute;

        [SerializeField, Tooltip("Hidden/Scatter/Spike/Blade Mesh.")]
        private Shader _bladeMeshShader;

        [SerializeField, Tooltip("Hidden/Scatter/Spike/Blade Procedural.")]
        private Shader _bladeProceduralShader;

        [SerializeField, Tooltip("Hidden/Scatter/Spike/Object.")]
        private Shader _objectShader;

        [SerializeField, Tooltip("Mesh of the Object layer (small rocks).")]
        private Mesh _objectMesh;

        public ComputeShader CullCompute => _cullCompute;
        public Shader BladeMeshShader => _bladeMeshShader;
        public Shader BladeProceduralShader => _bladeProceduralShader;
        public Shader ObjectShader => _objectShader;
        public Mesh ObjectMesh => _objectMesh;

        /// <summary>The asset of the package Resources folder.</summary>
        public static ScatterSpikeResources Load()
        {
            return Resources.Load<ScatterSpikeResources>(ResourceName);
        }
    }
}
