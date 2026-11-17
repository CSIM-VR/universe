using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// Menu: Space Between Us > Bake Star Nest Skybox.
    /// Star Nest is a per-pixel fractal (100+ loop steps per pixel) and not written for single-pass stereo,
    /// so on Quest it would be slow and only show in one eye. Instead we render it ONCE in the Editor into a
    /// cubemap and use Unity's own Skybox/Cubemap shader at runtime (cheap, works in both eyes).
    /// The scene builder / palette pick up the baked sky automatically.
    /// </summary>
    public static class PrototypeSkybox
    {
        const string Dir = "Assets/_Project/Materials/Skybox";
        public const string SkyMatPath = Dir + "/Sky_StarNest.mat";
        const string LiveMatPath = Dir + "/StarNest_Source.mat";
        const string CubePath = Dir + "/StarNest_Baked.asset";

        // ------------------------------------------------------------------ choose the sky

        const string SkyPref = "SpaceBetweenUs.SkyMaterial";
        const string NebulaDir = "Assets/Nebula Skyboxes";

        /// <summary>The sky the scene should use: the one picked in Space Between Us > Sky, else the baked Star Nest.</summary>
        public static Material CurrentSky()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(EditorPrefs.GetString(SkyPref, SkyMatPath));
            return m ? m : AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
        }

        [MenuItem("Space Between Us/Sky/Star Nest (baked)", priority = 20)]
        static void UseStarNest()
        {
            if (!AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath) && !Bake()) return;
            Use(SkyMatPath);
        }

        [MenuItem("Space Between Us/Sky/Nebula 01", priority = 21)] static void N1() => UseNebula(1);
        [MenuItem("Space Between Us/Sky/Nebula 02", priority = 22)] static void N2() => UseNebula(2);
        [MenuItem("Space Between Us/Sky/Nebula 03", priority = 23)] static void N3() => UseNebula(3);
        [MenuItem("Space Between Us/Sky/Nebula 04", priority = 24)] static void N4() => UseNebula(4);

        static void UseNebula(int i)
        {
            string texPath = $"{NebulaDir}/Nebula_0{i}_Cubemap.exr";
            var tex = AssetDatabase.LoadAssetAtPath<Cubemap>(texPath);
            if (!tex)
            {
                Debug.LogError($"[Skybox] {texPath} not found (or not imported as a Cube texture).");
                return;
            }
            // Quest: 1024 per face + ASTC keeps it ~10 MB in the build instead of hundreds
            if (AssetImporter.GetAtPath(texPath) is TextureImporter ti)
            {
                var android = ti.GetPlatformTextureSettings("Android");
                if (!android.overridden || android.maxTextureSize > 1024)
                {
                    android.overridden = true;
                    android.maxTextureSize = 1024;
                    android.format = TextureImporterFormat.ASTC_6x6;
                    ti.SetPlatformTextureSettings(android);
                    ti.SaveAndReimport();
                    tex = AssetDatabase.LoadAssetAtPath<Cubemap>(texPath);
                }
            }
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Skybox");
            string matPath = $"{Dir}/Sky_Nebula_0{i}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat)
            {
                mat = new Material(Shader.Find("Skybox/Cubemap"));
                mat.SetFloat("_Exposure", 1f);
                mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_Tex", tex);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Use(matPath);
        }

        static void Use(string matPath)
        {
            EditorPrefs.SetString(SkyPref, matPath);
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == PrototypeSceneBuilder.ScenePath)
            {
                PrototypePalette.ApplyToScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Skybox] Prototype_Core now uses {matPath}. (Exposure / rotation: select the material and tweak.)");
            }
            else Debug.Log($"[Skybox] Selected {matPath}. Open Prototype_Core and pick it again (or rebuild the scene) to apply.");
        }

        /// <summary>Average colour of the sky looking up, around the horizon and down (renders a tiny cubemap).</summary>
        public static bool SampleSky(Material sky, out Color up, out Color horizon, out Color down)
        {
            up = horizon = down = Color.black;
            var cube = new Cubemap(32, TextureFormat.RGBA32, false);
            var previous = RenderSettings.skybox;
            var go = new GameObject("[SkySampleCamera]") { hideFlags = HideFlags.HideAndDontSave };
            bool ok;
            try
            {
                RenderSettings.skybox = sky;
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.cullingMask = 0;
                cam.allowHDR = false;
                ok = cam.RenderToCubemap(cube);
            }
            finally
            {
                RenderSettings.skybox = previous;
                Object.DestroyImmediate(go);
            }
            if (!ok) { Object.DestroyImmediate(cube); return false; }

            Color Avg(CubemapFace f)
            {
                var px = cube.GetPixels(f);
                Color sum = Color.black;
                foreach (var p in px) sum += p;
                return sum / px.Length;
            }
            up = Avg(CubemapFace.PositiveY);
            down = Avg(CubemapFace.NegativeY);
            horizon = (Avg(CubemapFace.PositiveX) + Avg(CubemapFace.NegativeX) + Avg(CubemapFace.PositiveZ) + Avg(CubemapFace.NegativeZ)) / 4f;
            Object.DestroyImmediate(cube);
            return (up + horizon + down).maxColorComponent > 0.001f;
        }

        // ------------------------------------------------------------------ bake

        [MenuItem("Space Between Us/Bake Star Nest Skybox", priority = 4)]
        public static void BakeMenu()
        {
            if (!Bake()) return;
            EditorPrefs.SetString(SkyPref, SkyMatPath);
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == PrototypeSceneBuilder.ScenePath)
            {
                PrototypePalette.ApplyToScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[Skybox] Prototype_Core now uses the baked Star Nest sky.");
            }
            else Debug.Log("[Skybox] Baked. Open Prototype_Core and run Space Between Us > Apply Color Palette (or rebuild) to use it.");
        }

        /// <summary>Edit StarNest_Source.mat (colours, brightness, density) and bake again to change the look.</summary>
        public static bool Bake(int faceSize = 1024)
        {
            var shader = Shader.Find("Skybox/StarNest");
            if (!shader)
            {
                Debug.LogError("[Skybox] Shader 'Skybox/StarNest' not found (Assets/StarNestSkybox/StarNestSkybox.shader).");
                return false;
            }
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Skybox");

            var source = AssetDatabase.LoadAssetAtPath<Material>(LiveMatPath);
            if (!source)
            {
                source = new Material(shader);
                source.SetColor("_Color", new Color(0.85f, 0.85f, 1f));   // a touch of violet to match the palette
                source.SetVector("_Scroll", new Vector4(1.3f, 1f, 0.6f, 0f)); // static: no scrolling
                source.SetVector("_Rotation", new Vector4(0f, 0f, 0f, 0f));
                source.SetVector("_Center", new Vector4(1f, 0.3f, 0.5f, 0f));
                source.SetFloat("_Iterations", 17);
                source.SetFloat("_Volsteps", 14);   // baking is offline, so we can afford the high-quality setting
                source.SetFloat("_Brightness", 0.45f);
                AssetDatabase.CreateAsset(source, LiveMatPath);
            }

            var cube = new Cubemap(faceSize, TextureFormat.RGBA32, false);
            var previous = RenderSettings.skybox;
            var go = new GameObject("[SkyBakeCamera]") { hideFlags = HideFlags.HideAndDontSave };
            bool ok;
            try
            {
                RenderSettings.skybox = source;
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.cullingMask = 0;          // sky only
                cam.allowHDR = false;
                ok = cam.RenderToCubemap(cube);
            }
            finally
            {
                RenderSettings.skybox = previous;
                Object.DestroyImmediate(go);
            }

            float avg = 0f;
            if (ok)
            {
                var px = cube.GetPixels(CubemapFace.PositiveZ);
                for (int i = 0; i < px.Length; i += 97) avg += px[i].grayscale;
                avg /= px.Length / 97f;
            }
            if (!ok || avg < 0.002f)
            {
                Debug.LogError($"[Skybox] Baking failed (render ok={ok}, brightness={avg:0.0000}). Tell Claude - nothing was changed.");
                return false;
            }

            var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(CubePath);
            if (existing)
            {
                EditorUtility.CopySerialized(cube, existing);
                cube = existing;
            }
            else AssetDatabase.CreateAsset(cube, CubePath);

            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
            if (!sky)
            {
                sky = new Material(Shader.Find("Skybox/Cubemap"));
                AssetDatabase.CreateAsset(sky, SkyMatPath);
            }
            sky.SetTexture("_Tex", cube);
            sky.SetFloat("_Exposure", 1f);
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));   // 0.5 = neutral for Skybox/Cubemap
            EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Skybox] Baked Star Nest into {CubePath} ({faceSize}px per face).");
            return true;
        }
    }
}
