using System.Collections;
using NUnit.Framework;
using Scatter.Diagnostics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Scatter.Tests.PlayMode.Diagnostics
{
    public sealed class GpuMarkerProbeTests
    {
        [UnityTest]
        public IEnumerator Probe_ReportsRenderMarkersWithGpuTime()
        {
            // Batch mode has no window: the camera renders into a render texture.
            var target = new RenderTexture(128, 128, 24);
            var cameraObject = new GameObject("GpuMarkerProbeTests Camera");
            cameraObject.AddComponent<Camera>().targetTexture = target;
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(0f, 0f, 5f);
            var probe = new GpuMarkerProbe();
            try
            {
                yield return null;
                probe.Start();
                for (int frame = 0; frame < 10; frame++)
                {
                    yield return null;
                    probe.Sample();
                }

                string report = probe.Report();
                Assert.That(probe.MarkerCount, Is.GreaterThan(0));
                Assert.That(report, Does.StartWith("GPU marker probe: "));
                Assert.That(report, Does.Contain(" ms "), "No marker with GPU time: " + report);
            }
            finally
            {
                probe.Dispose();
                Object.Destroy(cameraObject);
                Object.Destroy(cube);
                target.Release();
            }
        }
    }
}
