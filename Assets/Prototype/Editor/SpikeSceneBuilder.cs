using Scatter.Diagnostics;
using Scatter.Rendering.Spike;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Prototype.Editor
{
    /// <summary>
    /// Creates the Phase 1 spike content: the <see cref="ScatterSpikeResources"/> asset of the package and the
    /// scene <c>Assets/Scenes/Spike_Draw.unity</c> (a copy of Bootstrap with a <see cref="ScatterSpike"/> driver and its <see cref="SpikeBenchmark"/>).
    /// Command line: Unity.exe -batchmode -quit -projectPath &lt;path&gt; -executeMethod Prototype.Editor.SpikeSceneBuilder.CreateSpikeContent -logFile &lt;file&gt;
    /// </summary>
    public static class SpikeSceneBuilder
    {
        private const string ShaderFolder = "Packages/com.gaiagames.scatter/Shaders/Spike/";
        private const string ResourcesFolder = "Packages/com.gaiagames.scatter/Runtime/Rendering/Spike/Resources";
        private const string ResourcesPath = ResourcesFolder + "/" + ScatterSpikeResources.ResourceName + ".asset";
        private const string TemplateScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string SpikeScenePath = "Assets/Scenes/Spike_Draw.unity";
        private const float GroundScale = 15f; // Unity plane: 10 m, so 150 m = the spike area.

        [MenuItem("Scatter Prototype/Create Spike Content")]
        public static void CreateSpikeContent()
        {
            // Design 6.3: players keep the DOTS_INSTANCING_ON variants BRG draws with. The default
            // (keep if Entities Graphics is installed) strips them in this project. The editor property is
            // read-only: the value is written in the serialized Graphics Settings.
            var graphicsSettings = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
            graphicsSettings.FindProperty("m_BrgStripping").intValue = (int)BatchRendererGroupStrippingMode.KeepAll;
            graphicsSettings.ApplyModifiedPropertiesWithoutUndo();

            ScatterSpikeResources resources = CreateOrUpdateResources();
            CreateScene(resources);
            AssetDatabase.SaveAssets();
            Debug.Log("Spike content created: " + ResourcesPath + ", " + SpikeScenePath
                + "; BatchRendererGroup variants: " + EditorGraphicsSettings.batchRendererGroupShaderStrippingMode);
        }

        private static ScatterSpikeResources CreateOrUpdateResources()
        {
            var resources = AssetDatabase.LoadAssetAtPath<ScatterSpikeResources>(ResourcesPath);
            if (resources == null)
            {
                if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                {
                    AssetDatabase.CreateFolder("Packages/com.gaiagames.scatter/Runtime/Rendering/Spike", "Resources");
                }

                resources = ScriptableObject.CreateInstance<ScatterSpikeResources>();
                AssetDatabase.CreateAsset(resources, ResourcesPath);
            }

            var serialized = new SerializedObject(resources);
            serialized.FindProperty("_cullCompute").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderFolder + "SpikeCull.compute");
            serialized.FindProperty("_bladeMeshShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "SpikeBladeMesh.shader");
            serialized.FindProperty("_bladeProceduralShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "SpikeBladeProcedural.shader");
            serialized.FindProperty("_objectShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "SpikeObject.shader");
            serialized.FindProperty("_objectMesh").objectReferenceValue = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(resources);
            return resources;
        }

        private static void CreateScene(ScatterSpikeResources resources)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SpikeScenePath) == null)
            {
                AssetDatabase.CopyAsset(TemplateScenePath, SpikeScenePath);
            }

            var scene = EditorSceneManager.OpenScene(SpikeScenePath, OpenSceneMode.Single);
            GameObject ground = GameObject.Find("Ground");
            if (ground != null)
            {
                ground.transform.localScale = new Vector3(GroundScale, 1f, GroundScale);
            }

            var spike = Object.FindAnyObjectByType<ScatterSpike>();
            if (spike == null)
            {
                spike = new GameObject("Scatter Spike").AddComponent<ScatterSpike>();
            }

            var serialized = new SerializedObject(spike);
            serialized.FindProperty("_resources").objectReferenceValue = resources;
            serialized.FindProperty("_cullCamera").objectReferenceValue = Camera.main;
            var hud = Object.FindAnyObjectByType<ScatterDebugHud>();
            serialized.FindProperty("_hud").objectReferenceValue = hud;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var benchmark = spike.GetComponent<SpikeBenchmark>();
            if (benchmark == null)
            {
                benchmark = spike.gameObject.AddComponent<SpikeBenchmark>();
            }

            var serializedBenchmark = new SerializedObject(benchmark);
            serializedBenchmark.FindProperty("_spike").objectReferenceValue = spike;
            serializedBenchmark.FindProperty("_hud").objectReferenceValue = hud;
            serializedBenchmark.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings();
        }

        private static void AddToBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == SpikeScenePath)
                {
                    return;
                }
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(SpikeScenePath, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
