using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// "Twilight Garden" colour palette for the prototype materials.
    /// Builder = warm gold, Destroyer = cool blue; Rock = amber, Ice = pale cyan, Crystal = violet (as in MaterialType).
    /// Menu: Space Between Us > Apply Color Palette - rewrites colour / smoothness / emission only
    /// (textures, shaders and models you added are left alone). Also used by the scene builder for new materials.
    /// </summary>
    public static class PrototypePalette
    {
        const string MatDir = "Assets/_Project/Materials/Prototype";

        public struct Entry
        {
            public Color baseColor;
            public Color? emission;   // HDR (already multiplied by intensity)
            public float smoothness;
        }

        static Color H(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = alpha;
            return c;
        }

        static Entry E(string baseHex, float smooth, string emissionHex = null, float intensity = 1f, float alpha = 1f) => new Entry
        {
            baseColor = H(baseHex, alpha),
            emission = emissionHex == null ? null : H(emissionHex) * intensity,
            smoothness = smooth
        };

        public static readonly Dictionary<string, Entry> Lit = new()
        {
            // the three materials
            ["Rock"] = E("#C8924A", 0.15f, "#2A1600"),
            ["Ice"] = E("#BFEFFF", 0.95f, "#1E5A78", 1f, alpha: 0.55f),
            ["Crystal"] = E("#B38CFF", 0.9f, "#7A3DFF", 2f),

            // islands
            ["IslandTop"] = E("#8C7B6B", 0.1f),
            ["IslandRock"] = E("#4A3F4F", 0.08f),
            ["LakeBed"] = E("#2C3550", 0.3f),

            // life
            ["Soil"] = E("#6B4A2F", 0.05f),
            ["Moss"] = E("#7BC96F", 0.15f, "#0E2A0A"),
            ["Grass"] = E("#4FA36A", 0.2f, "#0A2214"),
            ["Trunk"] = E("#7A5236", 0.1f),
            ["Water"] = E("#3FA9E0", 0.95f, "#0B3550", 1f, alpha: 0.7f),
            ["Lotus"] = E("#FF9ECF", 0.5f, "#FF5FA8", 0.9f),
            ["Fish"] = E("#FF8A3D", 0.6f, "#5A2000"),

            // slot glows (what goes where)
            ["GoldGlow"] = E("#FFD166", 0.6f, "#FFB23F", 1.5f),
            ["IceGlow"] = E("#8EE3FF", 0.8f, "#3FC8FF", 1.5f),
            ["CrystalGlow"] = E("#C9A7FF", 0.8f, "#9B5CFF", 1.8f),

            // the two roles + the artifact
            ["BuilderHand"] = E("#FFD58A", 0.5f, "#FFB347", 0.8f),
            ["DestroyerHand"] = E("#6FA8FF", 0.6f, "#2F6BFF", 1.5f),
            ["Artifact"] = E("#FFF1C4", 0.9f, "#FFD27A", 2f),

            // asteroids
            ["AsteroidShell"] = E("#3D3A45", 0.08f),
            ["AsteroidInside"] = E("#9FD8F0", 0.8f, "#10384A"),
            ["AsteroidBroken"] = E("#26232B", 0.05f),
            ["XRayVein"] = E("#C08BFF", 0.7f, "#A04CFF", 3f),
        };

        public static readonly Dictionary<string, Color> Particles = new()
        {
            ["P_Stars"] = H("#EAF0FF"),
            ["P_GoldSpark"] = H("#FFD27A"),
            ["P_WaterDrop"] = H("#9EDCFF"),
            ["P_Dust"] = H("#A88B6A"),
            ["P_LightThread"] = H("#FFE6A8"),
        };

        public static readonly Color Background = H("#070A1C");   // deep indigo space
        public static readonly Color Ambient = H("#2E3350");
        public static readonly Color Sun = H("#FFE9C9");

        /// <summary>Applies the palette entry for this material name, if there is one. Returns true if applied.</summary>
        public static bool ApplyTo(Material mat, string name)
        {
            if (mat == null) return false;
            if (Lit.TryGetValue(name, out var e))
            {
                mat.SetColor("_BaseColor", e.baseColor);
                mat.SetColor("_Color", e.baseColor);
                mat.SetFloat("_Smoothness", e.smoothness);
                if (e.emission.HasValue)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", e.emission.Value);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
            }
            else if (Particles.TryGetValue(name, out var c))
            {
                mat.SetColor("_BaseColor", c);
                mat.SetColor("_Color", c);
            }
            else return false;

            EditorUtility.SetDirty(mat);
            return true;
        }

        /// <summary>Camera background, ambient light and starlight colour in the open scene.</summary>
        public static void ApplyToScene()
        {
            // the sky picked in Space Between Us > Sky (baked Star Nest by default), else plain deep indigo
            var sky = PrototypeSkybox.CurrentSky();
            RenderSettings.skybox = sky;

            Color background = Background, sun = Sun;
            if (sky && PrototypeSkybox.SampleSky(sky, out var up, out var horizon, out var down))
            {
                // ambient from the sky's own colours: sky (top) / equator / ground, brightened to a readable level
                // but keeping each direction's hue, so the islands are lit by the nebula around them
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = WithLuminance(up, 0.30f);
                RenderSettings.ambientEquatorColor = WithLuminance(horizon, 0.24f);
                RenderSettings.ambientGroundColor = WithLuminance(down, 0.14f);
                // starlight: warm white nudged 30 % toward the sky's average hue
                var avg = (up + horizon * 4f + down) / 6f;
                sun = Color.Lerp(Sun, WithLuminance(avg, 0.9f), 0.3f);
                background = avg;
                Debug.Log($"[Palette] Lighting matched to {sky.name}: ambient sky #{ColorUtility.ToHtmlStringRGB(RenderSettings.ambientSkyColor)}, " +
                          $"equator #{ColorUtility.ToHtmlStringRGB(RenderSettings.ambientEquatorColor)}, ground #{ColorUtility.ToHtmlStringRGB(RenderSettings.ambientGroundColor)}, " +
                          $"starlight #{ColorUtility.ToHtmlStringRGB(sun)}");
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Ambient;
            }

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.clearFlags != CameraClearFlags.SolidColor && cam.clearFlags != CameraClearFlags.Skybox) continue;
                cam.backgroundColor = background;
                cam.clearFlags = sky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            }
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (light.type == LightType.Directional) light.color = sun;
        }

        /// <summary>Same hue, given brightness (sky colours are very dark; we only want their tint).</summary>
        static Color WithLuminance(Color c, float target)
        {
            c.a = 1f;
            float l = c.grayscale;
            if (l < 1e-4f) return new Color(target, target, target, 1f);
            c *= target / l;
            float max = c.maxColorComponent;
            if (max > 1f) c /= max;   // stay in LDR range
            c.a = 1f;
            return c;
        }

        [MenuItem("Space Between Us/Apply Color Palette", priority = 2)]
        public static void ApplyMenu()
        {
            int count = 0;
            foreach (var name in Lit.Keys)
                if (ApplyTo(AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat"), name)) count++;
            foreach (var name in Particles.Keys)
                if (ApplyTo(AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat"), name)) count++;
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == PrototypeSceneBuilder.ScenePath)
            {
                ApplyToScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[Palette] Applied 'Twilight Garden' to {count} materials" +
                      (scene.path == PrototypeSceneBuilder.ScenePath ? " and the scene (background, ambient, starlight)." : ". Open Prototype_Core and run it again to recolour the sky/light too."));
        }
    }
}
