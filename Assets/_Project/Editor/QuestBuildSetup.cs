using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// Space Between Us > Build > Prepare Quest Build: sets the Android package name and makes Prototype_Core the
    /// only scene in the build. Done through the editor API (editing ProjectSettings files while Unity is open gets
    /// overwritten by its in-memory copy).
    /// </summary>
    public static class QuestBuildSetup
    {
        public const string PackageName = "com.csimvr.spacebetweenus";

        [MenuItem("Space Between Us/Build/Prepare Quest Build", priority = 60)]
        static void Prepare()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);

            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path != PrototypeSceneBuilder.ScenePath);
            if (scenes.Count == 0) scenes.Add(new EditorBuildSettingsScene(PrototypeSceneBuilder.ScenePath, true));
            scenes[0].enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestBuild] Package = {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}; " +
                      $"scenes = {string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path))}. " +
                      "Now File > Build Profiles > Meta Quest > Build (or Build And Run with the headset plugged in).");
        }
    }
}
