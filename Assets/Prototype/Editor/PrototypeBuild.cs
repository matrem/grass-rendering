using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Prototype.Editor
{
    /// <summary>
    /// Player builds of the prototype. Command line:
    /// Unity.exe -batchmode -quit -projectPath &lt;path&gt; -executeMethod Prototype.Editor.PrototypeBuild.BuildWindows -logFile &lt;file&gt;
    /// </summary>
    public static class PrototypeBuild
    {
        private const string WindowsOutputPath = "Builds/Windows/GrassRendering.exe";

        [MenuItem("Scatter Prototype/Build Windows Player")]
        public static void BuildWindows()
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = WindowsOutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"Build {summary.result}: {summary.outputPath}, {summary.totalSize} bytes, {summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalTime}");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
