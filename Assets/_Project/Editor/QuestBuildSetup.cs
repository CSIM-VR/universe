using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// Space Between Us > Build > Prepare Quest Build: sets the Android package name, makes Prototype_Core the
    /// only scene in the build, and sets the target API to 34, the level Meta's Project Setup Tool asks for (and resets
    /// to). androidx.core 1.15 from the Meta XR SDK needs compileSdk 35+; GradleCompileSdkFix raises only compileSdk
    /// in the generated Gradle project, so the target can stay 34. With an active build profile that overrides Player Settings
    /// (our Meta Quest profile does) these calls write into that profile. Done through the editor API (editing ProjectSettings files while Unity is open gets
    /// overwritten by its in-memory copy).
    /// </summary>
    public static class QuestBuildSetup
    {
        public const string PackageName = "com.csimvr.spacebetweenus";
        const int TargetApi = 34;

        [MenuItem("Space Between Us/Build/Prepare Quest Build", priority = 60)]
        static void Prepare()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            if ((int)PlayerSettings.Android.targetSdkVersion != TargetApi)
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetApi;

            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path != PrototypeSceneBuilder.ScenePath);
            if (scenes.Count == 0) scenes.Add(new EditorBuildSettingsScene(PrototypeSceneBuilder.ScenePath, true));
            scenes[0].enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestBuild] Package = {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}; " +
                      $"target API = {(int)PlayerSettings.Android.targetSdkVersion}; " +
                      $"scenes = {string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path))}. " +
                      "Now File > Build Profiles > Meta Quest > Build (or Build And Run with the headset plugged in).");
        }
    }
}
