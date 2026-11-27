using System.Collections;
using System.IO;
using NUnit.Framework;
using Scatter.Diagnostics;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Scatter.Tests.PlayMode.Diagnostics
{
    public sealed class ScatterDebugHudTests
    {
        private const int FramesToWait = 5;

        private GameObject _hudObject;
        private PanelSettings _panelSettings;
        private ThemeStyleSheet _theme;
        private VisualElement _panelRoot;
        private string _csvPath;

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_hudObject);
            Object.Destroy(_panelSettings);
            Object.Destroy(_theme);
            _panelRoot = null;
            if (_csvPath != null)
            {
                File.Delete(_csvPath);
                _csvPath = null;
            }
        }

        [UnityTest]
        public IEnumerator Hud_AttachesLabel_WhenPanelLoadedBeforeHud()
        {
            _hudObject = new GameObject("ScatterDebugHud");
            PanelRenderer panelRenderer = _hudObject.AddComponent<PanelRenderer>();
            panelRenderer.panelSettings = CreatePanelSettings();
            yield return null;

            _hudObject.AddComponent<ScatterDebugHud>();
            yield return WaitFrames();

            AssertHudLabelShown(panelRenderer);
        }

        [UnityTest]
        public IEnumerator Hud_AttachesLabel_WhenPanelLoadedAfterHud()
        {
            _hudObject = new GameObject("ScatterDebugHud");
            _hudObject.AddComponent<ScatterDebugHud>();
            yield return null;

            PanelRenderer panelRenderer = _hudObject.GetComponent<PanelRenderer>();
            panelRenderer.panelSettings = CreatePanelSettings();
            yield return WaitFrames();

            AssertHudLabelShown(panelRenderer);
        }

        [UnityTest]
        public IEnumerator Hud_SamplesFrames()
        {
            _hudObject = new GameObject("ScatterDebugHud");
            ScatterDebugHud hud = _hudObject.AddComponent<ScatterDebugHud>();

            yield return WaitFrames();

            Assert.That(hud.Sampler.History.Count, Is.GreaterThan(0));
            Assert.That(hud.Sampler.Latest.FrameTimeMs, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator Hud_ReattachesLabel_AfterDisableAndEnable()
        {
            PanelRenderer panelRenderer = CreateHudWithPanel(out ScatterDebugHud hud);
            yield return WaitFrames();

            hud.enabled = false;
            yield return null;
            Assert.That(GetPanelRoot(panelRenderer).Q<Label>(), Is.Null);

            hud.enabled = true;
            yield return WaitFrames();
            AssertHudLabelShown(panelRenderer);
        }

        [UnityTest]
        public IEnumerator Hud_VisibleFalse_HidesLabel()
        {
            PanelRenderer panelRenderer = CreateHudWithPanel(out ScatterDebugHud hud);
            yield return WaitFrames();
            Label label = GetPanelRoot(panelRenderer).Q<Label>();

            hud.Visible = false;
            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.None));

            hud.Visible = true;
            Assert.That(label.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [UnityTest]
        public IEnumerator Hud_StopRecording_WritesCsv()
        {
            _hudObject = new GameObject("ScatterDebugHud");
            ScatterDebugHud hud = _hudObject.AddComponent<ScatterDebugHud>();
            yield return null;

            hud.StartRecording();
            yield return WaitFrames();
            hud.StopRecording();

            AssertCsvWritten(hud.LastCsvPath);
        }

        [UnityTest]
        public IEnumerator Hud_DisabledWhileRecording_WritesCsv()
        {
            _hudObject = new GameObject("ScatterDebugHud");
            ScatterDebugHud hud = _hudObject.AddComponent<ScatterDebugHud>();
            yield return null;

            hud.StartRecording();
            yield return WaitFrames();
            hud.enabled = false;

            Assert.That(hud.IsRecording, Is.False);
            AssertCsvWritten(hud.LastCsvPath);
        }

        private static IEnumerator WaitFrames()
        {
            for (int i = 0; i < FramesToWait; i++)
            {
                yield return null;
            }
        }

        private PanelRenderer CreateHudWithPanel(out ScatterDebugHud hud)
        {
            _hudObject = new GameObject("ScatterDebugHud");
            PanelRenderer panelRenderer = _hudObject.AddComponent<PanelRenderer>();
            panelRenderer.panelSettings = CreatePanelSettings();
            hud = _hudObject.AddComponent<ScatterDebugHud>();
            return panelRenderer;
        }

        private PanelSettings CreatePanelSettings()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            _panelSettings.themeStyleSheet = _theme;
            return _panelSettings;
        }

        private VisualElement GetPanelRoot(PanelRenderer panelRenderer)
        {
            // The root is only exposed through the reload callback, which is invoked at once when the panel is loaded.
            _panelRoot = null;
            PanelRenderer.VersionedUIReloadCallback capture = (_, root, _) => _panelRoot = root;
            panelRenderer.RegisterUIReloadCallback(capture);
            panelRenderer.UnregisterUIReloadCallback(capture);

            Assert.That(_panelRoot, Is.Not.Null);
            return _panelRoot;
        }

        private void AssertHudLabelShown(PanelRenderer panelRenderer)
        {
            Label label = GetPanelRoot(panelRenderer).Q<Label>();
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Does.StartWith("SCATTER"));
        }

        private void AssertCsvWritten(string path)
        {
            _csvPath = path;
            Assert.That(File.Exists(path), Is.True, path);

            string[] lines = File.ReadAllLines(path);
            int columnCount = FrameCsvColumns.CreateNames().Length;
            Assert.That(lines[0], Is.EqualTo(string.Join(",", FrameCsvColumns.CreateNames())));
            Assert.That(lines.Length - 1, Is.GreaterThanOrEqualTo(FramesToWait - 1), "recorded frames");
            for (int i = 1; i < lines.Length; i++)
            {
                Assert.That(lines[i].Split(',').Length, Is.EqualTo(columnCount), lines[i]);
            }
        }
    }
}
