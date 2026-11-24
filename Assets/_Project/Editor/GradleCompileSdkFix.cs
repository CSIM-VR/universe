using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// Keeps the Quest build compiling while the target API stays at 34 (what Meta's Project Setup Tool wants for the
    /// Store, and what it resets the target to). Meta XR SDK pulls in androidx.core 1.15, which needs compileSdk 35+,
    /// but Unity compiles against the target API, so Gradle stops in checkReleaseAarMetadata. After Unity writes the
    /// Gradle project, this raises compileSdk (only compileSdk) in every module to the newest installed Android
    /// platform that is 35 or higher. targetSdk is left as it is.
    /// </summary>
    public class GradleCompileSdkFix : IPostGenerateGradleAndroidProject
    {
        const int MinCompileSdk = 35, Fallback = 36;
        static readonly Regex CompileSdk = new(@"(compileSdk(?:Version)?\s*=?\s*)(\d+)");

        public int callbackOrder => 999;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var root = Path.GetDirectoryName(unityLibraryPath.TrimEnd('/', '\\'));
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
            int sdk = NewestPlatform();
            foreach (var file in Directory.GetFiles(root, "build.gradle", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}build{Path.DirectorySeparatorChar}")) continue;
                var text = File.ReadAllText(file);
                var fixedText = CompileSdk.Replace(text, m =>
                    int.Parse(m.Groups[2].Value) < MinCompileSdk ? m.Groups[1].Value + sdk : m.Value);
                if (fixedText == text) continue;
                File.WriteAllText(file, fixedText);
                Debug.Log($"[QuestBuild] compileSdk -> {sdk} in {file.Substring(root.Length + 1)} (targetSdk unchanged)");
            }
        }

        static int NewestPlatform()
        {
            var sdkRoot = EditorPrefs.GetString("AndroidSdkRoot");
            if (string.IsNullOrEmpty(sdkRoot) || !Directory.Exists(sdkRoot))
                sdkRoot = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "SDK");
            var platforms = Path.Combine(sdkRoot, "platforms");
            if (!Directory.Exists(platforms)) return Fallback;
            var levels = Directory.GetDirectories(platforms)
                .Select(d => Regex.Match(Path.GetFileName(d), @"^android-(\d+)$"))
                .Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value))
                .Where(n => n >= MinCompileSdk).ToList();
            return levels.Count > 0 ? levels.Max() : Fallback;
        }
    }
}
