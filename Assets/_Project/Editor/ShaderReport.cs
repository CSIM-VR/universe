using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// Space Between Us > Debug > Report Broken Shaders: lists every shader used in the open scene that has
    /// compile errors or is not supported (these render magenta), with the error messages.
    /// Written to Logs/ShaderReport.txt and the Console.
    /// </summary>
    public static class ShaderReport
    {
        [MenuItem("Space Between Us/Debug/Report Broken Shaders")]
        static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Pipeline: {(GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "NONE (built-in)")}");
            sb.AppendLine($"Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}  API: {SystemInfo.graphicsDeviceType}  Target: {EditorUserBuildSettings.activeBuildTarget}");

            var byShader = new Dictionary<Shader, List<string>>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                {
                    if (!m) { Add(byShader, null, Path(r.transform) + " (missing material)"); continue; }
                    Add(byShader, m.shader, Path(r.transform) + " [" + m.name + "]");
                }

            int bad = 0;
            foreach (var kv in byShader)
            {
                var s = kv.Key;
                bool err = s && ShaderUtil.ShaderHasError(s);
                bool broken = !s || err || !s.isSupported || s.name.StartsWith("Hidden/InternalErrorShader");
                sb.AppendLine($"{(broken ? "BAD " : "ok  ")}{(s ? s.name : "<null>")}  supported={(s && s.isSupported)} error={err}  users={kv.Value.Count}");
                if (!broken) continue;
                bad++;
                if (s)
                    foreach (var msg in ShaderUtil.GetShaderMessages(s))
                        sb.AppendLine($"     {msg.severity} {msg.platform} line {msg.line}: {msg.message} ({msg.file})");
                for (int i = 0; i < Mathf.Min(6, kv.Value.Count); i++) sb.AppendLine("     used by " + kv.Value[i]);
            }
            sb.AppendLine($"{bad} broken shader(s).");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ShaderReport.txt", sb.ToString());
            Debug.Log("[ShaderReport]\n" + sb);
        }

        static void Add(Dictionary<Shader, List<string>> d, Shader s, string who)
        {
            s ??= Shader.Find("Hidden/InternalErrorShader");
            if (!d.TryGetValue(s, out var l)) d[s] = l = new List<string>();
            l.Add(who);
        }

        static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
    }
}
