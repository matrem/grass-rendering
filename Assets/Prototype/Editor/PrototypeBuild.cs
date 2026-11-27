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
        private const string SpikeDevelopmentOutputPath = "Builds/WindowsSpikeDevelopment/GrassRendering.exe";
        private const string SpikeScenePath = "Assets/Scenes/Spike_Draw.unity";

        [MenuItem("Scatter Prototype/Build Windows Player")]
        public static void BuildWindows()
        {
            Build(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(), WindowsOutputPath, BuildOptions.None);
        }

        /// <summary>
        /// Development build of the spike scene alone: the Frame Debugger and the Profiler can attach to it
        /// (Frame Debugger target dropdown). Output in Builds/WindowsSpikeDevelopment.
        /// </summary>
        [MenuItem("Scatter Prototype/Build Spike Development Player")]
        public static void BuildSpikeDevelopment()
        {
            Build(new[] { SpikeScenePath }, SpikeDevelopmentOutputPath, BuildOptions.Development | BuildOptions.ConnectWithProfiler);
        }

        private static void Build(string[] scenes, string outputPath, BuildOptions buildOptions)
        {
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = buildOptions,
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
