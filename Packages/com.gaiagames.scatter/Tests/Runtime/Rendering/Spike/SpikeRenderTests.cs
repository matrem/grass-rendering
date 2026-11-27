using System.Collections;
using NUnit.Framework;
using Scatter.Rendering.Spike;
using UnityEngine;
using UnityEngine.TestTools;

namespace Scatter.Tests.PlayMode.Rendering.Spike
{
    /// <summary>
    /// Renders the spike with each blade draw type to a render texture and checks the pixels: every region
    /// drawn (debug view colours), no error shader (magenta). Also run in a Windows player, where strict shader
    /// variant matching shows stripped BRG variants as the error shader. A failing case saves its image under
    /// temporaryCachePath/SpikeRenderTests.
    /// </summary>
    public sealed class SpikeRenderTests
    {
        private const int Size = 256;
        private const int FramesToRender = 6;
        private const float MinCoverage = 0.002f;
        private static readonly int s_DebugView = Shader.PropertyToID("_ScatterDebugView");

        private ScatterSpikeSystem _system;
        private GameObject _cameraObject;
        private GameObject _lightObject;
        private RenderTexture _target;
        private Texture2D _readback;

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            Shader.SetGlobalFloat(s_DebugView, 0f);
            _system?.Dispose();
            _system = null;
            Object.Destroy(_cameraObject);
            Object.Destroy(_lightObject);
            if (_target != null)
            {
                _target.Release();
                Object.Destroy(_target);
            }

            Object.Destroy(_readback);
        }

        [UnityTest]
        public IEnumerator Render_VisibleOffset_DrawsEveryRegion_WithoutErrorShader([Values] SpikeDrawType drawType)
        {
            CreateScene(drawType, SpikeRegionOffsetMode.VisibleOffset, true);
            yield return Render();

            Coverage coverage = Measure();
            string details = SaveImage("Regions_" + drawType) + " " + coverage;
            Assert.That(coverage.Error, Is.EqualTo(0f), "Error shader (magenta): a shader variant is missing. " + details);
            Assert.That(coverage.Lod0, Is.GreaterThan(MinCoverage), "LOD0 region not drawn. " + details);
            Assert.That(coverage.Lod1, Is.GreaterThan(MinCoverage), "LOD1 region not drawn. " + details);
            Assert.That(coverage.Objects, Is.GreaterThan(MinCoverage), "Object region not drawn. " + details);
        }

        /// <summary>
        /// Phase 1 finding: where BRG uses raw buffers (D3D12, Vulkan), it rejects commands with a visible instances
        /// window and draws nothing. It prints (natively, not through the managed log) "BatchRendererGroup had non-zero
        /// ConstantBuffer mode parameters when BufferTarget is RawBuffer". The window is for the constant buffer mode only.
        /// </summary>
        [UnityTest]
        public IEnumerator Render_WindowOffset_IsRejectedInRawBufferMode(
            [Values(SpikeRegionOffsetMode.WindowOffsetBytes, SpikeRegionOffsetMode.WindowOffsetEntries)] SpikeRegionOffsetMode offsetMode)
        {
            Assume.That(UnityEngine.Rendering.BatchRendererGroup.BufferTarget, Is.EqualTo(UnityEngine.Rendering.BatchBufferTarget.RawBuffer));

            // In players the BRG error also reaches the managed log, once per command and view: expected here.
            LogAssert.ignoreFailingMessages = true;
            CreateScene(SpikeDrawType.MeshIndirect, offsetMode, true);
            yield return Render();

            Coverage coverage = Measure();
            Assert.That(coverage.Lod0 + coverage.Lod1 + coverage.Objects, Is.EqualTo(0f),
                "BRG drew with a visible instances window: update the Phase 1 findings. " + coverage);
        }

        [UnityTest]
        public IEnumerator Render_LitShading_DrawsGreenBlades([Values] SpikeDrawType drawType)
        {
            CreateScene(drawType, SpikeRegionOffsetMode.VisibleOffset, false);
            yield return Render();

            Coverage coverage = Measure();
            string details = SaveImage("Lit_" + drawType) + " " + coverage;
            Assert.That(coverage.Error, Is.EqualTo(0f), "Error shader (magenta): a shader variant is missing. " + details);
            Assert.That(coverage.Green, Is.GreaterThan(0.05f), "Lit blades not drawn. " + details);
        }

