using UnityEditor;
using UnityEngine;

namespace Scatter.Editor
{
    /// <summary>
    /// Editor menu entries of the scatter package.
    /// </summary>
    public static class ScatterMenuItems
    {
        private const string DebugHudPrefabPath = "Packages/com.gaiagames.scatter/Runtime/Diagnostics/ScatterDebugHud.prefab";

        /// <summary>Adds the debug HUD (design 12.1) to the open scene.</summary>
        [MenuItem("GameObject/Scatter/Debug HUD", false, 10)]
        public static void CreateDebugHud(MenuCommand command)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DebugHudPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            GameObjectUtility.SetParentAndAlign(instance, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(instance, "Create Scatter Debug HUD");
            Selection.activeObject = instance;
        }
    }
}
