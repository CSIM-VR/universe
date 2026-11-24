using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// Menu: Space Between Us > Apply Imported Art.
    /// Dresses the generated prototype prefabs with the imported asset packs. Only visuals change:
    /// the grey-box renderers are switched off (colliders, slots, scripts and hierarchy stay exactly as they were)
    /// and a model is added as an "Art" child, auto-fitted to the size of the shape it replaces.
    /// Safe to run again (replaces its own "Art" children). "Reset Prototype Art" brings back the grey box.
    ///
    ///   Island body          -> floating rock: flat rock cap + pointed spire underneath (menu Island Style:
    ///                           Asteroids pack rocks / upside-down Valley Gray Cliff / Mid Moon low-poly)
    ///   Tree                 -> Tree 1 / Tree 2 / Tree 7 (one per island)
    ///   Bushes               -> Mushroom 1, Mushroom 2, Pine .001
    ///   Fish                 -> Floreswa fish01 / 02 / 03
    ///   Asteroid shell       -> Asteroids pack Asteroid_1..5 (one per asteroid), shell bits + debris too
    ///   Asteroid core        -> SineVFX crystal cluster (with our violet Crystal material)
    ///   Rock fragment        -> Mid Moon ROCK 1
    ///   Crystal fragment     -> SineVFX crystal cluster (violet Crystal material)
    ///   Slot markers         -> soft, slowly breathing halos (generated ring texture, additive)
    ///   Ice fragment         -> irregular Asteroids pack lump in the translucent Ice material + snowflake sparkles
    ///   Lotus + lily pads    -> generated in code (LotusGenerator), petals open when it appears
    /// The moss, reeds and hands keep the prototype look (no matching asset).
    /// </summary>
    public static class PrototypeArtSwap
    {
        const string PrefabDir = "Assets/_Project/Prefabs/Prototype";
        const string MatDir = "Assets/_Project/Materials/Prototype";
        const float LakeX = 0.12f, LakeZ = 0.1f, LakeR = 0.12f;   // PrototypeSceneBuilder.LakeLocal, lake radius
        const string MidMoon = "Assets/Mid Moon Studio/Floating Islands - vol 1/Prefabs";
        const string Asteroids = "Assets/Asteroids pack";
        const string Fish = "Assets/Floreswa/Prefabs";
        const string Crystals = "Assets/SineVFX/TranslucentCrystals/Prefabs";
        const string Flora = "Assets/3D_Game_Assets_Flora/Prefap";
        /// <summary>true: Flora's lotus model; false: the lotus generated in code (LotusGenerator, petals bloom open).</summary>
        const bool UseFloraLotus = true;
        const string Art = "Art";

        enum Fit { Max, Height, Xz }
        enum Align { Center, Bottom }

        static readonly List<string> Report = new();

        [MenuItem("Space Between Us/Apply Imported Art", priority = 3)]
        public static void ApplyMenu()
        {
            Report.Clear();
            ConvertAsteroidMaterials();
            Edit("Island", DressIsland);
            Edit("Asteroid", DressAsteroid);
            Edit("Fragment_Rock", DressRockFragment);
            Edit("Fragment_Ice", DressIceFragment);
            Edit("Fragment_Crystal", DressCrystalFragment);
            DressRevealInScene();
            DressHandsInScene();
            DressHintsInScene();
            DressSkyAndSoundInScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtSwap] Done:\n  " + string.Join("\n  ", Report) +
                      "\nPrefab instances in Prototype_Core update automatically. Press Play to see it.");
        }

        // ------------------------------------------------------------------ island style

        const string StylePref = "SpaceBetweenUs.IslandStyle";
        /// <summary>Which Asteroids pack material the Rock Spire islands are made of (Asteroid_1 .. Asteroid_5).</summary>
        const string IslandStoneMaterial = "Asteroid_1";
        /// <summary>Which Asteroids pack shape the ice fragments use (Asteroid_1 .. Asteroid_5).</summary>
        const string IceShape = "Asteroid_5";
        enum IslandStyle { RockSpire = 0, CliffSpire = 1, MidMoon = 2 }
        static IslandStyle Style => (IslandStyle)EditorPrefs.GetInt(StylePref, 0);

        [MenuItem("Space Between Us/Island Style/Rock Spire (Asteroids pack)", priority = 10)]
        static void StyleRock() { EditorPrefs.SetInt(StylePref, 0); ApplyMenu(); }

        [MenuItem("Space Between Us/Island Style/Cliff Spire (Valley Gray Cliff)", priority = 11)]
        static void StyleCliff() { EditorPrefs.SetInt(StylePref, 1); ApplyMenu(); }

        [MenuItem("Space Between Us/Island Style/Low-poly (Mid Moon)", priority = 12)]
        static void StyleMidMoon() { EditorPrefs.SetInt(StylePref, 2); ApplyMenu(); }

        [MenuItem("Space Between Us/Island Style/Rock Spire (Asteroids pack)", true)]
        static bool StyleRockCheck() { Menu.SetChecked("Space Between Us/Island Style/Rock Spire (Asteroids pack)", Style == IslandStyle.RockSpire); return true; }
        [MenuItem("Space Between Us/Island Style/Cliff Spire (Valley Gray Cliff)", true)]
        static bool StyleCliffCheck() { Menu.SetChecked("Space Between Us/Island Style/Cliff Spire (Valley Gray Cliff)", Style == IslandStyle.CliffSpire); return true; }
        [MenuItem("Space Between Us/Island Style/Low-poly (Mid Moon)", true)]
        static bool StyleMidMoonCheck() { Menu.SetChecked("Space Between Us/Island Style/Low-poly (Mid Moon)", Style == IslandStyle.MidMoon); return true; }

        /// <summary>
        /// Floating-rock island: a flat, jagged rock cap whose top sits just under y = 0 (slots, lake and life stay on top),
        /// and a tapering spire underneath that ends in a point. One variant per island (Island_1/2/3).
        /// </summary>
        static void BuildIslandRock(GameObject root)
        {
            var rocks = Load($"{Asteroids}/Prefabs", "Asteroid_1", "Asteroid_2", "Asteroid_3", "Asteroid_4", "Asteroid_5");
            var cliff = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ValleyGrayCliff/ValleyGrayCliff.prefab");
            var midMoon = Load(MidMoon, "Island 1", "Island 2", "Island 1");
            var style = Style;
            if (style == IslandStyle.CliffSpire && !cliff) style = IslandStyle.RockSpire;
            if (style == IslandStyle.RockSpire && rocks.Length == 0) style = IslandStyle.MidMoon;

            // ONE stone material for the whole island (cap, spire and drifting rocks), so the top and the
            // underside read as the same piece of rock. The 5 asteroid models each ship a different texture/colour.
            Material stone = null;
            if (style == IslandStyle.CliffSpire) stone = cliff.GetComponentInChildren<Renderer>(true)?.sharedMaterial;
            else if (style == IslandStyle.RockSpire)
                stone = AssetDatabase.LoadAssetAtPath<Material>($"{Asteroids}/Materials/{IslandStoneMaterial}.mat")
                        ?? rocks[0].GetComponentInChildren<Renderer>(true)?.sharedMaterial;

            var group = Group(root.transform, "Art_Island");
            var variants = new List<GameObject>();
            for (int vi = 0; vi < 3; vi++)
            {
                var v = Group(group, $"Art_Variant_{vi}").gameObject;
                var rnd = new System.Random(17 + vi * 31);
                float Yaw() => (float)rnd.NextDouble() * 360f;
                GameObject Rock() => rocks[rnd.Next(rocks.Length)];
                Vector2 Jitter(float r) => new((float)(rnd.NextDouble() - 0.5) * r, (float)(rnd.NextDouble() - 0.5) * r);

                if (style == IslandStyle.MidMoon)
                {
                    if (midMoon.Length == 0) break;
                    var inst = Spawn(midMoon[vi % midMoon.Length], v.transform, midMoon[vi % midMoon.Length].name);
                    inst.transform.localRotation = Quaternion.Euler(0f, vi * 140f, 0f);
                    ScaleUniform(inst, v.transform, 1f);
                    var b = MeshBounds(inst);
                    ScaleUniform(inst, v.transform, 0.8f / Mathf.Max(b.size.x, b.size.z));
                    b = MeshBounds(inst);
                    float top = PlateauHeight(inst, b);
                    inst.transform.position += new Vector3(-b.center.x, -0.03f - top, -b.center.z);
                    Shadows(inst, true);
                }
                else
                {
                    // cap: several overlapping rock slabs of different size -> flat-ish top with a jagged,
                    // irregular outline (a single squashed rock reads as a round disc). Thick enough to look like a cliff edge.
                    Piece(v.transform, Rock(), new Vector3(0.62f, 0.24f, 0.5f), -0.006f, Jitter(0.06f), Yaw(), false);
                    for (int k = 0; k < 4; k++)
                    {
                        float a = (k * 90f + (float)rnd.NextDouble() * 50f) * Mathf.Deg2Rad;
                        float w = 0.3f + (float)rnd.NextDouble() * 0.16f;
                        float d = 0.17f + (float)rnd.NextDouble() * 0.07f;
                        Piece(v.transform, Rock(), new Vector3(w, 0.18f + (float)rnd.NextDouble() * 0.1f, w * 0.75f),
                            -0.008f - (float)rnd.NextDouble() * 0.02f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d, Yaw(), k % 2 == 1);
                    }
                    if (style == IslandStyle.CliffSpire)
                    {
                        // the cliff turned upside down = a long ridged spire
                        Piece(v.transform, cliff, new Vector3(0.7f, 0.85f + vi * 0.1f, 0.7f), -0.07f, Jitter(0.06f), Yaw(), true);
                    }
                    else
                    {
                        // stacked, shrinking rocks = jagged inverted cone
                        float h = 0.9f + vi * 0.12f;
                        Piece(v.transform, Rock(), new Vector3(0.70f, h * 0.36f, 0.70f), -0.06f, Jitter(0.04f), Yaw(), true);
                        Piece(v.transform, Rock(), new Vector3(0.46f, h * 0.36f, 0.46f), -0.06f - h * 0.26f, Jitter(0.08f), Yaw(), true);
                        Piece(v.transform, Rock(), new Vector3(0.26f, h * 0.30f, 0.26f), -0.06f - h * 0.52f, Jitter(0.1f), Yaw(), true);
                        Piece(v.transform, Rock(), new Vector3(0.11f, h * 0.22f, 0.11f), -0.06f - h * 0.74f, Jitter(0.08f), Yaw(), true);
                    }
                    // a couple of small rocks drifting beside the island
                    if (rocks.Length > 0)
                        for (int k = 0; k < 2; k++)
                        {
                            float a = Yaw() * Mathf.Deg2Rad;
                            float sz = 0.06f + (float)rnd.NextDouble() * 0.06f;
                            Piece(v.transform, Rock(), new Vector3(sz, sz * 1.6f, sz), -0.25f - (float)rnd.NextDouble() * 0.3f,
                                new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.55f + (float)rnd.NextDouble() * 0.15f), Yaw(), true);
                        }
                }
                if (stone)
                    foreach (var r in v.GetComponentsInChildren<Renderer>(true))
                        r.sharedMaterials = Enumerable.Repeat(stone, r.sharedMaterials.Length).ToArray();
                variants.Add(v);
            }
            if (variants.Count == 0) { Object.DestroyImmediate(group.gameObject); return; }
            AddVariant(group.gameObject, root.transform, variants);
            HideNamed(root.transform, "Body", "Underside", "Underside2", "Soil");
            Report.Add($"Island body -> {style}, all rock in {(stone ? stone.name : "original materials")}");
        }

        /// <summary>A model stretched to the given size, its top at topY, centred at xz, optionally upside down.</summary>
        static GameObject Piece(Transform parent, GameObject model, Vector3 size, float topY, Vector2 xz, float yaw, bool flip)
        {
            var inst = Spawn(model, parent, model.name);
            var p = parent.lossyScale;
            inst.transform.localScale = new Vector3(1f / p.x, 1f / p.y, 1f / p.z);
            var b = MeshBounds(inst);   // world size of the model at scale 1
            inst.transform.localScale = new Vector3(size.x / Mathf.Max(b.size.x, 1e-5f) / p.x,
                                                    size.y / Mathf.Max(b.size.y, 1e-5f) / p.y,
                                                    size.z / Mathf.Max(b.size.z, 1e-5f) / p.z);
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(flip ? 180f : 0f, 0f, 0f);
            b = MeshBounds(inst);
            var target = parent.TransformPoint(new Vector3(xz.x, topY, xz.y));
            inst.transform.position += new Vector3(target.x - b.center.x, target.y - b.max.y, target.z - b.center.z);
            Shadows(inst, true);
            return inst;
        }

        // ------------------------------------------------------------------ water + reveal threads

        public static Material StylizedWaterMaterial()
        {
            string path = $"{MatDir}/Water_Stylized.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            var shader = Shader.Find("SpaceBetweenUs/StylizedWater");
            if (!shader) return null;
            mat = new Material(shader) { renderQueue = (int)RenderQueue.Transparent };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        public static Material LightThreadMaterial()
        {
            string path = $"{MatDir}/LightThread_Soft.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            var shader = Shader.Find("SpaceBetweenUs/LightThread");
            if (!shader) return null;
            mat = new Material(shader) { renderQueue = (int)RenderQueue.Transparent };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Generated lotus (blooms when it appears) on three notched lily pads, floating on the lake.</summary>
        static void DressLotus(GameObject root)
        {
            var flower = Find(root.transform, "LotusFlower");
            var pad = Find(root.transform, "LotusPad");
            float waterTop = 0.017f;   // the full lake's surface height on the island
            var floraFlower = Load(Flora, "lotus_1_Prefap").FirstOrDefault();
            var floraLeaves = Load(Flora, "Lotus_leaves_Prefap").FirstOrDefault();
            if (UseFloraLotus && floraFlower && flower && pad)
            {
                Plant(flower, floraFlower, 0.05f, 30f, waterTop - 0.003f, fitWidth: 0.075f);
                Hide(flower);
                if (floraLeaves)
                {
                    Plant(pad, floraLeaves, 0.02f, 200f, waterTop - 0.001f, fitWidth: 0.16f);
                    Hide(pad);
                }
                Report.Add("Lotus -> Flora lotus_1 + lotus leaves");
                return;
            }
            if (flower)
            {
                var art = LotusGenerator.BuildFlower(flower, flower.gameObject.layer).transform;
                Unscale(art, flower);
                art.position = new Vector3(flower.position.x, waterTop - 0.001f, flower.position.z);
                Hide(flower);
            }
            if (pad)
            {
                var art = LotusGenerator.BuildPads(pad, pad.gameObject.layer).transform;
                Unscale(art, pad);
                art.position = new Vector3(pad.position.x, waterTop + 0.0015f, pad.position.z);
                Hide(pad);
            }
            Report.Add("Lotus -> generated flower (blooms open) + lily pads");
        }

        /// <summary>
        /// Puts a plant model on a grey-box object, whatever that object's (non-uniform) scale: a plain-metre holder
        /// under it, the model fitted to the given height (or footprint width if fitWidth is set), standing on bottomY,
        /// centred on the object, turned by yaw.
        /// </summary>
        static GameObject Plant(Transform target, GameObject model, float height, float yaw, float bottomY, float fitWidth = 0f)
        {
            Vector3 c = OwnBounds(target).center;
            var holder = new GameObject("Art_Plant").transform;
            holder.gameObject.layer = target.gameObject.layer;
            holder.SetParent(target, false);
            Unscale(holder, target);
            holder.localPosition = Vector3.zero;
            var bounds = fitWidth > 0f
                ? new Bounds(new Vector3(c.x, bottomY + height * 0.5f, c.z), new Vector3(fitWidth, height, fitWidth))
                : new Bounds(new Vector3(c.x, bottomY + height * 0.5f, c.z), new Vector3(height, height, height));
            var inst = Dress(holder, model, bounds, fitWidth > 0f ? Fit.Xz : Fit.Height, Align.Bottom, null, 1f);
            var b = MeshBounds(inst);
            // flat models (big leaves, flower carpets) fitted by height blow up sideways: never wider than 2.5x the height
            float wide = Mathf.Max(b.size.x, b.size.z), maxWide = fitWidth > 0f ? fitWidth * 1.05f : height * 2.5f;
            if (wide > maxWide)
            {
                inst.transform.localScale *= maxWide / wide;
                var nb = MeshBounds(inst);
                inst.transform.position += new Vector3(b.center.x - nb.center.x, b.min.y - nb.min.y, b.center.z - nb.center.z);
                b = MeshBounds(inst);
            }
            inst.transform.RotateAround(new Vector3(b.center.x, 0f, b.center.z), Vector3.up, yaw);
            return inst;
        }

        /// <summary>Plant, moved sideways by offset (island xz, metres) from the target's centre.</summary>
        static GameObject PlantAt(Transform target, GameObject model, float height, float yaw, float bottomY, Vector2 offset, float fitWidth = 0f)
        {
            if (!target || !model) return null;
            var inst = Plant(target, model, height, yaw, bottomY, fitWidth);
            inst.transform.position += new Vector3(offset.x, 0f, offset.y);
            return inst;
        }

        static Vector2 Random2(System.Func<float, float, float> rand, float r)
        {
            float a = rand(0f, Mathf.PI * 2f), d = Mathf.Sqrt(rand(0f, 1f)) * r;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
        }

        /// <summary>Keeps lawn plants on the island top and out of the lake (island root at the origin while editing the prefab).</summary>
        static bool OnIslandTop(Vector3 centre, Vector2 off)
        {
            var p = new Vector2(centre.x + off.x, centre.z + off.y);
            bool stream = p.x > 0.17f && Mathf.Abs(p.y - 0.06f) < 0.07f;   // where the stream runs to the edge
            return p.magnitude < 0.29f && (p - new Vector2(LakeX, LakeZ)).magnitude > 0.15f && !stream;
        }

        /// <summary>Cancels the parent's (non-uniform) scale so the child lives in plain metres.</summary>
        static void Unscale(Transform child, Transform parent)
        {
            var p = parent.lossyScale;
            child.localRotation = Quaternion.identity;
            child.localScale = new Vector3(1f / p.x, 1f / p.y, 1f / p.z);
        }

        /// <summary>The lake surface: deep-to-shallow blue, drifting ripples, starlight glints, foam at the shore.</summary>
        static void DressWater(GameObject root)
        {
            var water = Find(root.transform, "Water");
            var mat = StylizedWaterMaterial();
            if (!water || !mat || !water.TryGetComponent<Renderer>(out var r)) return;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            Report.Add("Lake water -> SpaceBetweenUs/StylizedWater (Water_Stylized.mat)");
        }

        // ------------------------------------------------------------------ stream + waterfall
        const string StreamDir = "Assets/_Project/Art/Stream";

        /// <summary>
        /// A stream that leaves the lake, winds across the island top and spills over the edge as a waterfall
        /// falling into space. One per island-rock variant (the edge is different on each), built on that variant's
        /// rock surface. Runs only once the lake is full (StreamReveal).
        /// </summary>
        static void BuildStream(GameObject root)
        {
            var water = Find(root.transform, "Water");
            var art = Find(root.transform, "Art_Island");
            var shader = Shader.Find("SpaceBetweenUs/FlowingWater");
            if (!water || !art) return;
            if (!shader) { Report.Add("FlowingWater shader not imported yet - run again for the stream"); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Water_Stream.mat");
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, $"{MatDir}/Water_Stream.mat"); }
            mat.shader = shader;
            mat.renderQueue = (int)RenderQueue.Transparent + 10;
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art")) AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder(StreamDir)) AssetDatabase.CreateFolder("Assets/_Project/Art", "Stream");

            for (int vi = 0; vi < art.childCount; vi++)
            {
                var variant = art.GetChild(vi);
                if (!variant.name.StartsWith("Art_Variant")) continue;
                // rock surface points of this variant (world = island space while editing the prefab)
                var pts = new List<Vector3>();
                foreach (var mf in variant.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.sharedMesh) continue;
                    var m = mf.transform.localToWorldMatrix;
                    // works for meshes without Read/Write in the editor
                    using var data = Mesh.AcquireReadOnlyMeshData(mf.sharedMesh);
                    var verts = new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount, Unity.Collections.Allocator.Temp);
                    data[0].GetVertices(verts);
                    foreach (var v in verts)
                    {
                        var w = m.MultiplyPoint3x4(v);
                        if (w.y > -0.15f) pts.Add(w);
                    }
                    verts.Dispose();
                }
                if (pts.Count < 20) { Report.Add("(stream skipped: no island rock found)"); continue; }

                float TopY(Vector2 p)
                {
                    float best = float.NegativeInfinity;
                    foreach (var w in pts)
                        if ((new Vector2(w.x, w.z) - p).sqrMagnitude < 0.035f * 0.035f && w.y > best) best = w.y;
                    return float.IsNegativeInfinity(best) ? -0.02f : best;
                }
                float Rim(float ang)
                {
                    var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    float best = 0.25f;
                    foreach (var w in pts)
                    {
                        if (w.y < -0.06f) continue;
                        var q = new Vector2(w.x, w.z);
                        float along = Vector2.Dot(q, d);
                        if (along > best && Mathf.Abs(d.x * q.y - d.y * q.x) < 0.03f) best = along;
                    }
                    return best;
                }

                // path: out of the lake's side, a gentle S-bend, over the rim
                var lake = new Vector2(LakeX, LakeZ);
                float a0 = Mathf.Atan2(LakeZ, LakeX);
                float outAng = a0 - 0.55f;
                var S = lake + new Vector2(Mathf.Cos(a0 - 0.9f), Mathf.Sin(a0 - 0.9f)) * (LakeR - 0.015f);
                var dirOut = new Vector2(Mathf.Cos(outAng), Mathf.Sin(outAng));
                var E = dirOut * (Rim(outAng) - 0.012f);
                var perp = new Vector2(-dirOut.y, dirOut.x);
                var C1 = S + new Vector2(Mathf.Cos(a0 - 0.9f), Mathf.Sin(a0 - 0.9f)) * 0.07f + perp * 0.02f;
                var C2 = E - dirOut * 0.07f - perp * 0.03f;

                const int N = 26;
                var path = new Vector3[N];
                for (int i = 0; i < N; i++)
                {
                    float t = i / (N - 1f), u = 1f - t;
                    var q = u * u * u * S + 3f * u * u * t * C1 + 3f * u * t * t * C2 + t * t * t * E;
                    path[i] = new Vector3(q.x, TopY(q) + 0.004f, q.y);
                }
                path[0].y = Mathf.Min(path[0].y, 0.012f);   // starts under the lake surface
                for (int pass = 0; pass < 3; pass++)          // smooth the bed, never sink into the rock
                    for (int i = 1; i < N - 1; i++)
                        path[i].y = Mathf.Max((path[i - 1].y + path[i].y * 2f + path[i + 1].y) * 0.25f,
                                              TopY(new Vector2(path[i].x, path[i].z)) + 0.002f);

                var mesh = StreamMesh(path, new Vector3(dirOut.x, 0f, dirOut.y));
                string meshPath = $"{StreamDir}/Stream_{vi}.asset";
                var stored = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (stored) { EditorUtility.CopySerialized(mesh, stored); mesh = stored; }
                else AssetDatabase.CreateAsset(mesh, meshPath);

                var go = new GameObject("Art_Stream");
                go.layer = root.layer;
                go.transform.SetParent(variant, true);
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                var ls = variant.lossyScale;
                go.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                var reveal = go.AddComponent<StreamReveal>();
                reveal.water = water;
                reveal.fullScale = water.localScale.x;
            }
            Report.Add("Lake -> stream winding over the island edge + waterfall into space (Water_Stream.mat)");
        }

        /// <summary>Ribbon along the stream bed, then a parabola falling off the rim, fading out below.</summary>
        static Mesh StreamMesh(Vector3[] bed, Vector3 outward)
        {
            var centre = new List<Vector3>(bed);
            var lip = bed[bed.Length - 1];
            const int F = 18;
            const float drop = 0.6f, reach = 0.07f;
            for (int j = 1; j <= F; j++)
            {
                float t = j / (float)F;
                centre.Add(lip + outward * (reach * Mathf.Sqrt(t)) + Vector3.down * (drop * t * t + 0.02f * t));
            }

            int n = centre.Count, nb = bed.Length;
            var verts = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>();
            var cols = new List<Color>(); var nrm = new List<Vector3>(); var tris = new List<int>();
            float dist = 0f, total = 0f;
            for (int i = 1; i < n; i++) total += Vector3.Distance(centre[i - 1], centre[i]);
            for (int i = 0; i < n; i++)
            {
                if (i > 0) dist += Vector3.Distance(centre[i - 1], centre[i]);
                var fwd = (centre[Mathf.Min(i + 1, n - 1)] - centre[Mathf.Max(i - 1, 0)]);
                var side = Vector3.Cross(Vector3.up, new Vector3(fwd.x, 0f, fwd.z)).normalized;
                if (side.sqrMagnitude < 0.5f) side = Vector3.Cross(Vector3.up, outward).normalized;
                float fall = i < nb ? 0f : (i - nb + 1f) / F;
                float tBed = i < nb ? i / (nb - 1f) : 1f;
                float width = i < nb ? Mathf.Lerp(0.05f, 0.036f, tBed) : Mathf.Lerp(0.036f, 0.07f, fall);
                float alpha = (i < nb ? Mathf.SmoothStep(0f, 1f, tBed / 0.12f) : 1f) * (1f - Mathf.SmoothStep(0.45f, 1f, fall));
                var up = i < nb ? Vector3.up : outward;
                for (int s = 0; s < 2; s++)
                {
                    verts.Add(centre[i] + side * (s == 0 ? -0.5f : 0.5f) * width);
                    uv.Add(new Vector2(s, dist));
                    uv2.Add(new Vector2(dist / total, 0f));
                    cols.Add(new Color(fall > 0f ? Mathf.Clamp01(0.4f + fall) : 0f, 0f, 0f, alpha));
                    nrm.Add(up);
                }
                if (i > 0)
                {
                    int a = (i - 1) * 2;
                    tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }
            var mesh = new Mesh { name = "Stream" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv2); mesh.SetColors(cols);
            mesh.SetNormals(nrm); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Twinkling stars + shooting stars, and the SoundDirector with the generated placeholder sounds.</summary>
        static void DressSkyAndSoundInScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != PrototypeSceneBuilder.ScenePath) { Report.Add("(open Prototype_Core and run again for twinkling stars + sound)"); return; }

            // ---- stars
            var starsGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Stars");
            var twinkle = Shader.Find("SpaceBetweenUs/TwinkleStar");
            if (starsGo && twinkle && starsGo.TryGetComponent<ParticleSystem>(out var ps))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Stars_Twinkle.mat");
                if (!mat) { mat = new Material(twinkle); AssetDatabase.CreateAsset(mat, $"{MatDir}/Stars_Twinkle.mat"); }
                mat.shader = twinkle;
                mat.renderQueue = (int)RenderQueue.Transparent - 50;   // behind everything else that is transparent
                var pr = starsGo.GetComponent<ParticleSystemRenderer>();
                pr.sharedMaterial = mat;
                pr.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                    { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.StableRandomX });
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.93f, 0.8f));   // cool and warm stars

                var thread = Shader.Find("SpaceBetweenUs/LightThread");
                var shooting = starsGo.GetComponent<ShootingStars>();
                if (!shooting) shooting = starsGo.AddComponent<ShootingStars>();
                if (thread)
                {
                    var tm = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/ShootingStar.mat");
                    if (!tm) { tm = new Material(thread); AssetDatabase.CreateAsset(tm, $"{MatDir}/ShootingStar.mat"); }
                    tm.shader = thread;
                    tm.SetFloat("_Beads", 0f);
                    tm.SetFloat("_CoreWidth", 0.25f);
                    tm.SetFloat("_GlowWidth", 0.8f);
                    tm.SetColor("_Color", new Color(1.6f, 1.6f, 1.8f));
                    EditorUtility.SetDirty(tm);
                    shooting.trailMaterial = tm;
                }
                EditorUtility.SetDirty(mat);
                EditorUtility.SetDirty(starsGo);
                Report.Add("Stars -> twinkling (TwinkleStar shader) + a shooting star every 8-15 s");
            }
            else if (!twinkle) Report.Add("TwinkleStar shader not imported yet - run again for twinkling stars");

            // ---- little moving life on the islands: fish swim, frog hops, butterflies + dragonflies (Critters)
            var critterShader = Shader.Find("SpaceBetweenUs/CritterWing");
            if (critterShader)
            {
                var wing = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/CritterWing.mat");
                if (!wing) { wing = new Material(critterShader); AssetDatabase.CreateAsset(wing, $"{MatDir}/CritterWing.mat"); }
                int n = 0;
                foreach (var island in Object.FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var cr = island.GetComponent<Critters>();
                    if (!cr) cr = island.gameObject.AddComponent<Critters>();
                    cr.wingMaterial = wing;
                    EditorUtility.SetDirty(cr);
                    n++;
                }
                Report.Add($"Critters on {n} island(s): swimming fish, hopping frog, butterflies, dragonflies");
            }
            else Report.Add("CritterWing shader not imported yet - run again for butterflies / dragonflies");

            // ---- sound
            var clips = SoundSynth.EnsureClips(false);
            AudioClip C(string n) => clips.TryGetValue(n, out var c) ? c : null;
            var audioGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Audio") ?? new GameObject("Audio");
            var sd = audioGo.GetComponent<SoundDirector>();
            if (!sd) sd = audioGo.AddComponent<SoundDirector>();
            sd.ambBuilder = C("amb_builder"); sd.ambDestroyer = C("amb_destroyer");
            sd.punch = C("punch"); sd.tap = C("tap"); sd.violent = C("violent"); sd.islandRuin = C("island_ruin");
            sd.attractLoop = C("attract_loop"); sd.grab = C("grab"); sd.placeRock = C("place_rock");
            sd.pourLoop = C("pour_loop"); sd.waterFill = C("water_fill"); sd.crystal = C("crystal");
            sd.grow = C("grow"); sd.islandAlive = C("island_alive");
            sd.switchWhoosh = C("switch_whoosh"); sd.reveal = C("reveal");
            EditorUtility.SetDirty(sd);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Report.Add($"Sound -> SoundDirector with {clips.Count} placeholder clips (Assets/_Project/Audio)");
        }

        /// <summary>Hint text: lazy-follow panel drawn on top (was head-locked and flickered behind objects).</summary>
        static void DressHintsInScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != PrototypeSceneBuilder.ScenePath) return;
            var go = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(c => c.gameObject).FirstOrDefault(g => g.name == "HintCanvas");
            if (!go) return;
            var f = go.GetComponent<HudFollow>(); if (!f) f = go.AddComponent<HudFollow>();
            f.head = go.transform.parent;
            EditorUtility.SetDirty(f);

            // Canvas text flickers in the headset / XR Simulator -> 3D TextMesh with an always-on-top shader
            var hints = go.GetComponentInChildren<FlowHints>(true);
            var shader = Shader.Find("SpaceBetweenUs/HintText");
            if (hints && shader)
            {
                var old = go.transform.Find("Art_HintText");
                if (old) Object.DestroyImmediate(old.gameObject);
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/HintText.mat");
                if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, $"{MatDir}/HintText.mat"); }
                mat.shader = shader;
                mat.mainTexture = font.material.mainTexture;
                mat.renderQueue = 4000;
                EditorUtility.SetDirty(mat);

                var t = new GameObject("Art_HintText");
                t.layer = go.layer;
                t.transform.SetParent(go.transform, false);
                t.transform.localPosition = new Vector3(0f, -160f, -1f);   // bottom of the old 900x320 panel
                var tm = t.AddComponent<TextMesh>();
                tm.font = font;
                tm.fontSize = 48;
                tm.richText = true;
                tm.anchor = TextAnchor.LowerCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(1f, 1f, 1f, 0.92f);
                tm.text = "Hg";
                var mr = t.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                float h = mr.localBounds.size.y, ls = t.transform.lossyScale.y;
                if (h > 1e-5f && ls > 1e-7f) tm.characterSize = hints.lineHeight / (h * ls);
                hints.mesh = tm;
                var txt = hints.GetComponent<UnityEngine.UI.Text>();
                if (txt) { txt.enabled = false; EditorUtility.SetDirty(txt); }
                EditorUtility.SetDirty(hints);
            }
            else if (!shader) Report.Add("HintText shader not imported yet - run again for the 3D hint text");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Report.Add("Hint text -> 3D text (no Canvas), lazy-follow, drawn on top");
        }

        // ------------------------------------------------------------------ hands of light
        const string HandModels = "Packages/com.meta.xr.sdk.core/Meshes/HandTracking";

        /// <summary>Replaces the hand spheres with translucent glowing Meta hand meshes (gold builder, blue destroyer).</summary>
        static void DressHandsInScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != PrototypeSceneBuilder.ScenePath) { Report.Add("(open Prototype_Core and run again to add the hands of light)"); return; }
            var gold = GhostHandMaterial("GhostHand_Gold", new Color(1.6f, 1.1f, 0.45f));
            var blue = GhostHandMaterial("GhostHand_Blue", new Color(0.45f, 0.95f, 1.8f));
            if (!gold || !blue) { Report.Add("GhostHand shader not found - let Unity import Shaders/GhostHand.shader, then run again"); return; }
            int n = 0;
            foreach (var name in new[] { "BuilderHand_Left", "BuilderHand_Right", "DestroyerHand_Left", "DestroyerHand_Right" })
            {
                // GameObject.Find skips inactive objects, and the destroyer hands start hidden (until step 4)
                var go = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Select(t => t.gameObject).FirstOrDefault(g => g.name == name && g.scene == scene);
                if (!go) { Report.Add($"(hand '{name}' not in scene)"); continue; }
                bool right = name.EndsWith("Right");
                bool destroyer = name.StartsWith("Destroyer");
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{HandModels}/OculusHand_{(right ? "R" : "L")}.fbx");
                if (!model) { Report.Add("Meta hand models not found in " + HandModels); return; }

                for (int i = go.transform.childCount - 1; i >= 0; i--)
                    if (go.transform.GetChild(i).name.StartsWith("Art")) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
                var visual = go.transform.Find("Visual");
                if (visual && visual.TryGetComponent<Renderer>(out var vr)) vr.enabled = false;

                var hand = (GameObject)Object.Instantiate(model, go.transform);
                hand.name = "Art_Hand";
                foreach (var a in hand.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
                float s = destroyer ? 1.3f : 1f;
                var lossy = go.transform.lossyScale;
                hand.transform.localScale = new Vector3(s / lossy.x, s / lossy.y, s / lossy.z);
                hand.transform.localRotation = Quaternion.LookRotation(right ? Vector3.up : Vector3.down, Vector3.right);
                hand.transform.localPosition = new Vector3(0f, -0.01f, -0.065f * s);
                foreach (var r in hand.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int m = 0; m < mats.Length; m++) mats[m] = destroyer ? blue : gold;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                }
                var pose = hand.AddComponent<HandPose>();
                pose.hand = right ? Hand.Right : Hand.Left;
                pose.destroyer = destroyer;
                n++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Report.Add($"Hands -> {n} hands of light (Meta hand mesh + GhostHand glow, fingers curl with GRIP)");
        }

        static Material GhostHandMaterial(string name, Color color)
        {
            var shader = Shader.Find("SpaceBetweenUs/GhostHand");
            if (!shader) return null;
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Step-6 threads: soft flowing light + halos (scene object, so only when Prototype_Core is open).</summary>
        static void DressRevealInScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != PrototypeSceneBuilder.ScenePath) { Report.Add("(open Prototype_Core and run again to restyle the step-6 light threads)"); return; }
            var reveal = Object.FindAnyObjectByType<RevealThreads>(FindObjectsInactive.Include);
            var thread = LightThreadMaterial();
            if (!reveal || !thread) return;
            reveal.threadMaterial = thread;
            // soft halo-like look (the asset may predate the new shader defaults)
            thread.SetFloat("_CoreWidth", 0.22f);
            thread.SetFloat("_GlowWidth", 0.85f);
            thread.SetFloat("_FlowSpeed", 0.12f);
            thread.SetFloat("_Beads", 3f);
            EditorUtility.SetDirty(thread);
            EditorUtility.SetDirty(reveal);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Report.Add("Step-6 threads -> soft halo-like glowing curves (no end halos)");
        }

        // ------------------------------------------------------------------ soft glowing slot markers

        const string RingTexPath = MatDir + "/Tex_SoftRing.png";

        /// <summary>
        /// The "put it here" markers (gold = rock, cyan = ice, violet = crystal) become soft, slowly breathing halos
        /// floating just above the rock instead of solid discs. They still switch off when the slot is filled
        /// (the halo is a child of the original Glow / HoldRing object).
        /// </summary>
        static void DressMarkers(GameObject root)
        {
            var ring = SoftRingTexture();
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (t.name != "Glow" && t.name != "HoldRing") continue;
                string slot = t.parent ? t.parent.name : "";
                var (matName, color) = slot.Contains("Rock") ? ("Halo_Gold", Hex("#FFC85A", 1.6f))
                                     : slot.Contains("Ice") ? ("Halo_Ice", Hex("#7FDFFF", 1.6f))
                                     : ("Halo_Crystal", Hex("#B98CFF", 1.8f));
                var mat = HaloMaterial(matName, ring, color);

                // world size: a bit wider than the old disc so the soft edge has room to fade
                var b = OwnBounds(t);
                float diameter = Mathf.Max(b.size.x, b.size.z) * (t.name == "HoldRing" ? 1.3f : 1.7f);
                if (t.name == "Glow" && slot.Contains("Crystal")) diameter = 0.12f;   // the lotus seed glow is a small sphere

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.name = "Art_Halo";
                quad.layer = t.gameObject.layer;
                quad.transform.SetParent(t, false);
                quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat, facing up
                var p = t.lossyScale;
                // undo the parent's flat (y) scale; the quad's own axes map to the parent's x / z after the 90 degree turn
                quad.transform.localScale = new Vector3(diameter / p.x, diameter / p.z, 1f / Mathf.Max(p.y, 1e-4f));
                quad.transform.position = new Vector3(b.center.x, b.center.y + 0.01f, b.center.z);
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                var pulse = quad.AddComponent<SoftPulse>();
                pulse.color = color;
                pulse.spin = t.name == "HoldRing" ? -12f : 8f;
                Hide(t);
                n++;
            }
            Report.Add($"Slot markers -> {n} soft breathing halos");
        }

        static Color Hex(string hex, float intensity)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return new Color(c.r * intensity, c.g * intensity, c.b * intensity, 1f);
        }

        static Material HaloMaterial(string name, Texture2D tex, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            // URP Unlit, transparent + additive, double sided, no depth write
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetFloat("_AlphaClip", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>256px white texture whose alpha is a soft ring with a faint glowing centre and a few brighter arcs.</summary>
        static Texture2D SoftRingTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(RingTexPath);
            if (existing) return existing;

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(v, u);
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.62f) / 0.12f, 2f));
                float arcs = 0.75f + 0.25f * Mathf.Sin(ang * 3f);              // so the slow spin is visible
                float inner = 0.22f * Mathf.Exp(-Mathf.Pow(r / 0.5f, 2f));
                float edge = Mathf.Clamp01((1f - r) / 0.12f);                   // fully transparent at the quad edge
                float a = Mathf.Clamp01(ring * arcs + inner) * edge;
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(RingTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(RingTexPath);
            if (AssetImporter.GetAtPath(RingTexPath) is TextureImporter ti)
            {
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(RingTexPath);
        }

        // ------------------------------------------------------------------ per prefab

        static void DressIsland(GameObject root)
        {
            BuildIslandRock(root);
            DressMarkers(root);
            DressWater(root);
            BuildStream(root);
            DressLotus(root);

            var rnd = new System.Random(5);
            float Rand(float lo, float hi) => lo + (float)rnd.NextDouble() * (hi - lo);

            // tree: one kind per island (Flora maple / willow, else the Mid Moon low-poly trees)
            var tree = Find(root.transform, "Tree");
            var trees = Load(Flora, "Maple_1_Prefap", "Willow_Prefap", "Maple_2_Prefap");
            if (trees.Length == 0) trees = Load(MidMoon, "Tree 1", "Tree 2", "Tree 7");
            if (tree && trees.Length > 0)
            {
                var parts = new[] { Find(tree, "Trunk"), Find(tree, "Canopy") };
                var tb = OwnBounds(parts);
                var group = Group(tree, "Art_Tree");
                var variants = trees.Select(t => Dress(group, t, tb, Fit.Height, Align.Bottom, null, 1.2f, t.name)).ToList();
                AddVariant(group.gameObject, root.transform, variants);
                Hide(parts);
                Report.Add($"Tree -> {string.Join(", ", trees.Select(x => x.name))}");
            }

            // extra smaller trees of other kinds, growing with the main one
            if (tree && trees.Length > 0)
            {
                var trunk = Find(tree, "Trunk");
                var others = Load(Flora, "Willow_Prefap", "Maple_2_Prefap", "Maple_1_Prefap", "Lilac_2_2_Prefap");
                Vector2[] spots = { new(-0.05f, 0.25f), new(0.33f, -0.05f) };
                float[] heights = { 0.24f, 0.18f };
                for (int k = 0; k < spots.Length && others.Length > 0; k++)
                    PlantAt(trunk, others[(k + 1) % others.Length], heights[k], Rand(0f, 360f), -0.008f, spots[k]);
                Report.Add("Tree +2 smaller trees");
            }

            // bushes, each with a few companions (ferns, mushrooms, pebbles, big leaves)
            var bushes = Load(Flora, "Bush_1_Prefap", "Lilac_1_2_Prefap", "Fern_1_Prefap");
            float[] bushHeight = { 0.08f, 0.075f, 0.055f };
            if (bushes.Length < 3) { bushes = Load(MidMoon, "Mushroom 1", "Mushroom 2", "Pine .001"); bushHeight = new[] { 0.07f, 0.07f, 0.12f }; }
            var companions = Load(Flora, "Fern_2_Prefap", "Mushroom_Red_Prefap", "Fern_1_Prefap", "Mushroom_White_1_Prefap",
                                         "Stones_Min_1_Prefap", "Lilac_2_1_Prefap", "Mushroom_White_2_Prefap", "Stones_Min_3_Prefap", "Fern_2_Prefap");
            int ci = 0;
            for (int i = 0; i < 3 && i < bushes.Length; i++)
            {
                var bush = Find(root.transform, $"Bush_{i}");
                if (!bush) continue;
                float bottom = OwnBounds(bush).min.y;
                Plant(bush, bushes[i], bushHeight[i], Rand(0f, 360f), bottom);
                for (int k = 0; k < 3 && companions.Length > 0; k++, ci++)
                {
                    var m = companions[ci % companions.Length];
                    string n = m.name;
                    float h = n.StartsWith("Mushroom") ? Rand(0.02f, 0.03f) : n.StartsWith("Stones") ? 0.012f : Rand(0.04f, 0.055f);
                    float a = (k / 3f + Rand(-0.08f, 0.08f)) * Mathf.PI * 2f;
                    var off = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Rand(0.045f, 0.07f);
                    PlantAt(bush, m, h, Rand(0f, 360f), bottom, off, n.StartsWith("Stones") ? 0.035f : 0f);
                }
                Hide(bush);
            }
            Report.Add("Bush_0..2 -> " + string.Join(", ", bushes.Select(x => x.name)) + " + ferns, mushrooms, pebbles");

            // moss stage -> a real lawn: each moss spot becomes a cluster of small grass clumps with a few
            // little flowers (single flowers fitted by height, flower carpets fitted by width so they stay small)
            var grass = Load(Flora, "Grass_1_Prefap", "Grass_2_Prefap", "Grass_3_Prefap", "Grass_4_Prefap", "Grass_5_Prefap");
            var flowers = Load(Flora, "Flower_purple_1_Prefap", "Flower_yellow_1_Prefap", "Pink_Flower_1", "Flower_purple_2_Prefap",
                                      "Flower_yellow_2_Prefap", "Pink_Flower_2");
            var carpets = Load(Flora, "Flowers on the ground", "Chamomile", "Mint");
            if (grass.Length > 0)
            {
                int fi = 0;
                for (int i = 0; i < 8; i++)
                {
                    var moss = Find(root.transform, $"Moss_{i}");
                    if (!moss) continue;
                    for (int k = 0; k < 6; k++)
                    {
                        var off = Random2(Rand, 0.065f);
                        if (!OnIslandTop(OwnBounds(moss).center, off)) continue;
                        PlantAt(moss, grass[(i + k) % grass.Length], Rand(0.025f, 0.042f), Rand(0f, 360f), -0.01f, off);
                    }
                    if (flowers.Length > 0 && i % 2 == 0)
                        for (int k = 0; k < 2; k++)
                        {
                            var off = Random2(Rand, 0.05f);
                            if (OnIslandTop(OwnBounds(moss).center, off))
                                PlantAt(moss, flowers[fi++ % flowers.Length], Rand(0.025f, 0.035f), Rand(0f, 360f), -0.008f, off);
                        }
                    if (carpets.Length > 0 && i % 3 == 1)
                        PlantAt(moss, carpets[(i / 3) % carpets.Length], 0.015f, Rand(0f, 360f), -0.008f, Random2(Rand, 0.03f), 0.05f);
                    Hide(moss);
                }
                Report.Add("Moss_0..7 -> lawn: grass clusters + small flowers");
            }

            // reeds around the lake
            var reeds = Load(Flora, "Reed_1_Prefap", "Reed_2_Prefap");
            if (reeds.Length > 0)
            {
                for (int i = 0; i < 7; i++)
                {
                    var reed = Find(root.transform, $"Reed_{i}");
                    if (!reed) continue;
                    Plant(reed, reeds[i % reeds.Length], Rand(0.07f, 0.1f), Rand(0f, 360f), OwnBounds(reed).min.y);
                    Hide(reed);
                }
                Report.Add("Reed_0..6 -> Flora reeds");
            }

            // fish: swim flat on the water, facing along the circle
            var fish = Load(Fish, "fish01", "fish02", "fish03");
            for (int i = 0; i < 3 && fish.Length > 0; i++)
            {
                var f = Find(root.transform, $"Fish_{i}");
                if (!f) continue;
                Vector3 c = OwnBounds(f).center;
                float yaw = i / 3f * 360f;
                f.localRotation = Quaternion.Euler(0f, -yaw, 0f);
                f.localScale = Vector3.one;
                Dress(f, fish[i % fish.Length], new Bounds(c, Vector3.one * 0.045f), Fit.Max, Align.Center, null, 1f);
                Hide(f);
            }
            if (fish.Length > 0) Report.Add("Fish_0..2 -> Floreswa fish01/02/03");
        }

        static void DressAsteroid(GameObject root)
        {
            var rocks = Load($"{Asteroids}/Prefabs", "Asteroid_1", "Asteroid_2", "Asteroid_3", "Asteroid_4", "Asteroid_5");
            if (rocks.Length > 0)
            {
                // shell: one asteroid model per asteroid instance
                var shell = Find(root.transform, "Shell");
                var parts = shell.Cast<Transform>().Where(t => t.name == "Rock" || t.name.StartsWith("Lump_")).ToArray();
                var tb = OwnBounds(parts);
                var group = Group(shell, "Art_Shell");
                var variants = rocks.Select(r => Dress(group, r, tb, Fit.Max, Align.Center, null, 0.95f, r.name)).ToList();
                AddVariant(group.gameObject, root.transform, variants);
                Hide(parts);

                // shell bits around the ice + the debris after it breaks
                int k = 0;
                foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (!t.name.StartsWith("ShellBit_") && !t.name.StartsWith("Chunk_")) continue;
                    Dress(t, rocks[k++ % rocks.Length], OwnBounds(t), Fit.Max, Align.Center, null, 1.2f);
                    Hide(t);
                }
                Report.Add($"Asteroid shell / shell bits / debris -> Asteroids pack ({rocks.Length} models)");
            }

            var crystal = Load(Crystals, "Crystalsv03").FirstOrDefault();
            var core = Find(root.transform, "Core");
            var coreCrystal = core ? Find(core, "Crystal") : null;
            if (crystal && coreCrystal)
            {
                Dress(core, crystal, OwnBounds(coreCrystal), Fit.Max, Align.Center, Mat("Crystal"), 1.4f, "Art_Core");
                Hide(coreCrystal);
                Report.Add("Asteroid core -> SineVFX Crystalsv03 (violet Crystal material)");
            }
        }

        static void DressRockFragment(GameObject root)
        {
            var rock = Load(MidMoon, "ROCK 1").FirstOrDefault();
            if (!rock) return;
            var parts = new[] { root.transform, Find(root.transform, "Lump"), Find(root.transform, "Lump2") };
            Dress(root.transform, rock, OwnBounds(parts), Fit.Max, Align.Center, null, 1f);
            Hide(parts);
            Report.Add("Fragment_Rock -> Mid Moon ROCK 1");
        }

        /// <summary>Ice: an irregular lump (an Asteroids pack shape) in our translucent pale-cyan Ice material = a frosted chunk.</summary>
        static void DressIceFragment(GameObject root)
        {
            var lump = Load($"{Asteroids}/Prefabs", IceShape).FirstOrDefault();
            var ice = Mat("Ice");
            if (!lump || !ice) return;
            Dress(root.transform, lump, OwnBounds(root.transform), Fit.Max, Align.Center, ice, 1.25f);
            Hide(root.transform);
            AddFrost(root.transform);
            Report.Add($"Fragment_Ice -> {IceShape} shape in the Ice material + drifting snowflake sparkles");
        }

        const string SnowTexPath = MatDir + "/Tex_Snowflake.png";

        /// <summary>A few tiny glinting snowflakes drifting around the ice; they thin out as it melts (ScaleLinkedEmission).</summary>
        static void AddFrost(Transform root)
        {
            float p = Mathf.Max(root.lossyScale.x, 1e-4f);   // sizes below are in metres, the system scales with the fragment
            var go = new GameObject("Art_Frost");
            go.transform.SetParent(root, false);
            go.layer = root.gameObject.layer;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);   // slow drift either way the scale is applied
            main.startSize = new ParticleSystem.MinMaxCurve(0.010f / p, 0.020f / p);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.85f, 0.97f, 1f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // flakes linger behind a moving chunk
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 24;

            var emission = ps.emission;
            emission.rateOverTime = 7f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f / p;
            shape.radiusThickness = 0.3f;   // from the surface, not from inside the ice

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

            // fade in, glint, fade out
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.6f)));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = SnowMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;

            go.AddComponent<ScaleLinkedEmission>();
        }

        static Material SnowMaterial()
        {
            string path = $"{MatDir}/P_Snowflake.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);   // additive: glints of light
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetTexture("_BaseMap", SnowflakeTexture());
            mat.SetColor("_BaseColor", new Color(1.3f, 1.5f, 1.7f, 1f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>128px six-armed snowflake (alpha) with side branches and a soft glow, generated once.</summary>
        static Texture2D SnowflakeTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(SnowTexPath);
            if (existing) return existing;

            const int size = 128;
            var segs = new List<(Vector2 a, Vector2 b, float w)>();
            for (int i = 0; i < 6; i++)
            {
                float ang = i * Mathf.PI / 3f + Mathf.PI / 2f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                segs.Add((Vector2.zero, dir * 0.82f, 0.045f));                       // main arm
                foreach (var (at, len) in new[] { (0.38f, 0.22f), (0.6f, 0.15f) })   // side branches
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float bAng = ang + side * Mathf.PI / 3f;
                        var start = dir * at;
                        segs.Add((start, start + new Vector2(Mathf.Cos(bAng), Mathf.Sin(bAng)) * len, 0.035f));
                    }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var q = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float d = 9f;
                foreach (var (a, b, w) in segs)
                {
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                    d = Mathf.Min(d, (q - (a + ab * t)).magnitude - w);
                }
                float line = Mathf.Clamp01(1f - d / 0.03f);                // crisp arms, slightly soft edge
                float glow = 0.35f * Mathf.Exp(-q.sqrMagnitude / 0.08f);   // bright core
                float a01 = Mathf.Clamp01(line + glow) * Mathf.Clamp01((1f - q.magnitude) / 0.1f);
                px[y * size + x] = new Color(1f, 1f, 1f, a01);
            }
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(SnowTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SnowTexPath);
            if (AssetImporter.GetAtPath(SnowTexPath) is TextureImporter ti)
            {
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SnowTexPath);
        }

        static void DressCrystalFragment(GameObject root)
        {
            var crystal = Load(Crystals, "Crystalsv01").FirstOrDefault();
            if (!crystal) return;
            var parts = new[] { root.transform, Find(root.transform, "Shard") };
            Dress(root.transform, crystal, OwnBounds(parts), Fit.Max, Align.Center, Mat("Crystal"), 1.1f);
            Hide(parts);
            Report.Add("Fragment_Crystal -> SineVFX Crystalsv01 (violet Crystal material)");
        }

        // ------------------------------------------------------------------ Asteroids pack: Standard -> URP Lit

        static readonly string[] ConvertDirs =
        {
            $"{Asteroids}/Materials", "Assets/ValleyGrayCliff", "Assets/3D_Game_Assets_Flora", "Assets/BigRookGames",
        };

        static void ConvertAsteroidMaterials()
        {
            int n = 0;
            var dirs = ConvertDirs.Where(AssetDatabase.IsValidFolder).ToArray();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", dirs))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat && ToUrpLit(mat)) n++;
                if (path.StartsWith(Asteroids) || path.StartsWith("Assets/ValleyGrayCliff"))
                {
                    if (mat && mat.shader && mat.shader.name == "Universal Render Pipeline/Lit")
                    {
                        // matte stone: the packs ship glossy / slightly metallic values that read as metal under our lights
                        mat.SetFloat("_Metallic", 0f);
                        mat.SetFloat("_Smoothness", mat.GetTexture("_MetallicGlossMap") ? 0.35f : 0.12f);
                        EditorUtility.SetDirty(mat);
                    }
                }
            }
            if (n > 0) Report.Add($"Converted {n} pack materials (Standard / HDRP / missing shader) to URP/Lit - they were pink");
        }

        /// <summary>
        /// Standard, HDRP Lit or missing-shader material -> URP Lit, in place. Reads the saved values straight from the
        /// asset (works even when the original shader isn't in the project), keeps alpha-cutout leaves cut out and
        /// double sided, and caps the textures at 1024 px for Quest.
        /// </summary>
        static bool ToUrpLit(Material m)
        {
            string sh = m.shader ? m.shader.name : "";
            bool convert = sh == "" || sh.StartsWith("Hidden/InternalErrorShader") || sh.StartsWith("Standard") ||
                           sh.StartsWith("Legacy") || sh.StartsWith("HDRP") || sh.StartsWith("HDRenderPipeline") ||
                           (m.shader && !m.shader.isSupported);
            if (!convert) return false;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (!lit) return false;

            // saved properties, whatever shader wrote them
            var texs = new Dictionary<string, (Texture tex, Vector2 scale, Vector2 offset)>();
            var floats = new Dictionary<string, float>();
            var colors = new Dictionary<string, Color>();
            var so = new SerializedObject(m);
            var saved = so.FindProperty("m_SavedProperties");
            var te = saved.FindPropertyRelative("m_TexEnvs");
            for (int i = 0; i < te.arraySize; i++)
            {
                var e = te.GetArrayElementAtIndex(i);
                var v = e.FindPropertyRelative("second");
                texs[e.FindPropertyRelative("first").stringValue] = (v.FindPropertyRelative("m_Texture").objectReferenceValue as Texture,
                    v.FindPropertyRelative("m_Scale").vector2Value, v.FindPropertyRelative("m_Offset").vector2Value);
            }
            var fl = saved.FindPropertyRelative("m_Floats");
            for (int i = 0; i < fl.arraySize; i++)
            {
                var e = fl.GetArrayElementAtIndex(i);
                floats[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").floatValue;
            }
            var co = saved.FindPropertyRelative("m_Colors");
            for (int i = 0; i < co.arraySize; i++)
            {
                var e = co.GetArrayElementAtIndex(i);
                colors[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").colorValue;
            }
            var keywords = new HashSet<string>(m.shaderKeywords);
            var validKw = so.FindProperty("m_ValidKeywords");
            if (validKw != null) for (int i = 0; i < validKw.arraySize; i++) keywords.Add(validKw.GetArrayElementAtIndex(i).stringValue);

            (Texture tex, Vector2 scale, Vector2 offset) T(params string[] names)
            {
                foreach (var n in names) if (texs.TryGetValue(n, out var t) && t.tex) return t;
                return (null, Vector2.one, Vector2.zero);
            }
            float F(float d, params string[] names) { foreach (var n in names) if (floats.TryGetValue(n, out var f)) return f; return d; }
            Color C(Color d, params string[] names) { foreach (var n in names) if (colors.TryGetValue(n, out var c)) return c; return d; }

            var main = T("_BaseColorMap", "_MainTex", "_BaseMap");
            var bump = T("_NormalMap", "_BumpMap");
            var metalMap = T("_MetallicGlossMap");
            var occlusion = T("_OcclusionMap");
            var emissionMap = T("_EmissiveColorMap", "_EmissionMap");
            var color = C(Color.white, "_BaseColor", "_Color");
            var emission = C(Color.black, "_EmissionColor", "_EmissiveColor");
            bool cutout = keywords.Contains("_ALPHATEST_ON") || F(0f, "_AlphaCutoffEnable") > 0.5f || Mathf.Approximately(F(0f, "_Mode"), 1f);
            float cutoff = F(0.5f, "_AlphaCutoff", "_Cutoff");
            bool doubleSided = F(0f, "_DoubleSidedEnable") > 0.5f || (floats.TryGetValue("_CullMode", out var cm) && cm < 0.5f) || cutout;
            float smooth = metalMap.tex ? F(1f, "_GlossMapScale") : F(0.3f, "_Smoothness", "_Glossiness");

            m.shader = lit;
            m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", main.tex);
            m.SetTextureScale("_BaseMap", main.scale);
            m.SetTextureOffset("_BaseMap", main.offset);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", F(0f, "_Metallic"));
            m.SetFloat("_Smoothness", Mathf.Min(smooth, 0.6f));
            Keyword(m, "_NORMALMAP", bump.tex);
            m.SetTexture("_BumpMap", bump.tex);
            m.SetFloat("_BumpScale", F(1f, "_NormalScale", "_BumpScale"));
            Keyword(m, "_METALLICSPECGLOSSMAP", metalMap.tex);
            m.SetTexture("_MetallicGlossMap", metalMap.tex);
            Keyword(m, "_OCCLUSIONMAP", occlusion.tex);
            m.SetTexture("_OcclusionMap", occlusion.tex);
            bool emissive = emissionMap.tex || emission.maxColorComponent > 0.01f;
            Keyword(m, "_EMISSION", emissive);
            m.SetTexture("_EmissionMap", emissionMap.tex);
            m.SetColor("_EmissionColor", emission);
            // opaque (cut out where needed)
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            m.SetFloat("_Cutoff", cutoff);
            Keyword(m, "_ALPHATEST_ON", cutout);
            m.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            m.renderQueue = cutout ? (int)RenderQueue.AlphaTest : -1;
            m.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque");

            foreach (var t in new[] { main.tex, bump.tex, metalMap.tex, occlusion.tex, emissionMap.tex }) CapForQuest(t);
            EditorUtility.SetDirty(m);
            return true;
        }

        /// <summary>Android (Quest) override: at most 1024 px. The Editor keeps the full-size texture.</summary>
        static void CapForQuest(Texture t)
        {
            if (!t || AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) is not TextureImporter ti) return;
            var android = ti.GetPlatformTextureSettings("Android");
            if (android.overridden && android.maxTextureSize <= 1024) return;
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.Automatic;
            ti.SetPlatformTextureSettings(android);
            ti.SaveAndReimport();
        }

        static void Keyword(Material m, string k, Object on)
        {
            if (on) m.EnableKeyword(k); else m.DisableKeyword(k);
        }

        static void Keyword(Material m, string k, bool on)
        {
            if (on) m.EnableKeyword(k); else m.DisableKeyword(k);
        }

        // ------------------------------------------------------------------ helpers

        static void Edit(string prefabName, System.Action<GameObject> dress)
        {
            string path = $"{PrefabDir}/{prefabName}.prefab";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
            {
                Report.Add($"(skipped {prefabName}: build the prototype scene first)");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // idempotent: drop what an earlier run added
                foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
                    if (t && t != root.transform && t.name.StartsWith(Art)) Object.DestroyImmediate(t.gameObject);
                dress(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static GameObject[] Load(string dir, params string[] names) => names
            .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{n}.prefab"))
            .Where(g => g != null).ToArray();

        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");

        /// <summary>Adds a model under parent, scaled (uniformly, in world terms) and moved to fill the target bounds.</summary>
        static GameObject Dress(Transform parent, GameObject model, Bounds target, Fit fit, Align align, Material overrideMat,
            float sizeMul, string name = Art)
        {
            var inst = Spawn(model, parent, name.StartsWith(Art) ? name : $"{Art}_{name}");
            if (overrideMat)
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(overrideMat, r.sharedMaterials.Length).ToArray();

            ScaleUniform(inst, parent, 1f);
            var b = MeshBounds(inst);
            float s = fit switch
            {
                Fit.Height => target.size.y / Mathf.Max(b.size.y, 1e-5f),
                Fit.Xz => Mathf.Max(target.size.x, target.size.z) / Mathf.Max(b.size.x, b.size.z, 1e-5f),
                _ => Max(target.size) / Mathf.Max(Max(b.size), 1e-5f),
            } * sizeMul;
            ScaleUniform(inst, parent, s);
            b = MeshBounds(inst);
            inst.transform.position += align == Align.Bottom
                ? new Vector3(target.center.x - b.center.x, target.min.y - b.min.y, target.center.z - b.center.z)
                : target.center - b.center;
            Shadows(inst, Max(MeshBounds(inst).size) > 0.1f);
            return inst;
        }

        static GameObject Spawn(GameObject model, Transform parent, string name)
        {
            var inst = (GameObject)Object.Instantiate(model, parent, false);
            inst.name = name.StartsWith(Art) ? name : $"{Art}_{name}";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            // visuals only: no physics, lights or scripts that could change the gameplay
            foreach (var c in inst.GetComponentsInChildren<Component>(true).Reverse())
                if (c is Collider || c is Rigidbody || c is Light || c is MonoBehaviour || c is Animator)
                    Object.DestroyImmediate(c);
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;
            return inst;
        }

        /// <summary>World scale s on every axis, whatever (axis-aligned) scale the parent has.</summary>
        static void ScaleUniform(GameObject inst, Transform parent, float s)
        {
            var p = parent.lossyScale;
            inst.transform.localScale = new Vector3(s / p.x, s / p.y, s / p.z);
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.gameObject.layer = parent.gameObject.layer;
            return t;
        }

        static void AddVariant(GameObject on, Transform nameSource, List<GameObject> variants)
        {
            var v = on.AddComponent<ArtVariant>();
            v.nameSource = nameSource;
            v.variants = variants.ToArray();
            for (int i = 0; i < variants.Count; i++) variants[i].SetActive(i == 0);
        }

        static void HideNamed(Transform root, params string[] names)
        {
            foreach (var n in names) Hide(Find(root, n));
        }

        static void Hide(params Transform[] ts)
        {
            foreach (var t in ts)
                if (t && t.TryGetComponent<Renderer>(out var r)) r.enabled = false;
        }

        static void Shadows(GameObject go, bool cast)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        static Transform Find(Transform root, string name)
        {
            if (!root) return null;
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = Find(c, name);
                if (r) return r;
            }
            return null;
        }

        static float Max(Vector3 v) => Mathf.Max(v.x, Mathf.Max(v.y, v.z));

        /// <summary>Bounds of the given objects' own meshes (not their children), in world space. Works on inactive objects.</summary>
        static Bounds OwnBounds(params Transform[] ts)
        {
            Bounds? acc = null;
            foreach (var t in ts)
                if (t && t.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh)
                    Encapsulate(ref acc, t.localToWorldMatrix, mf.sharedMesh.bounds);
            return acc ?? new Bounds(ts.FirstOrDefault(t => t)?.position ?? Vector3.zero, Vector3.one * 0.05f);
        }

        /// <summary>Bounds of every mesh under go, in world space.</summary>
        static Bounds MeshBounds(GameObject go)
        {
            Bounds? acc = null;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh) Encapsulate(ref acc, mf.transform.localToWorldMatrix, mf.sharedMesh.bounds);
            foreach (var sm in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (sm.sharedMesh) Encapsulate(ref acc, sm.transform.localToWorldMatrix, sm.sharedMesh.bounds);
            return acc ?? new Bounds(go.transform.position, Vector3.zero);
        }

        static void Encapsulate(ref Bounds? acc, Matrix4x4 m, Bounds local)
        {
            Vector3 c = local.center, e = local.extents;
            for (int i = 0; i < 8; i++)
            {
                var p = m.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (acc.HasValue) { var b = acc.Value; b.Encapsulate(p); acc = b; }
                else acc = new Bounds(p, Vector3.zero);
            }
        }

        /// <summary>Height of the island's walkable top: median height of the upper-half vertices near the middle.</summary>
        static float PlateauHeight(GameObject inst, Bounds b)
        {
            var ys = new List<float>();
            float r = Mathf.Max(b.extents.x, b.extents.z) * 0.5f;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;   // vertices are always readable in the Editor
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    if (p.y > b.center.y && new Vector2(p.x - b.center.x, p.z - b.center.z).magnitude < r) ys.Add(p.y);
                }
            }
            if (ys.Count == 0) return b.max.y;
            ys.Sort();
            return ys[ys.Count / 2];
        }
    }
}
