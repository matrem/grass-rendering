using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Scatter.Rendering.Spike
{
    /// <summary>
    /// Phase 1b benchmark: changes the render scale and the main light shadow map resolution of a URP asset, and puts
    /// the original values back. In players the asset changes in memory only; in the Editor the project asset itself
    /// changes, so <see cref="Restore"/> must run (end of run, disable, quit).
    /// </summary>
    public sealed class SpikeUrpSettings
    {
        /// <summary>Value of a case that keeps the project setting (<see cref="Apply"/>).</summary>
        public const int ProjectSetting = 0;

        private readonly UniversalRenderPipelineAsset _asset;
        private readonly float _originalRenderScale;
        private readonly int _originalShadowResolution;

        /// <param name="asset">Asset to change; null: nothing is changed (no URP).</param>
        public SpikeUrpSettings(UniversalRenderPipelineAsset asset)
        {
            _asset = asset;
            if (asset != null)
            {
                _originalRenderScale = asset.renderScale;
                _originalShadowResolution = asset.mainLightShadowmapResolution;
            }
        }

        public bool IsAvailable => _asset != null;

        /// <summary>Current render scale; NaN without URP.</summary>
        public float RenderScale => _asset != null ? _asset.renderScale : float.NaN;

        /// <summary>Current main light shadow map resolution (texels per side); 0 without URP.</summary>
        public int ShadowResolution => _asset != null ? _asset.mainLightShadowmapResolution : 0;

        /// <summary>The URP asset of the current quality level, or of the Graphics Settings.</summary>
        public static SpikeUrpSettings ForActivePipeline()
        {
            return new SpikeUrpSettings(GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
        }

        /// <summary>
        /// Applies a render scale and a shadow map resolution; <see cref="ProjectSetting"/> (0) keeps the original value.
        /// Does not allocate.
        /// </summary>
        public void Apply(float renderScale, int shadowResolution)
        {
            if (_asset == null)
            {
                return;
            }

            _asset.renderScale = renderScale > 0f ? renderScale : _originalRenderScale;
            _asset.mainLightShadowmapResolution = shadowResolution > 0 ? shadowResolution : _originalShadowResolution;
        }

        public void Restore()
        {
            Apply(ProjectSetting, ProjectSetting);
        }
    }
}
