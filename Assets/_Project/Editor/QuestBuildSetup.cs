using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// Runs before every Android build (no menu): turns hand tracking off, controllers only.
    /// Package name (com.csimvr.spacebetweenus), target API 34 and the scene list are already saved in the
    /// Meta Quest build profile and the build settings. androidx.core 1.15 from the Meta XR SDK needs compileSdk 35+;
    /// GradleCompileSdkFix raises only compileSdk in the generated Gradle project, so the target can stay 34.
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
    }
}
