using UnityEditor;
using UnityEditor.SceneManagement;
using SpaceBetweenUs.Tests;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// Menu: Space Between Us > Run Core Loop Smoke Test - plays Prototype_Core and lets CoreLoopSmokeTest
    /// drive the full loop; results appear in the Console as [SmokeTest] lines.
    /// Batch mode: Unity -batchmode -projectPath . -executeMethod SpaceBetweenUs.EditorTools.PrototypeSmokeTest.RunBatch -logFile -
    /// </summary>
    public static class PrototypeSmokeTest
    {
        [MenuItem("Space Between Us/Run Core Loop Smoke Test", priority = 20)]
        public static void RunMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!System.IO.File.Exists(PrototypeSceneBuilder.ScenePath)) PrototypeSceneBuilder.Build();
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
            CoreLoopSmokeTest.Request(exitWhenDone: false);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Rebuilds the scene, plays the loop and exits with 0 (pass) or 1 (fail).</summary>
        public static void RunBatch()
        {
            PrototypeSceneBuilder.Build();
            EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
            CoreLoopSmokeTest.Request(exitWhenDone: true);
            EditorApplication.EnterPlaymode();
        }

        public static void BuildBatch()
        {
            PrototypeSceneBuilder.Build();
            EditorApplication.Exit(0);
        }
    }
}
