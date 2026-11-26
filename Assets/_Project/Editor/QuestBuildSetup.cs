using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// Space Between Us > Build > Prepare Quest Build: sets the Android package name, makes Prototype_Core the
    /// only scene in the build, turns hand tracking off (controllers only; also done before every Android build), and sets the target API to 34, the level Meta's Project Setup Tool asks for (and resets
    /// to). androidx.core 1.15 from the Meta XR SDK needs compileSdk 35+; GradleCompileSdkFix raises only compileSdk
    /// in the generated Gradle project, so the target can stay 34. With an active build profile that overrides Player Settings
    /// (our Meta Quest profile does) these calls write into that profile. Done through the editor API (editing ProjectSettings files while Unity is open gets
    /// overwritten by its in-memory copy).
    /// </summary>
    public class QuestBuildSetup : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        /// <summary>
        /// Every Android build: controllers only, no hand tracking. The game is played with Touch controllers; with
        /// hand tracking on, the Quest drops to hands when the controllers are idle and the hands stop following them.
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) ControllersOnly();
        }

        static void ControllersOnly()
        {
            var cfg = OVRProjectConfig.CachedProjectConfig;
            if (!cfg || cfg.handTrackingSupport == OVRProjectConfig.HandTrackingSupport.ControllersOnly) return;
            cfg.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersOnly;
            OVRProjectConfig.CommitProjectConfig(cfg);
            Debug.Log("[QuestBuild] Hand tracking support -> Controllers Only");
        }

        public const string PackageName = "com.csimvr.spacebetweenus";
        const int TargetApi = 34;

        [MenuItem("Space Between Us/Build/Prepare Quest Build", priority = 60)]
        static void Prepare()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            ControllersOnly();
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