        /// <summary>Saves the rendered image (gamma-encoded PNG) for inspection; returns its path.</summary>
        private string SaveImage(string label)
        {
            Color[] pixels = _readback.GetPixels();
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = pixels[i].gamma;
            }

            image.SetPixels(pixels);
            string folder = System.IO.Path.Combine(Application.temporaryCachePath, "SpikeRenderTests");
            System.IO.Directory.CreateDirectory(folder);
            string path = System.IO.Path.Combine(folder, label + ".png");
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            Object.Destroy(image);
            return path;
        }

        private void CreateScene(SpikeDrawType drawType, SpikeRegionOffsetMode offsetMode, bool debugView)
        {
            ScatterSpikeResources resources = ScatterSpikeResources.Load();
            Assert.That(resources, Is.Not.Null, "Spike resources asset missing from the package Resources folder.");

            ScatterSpikeConfig config = ScatterSpikeConfig.Default;
            config.MaxBladeCount = 1 << 18;
            config.BladeCount = 1 << 18;
            config.ObjectCount = 1 << 12;
            config.BladeDrawType = drawType;
            config.OffsetMode = offsetMode;
            config.BladesPerInstance = 16;
            _system = new ScatterSpikeSystem(resources, config);
            Shader.SetGlobalFloat(s_DebugView, debugView ? 1f : 0f);

            _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGBHalf) { name = "SpikeRenderTests Target" };
            _readback = new Texture2D(Size, Size, TextureFormat.RGBAHalf, false, true);
            _cameraObject = new GameObject("SpikeRenderTests Camera");
            _cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 2f, -40f), Quaternion.Euler(8f, 0f, 0f));
            Camera camera = _cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.farClipPlane = 200f;
            camera.targetTexture = _target;

            _lightObject = new GameObject("SpikeRenderTests Light");
            _lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = _lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
        }

        private IEnumerator Render()
        {
            // The camera renders into the target at the end of each frame; the cull runs before, as in LateUpdate.
            Camera camera = _cameraObject.GetComponent<Camera>();
            for (int frame = 0; frame < FramesToRender; frame++)
            {
                _system.Tick(camera, false);
                yield return null;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            _readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            _readback.Apply();
            RenderTexture.active = previous;
        }

        /// <summary>Fraction of pixels per kind, from the debug view colours of ScatterSpikeSystem.</summary>
        private Coverage Measure()
        {
            Color[] pixels = _readback.GetPixels();
            var coverage = new Coverage();
            for (int i = 0; i < pixels.Length; i++)
            {
                // Gamma space: Material.SetColor converts the debug colours to linear; the sRGB curve (close to a
                // power) keeps the min/max ratios of the original colours.
                Color pixel = pixels[i].gamma;
                float max =Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                float min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                if (max < 0.05f)
                {
                    continue;
                }

                if (pixel.r > 0.7f && pixel.b > 0.7f && pixel.g < 0.2f)
                {
                    coverage.Error++;
                }

                if (pixel.g > pixel.r * 1.2f && pixel.g > pixel.b * 1.2f)
                {
                    coverage.Green++;
                }

                // Debug colours: LOD0 saturated (min/max about 0.22), LOD1 lighter (about 0.55), objects grey (1).
                float ratio = min / max;
                if (ratio < 0.35f)
                {
                    coverage.Lod0++;
                }
                else if (ratio > 0.42f && ratio < 0.75f)
                {
                    coverage.Lod1++;
                }
                else if (ratio > 0.9f)
                {
                    coverage.Objects++;
                }
            }

            float scale = 1f / pixels.Length;
            coverage.Error *= scale;
            coverage.Green *= scale;
            coverage.Lod0 *= scale;
            coverage.Lod1 *= scale;
            coverage.Objects *= scale;
            return coverage;
        }

        private struct Coverage
        {
            public float Error;
            public float Green;
            public float Lod0;
            public float Lod1;
            public float Objects;

            public override string ToString()
            {
                return $"(coverage: error {Error:F4}, green {Green:F4}, LOD0 {Lod0:F4}, LOD1 {Lod1:F4}, objects {Objects:F4})";
            }
        }
    }
}
