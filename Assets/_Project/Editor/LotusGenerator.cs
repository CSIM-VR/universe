using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// Builds an original lotus from code: three layers of cupped petals (white-pink base to pink tip, soft glow),
    /// a golden seed pod with glowing seeds, and notched lily pads. Meshes, textures and materials are saved under
    /// Assets/_Project/Art/Lotus so prefabs can reference them. Used by Apply Imported Art.
    /// </summary>
    public static class LotusGenerator
    {
        const string Dir = "Assets/_Project/Art/Lotus";

        // ------------------------------------------------------------------ public

        /// <summary>A lotus flower (world size ~7 cm across when open) with its base at parent's origin.</summary>
        public static GameObject BuildFlower(Transform parent, int layer)
        {
            EnsureDir();
            var petalMesh = SaveMesh(PetalMesh(), "Lotus_Petal");
            var petalMat = PetalMaterial();
            var podMat = LitMaterial("Lotus_Pod", Hex("#E9D27A"), 0.35f, Hex("#B8901F") * 0.6f);
            var seedMat = LitMaterial("Lotus_Seed", Hex("#FFE9A8"), 0.6f, Hex("#FFC85A") * 2.2f);

            var root = new GameObject("Art_Lotus");
            root.layer = layer;
            root.transform.SetParent(parent, false);

            // layers: count, petal length (m), width factor, base ring radius, closed / open lift, start delay
            var layers = new (int count, float length, float width, float ring, float closed, float open, float delay, float yawOffset)[]
            {
                (9, 0.034f, 0.46f, 0.009f, 72f, 18f, 0.0f, 0f),
                (8, 0.029f, 0.44f, 0.006f, 78f, 42f, 0.5f, 20f),
                (6, 0.021f, 0.42f, 0.0035f, 84f, 66f, 1.0f, 0f),
            };

            var petals = new List<Transform>();
            var yaws = new List<float>();
            var closed = new List<float>();
            var open = new List<float>();
            var delays = new List<float>();
            var rnd = new System.Random(7);
            foreach (var L in layers)
            {
                for (int i = 0; i < L.count; i++)
                {
                    float yaw = L.yawOffset + i * 360f / L.count + (float)(rnd.NextDouble() - 0.5) * 8f;
                    var pivot = new GameObject("Petal").transform;
                    pivot.gameObject.layer = layer;
                    pivot.SetParent(root.transform, false);
                    pivot.localPosition = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * L.ring;
                    var petal = new GameObject("Mesh");
                    petal.layer = layer;
                    petal.transform.SetParent(pivot, false);
                    float var01 = 0.92f + (float)rnd.NextDouble() * 0.16f;
                    petal.transform.localScale = new Vector3(L.length * L.width * var01, L.length * var01, L.length * var01);   // mesh is 1 wide
                    petal.AddComponent<MeshFilter>().sharedMesh = petalMesh;
                    var r = petal.AddComponent<MeshRenderer>();
                    r.sharedMaterial = petalMat;
                    r.shadowCastingMode = ShadowCastingMode.Off;

                    petals.Add(pivot);
                    yaws.Add(yaw);
                    closed.Add(L.closed + (float)(rnd.NextDouble() - 0.5) * 4f);
                    open.Add(L.open + (float)(rnd.NextDouble() - 0.5) * 8f);
                    delays.Add(L.delay + (float)rnd.NextDouble() * 0.25f);
                }
            }

            // seed pod + glowing seeds (the crystal's light lives on in the flower)
            var pod = Primitive(PrimitiveType.Cylinder, "Pod", root.transform, new Vector3(0f, 0.005f, 0f), new Vector3(0.013f, 0.0035f, 0.013f), podMat, layer);
            for (int i = 0; i < 7; i++)
            {
                Vector3 p = i == 0 ? Vector3.zero : Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * 0.0042f;
                Primitive(PrimitiveType.Sphere, "Seed", pod.transform.parent, p + new Vector3(0f, 0.0088f, 0f), Vector3.one * 0.0022f, seedMat, layer);
            }

            var bloom = root.AddComponent<LotusBloom>();
            bloom.petals = petals.ToArray();
            bloom.yaw = yaws.ToArray();
            bloom.closedLift = closed.ToArray();
            bloom.openLift = open.ToArray();
            bloom.delay = delays.ToArray();
            // show it open in the editor / prefab preview
            for (int i = 0; i < petals.Count; i++)
                petals[i].localRotation = Quaternion.Euler(0f, yaws[i], 0f) * Quaternion.Euler(-open[i], 0f, 0f);
            return root;
        }

        /// <summary>Three notched lily pads of different size around parent's origin (flat, facing up).</summary>
        public static GameObject BuildPads(Transform parent, int layer)
        {
            EnsureDir();
            var padMesh = SaveMesh(PadMesh(), "Lotus_Pad");
            var padMat = PadMaterial();
            var root = new GameObject("Art_LotusPads");
            root.layer = layer;
            root.transform.SetParent(parent, false);
            var pads = new (Vector2 pos, float size, float yaw)[]
            {
                (new Vector2(0f, 0f), 0.085f, 30f),
                (new Vector2(0.06f, -0.035f), 0.05f, 160f),
                (new Vector2(-0.05f, 0.045f), 0.042f, 260f),
            };
            foreach (var (pos, size, yaw) in pads)
            {
                var go = new GameObject("Pad");
                go.layer = layer;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(pos.x, 0f, pos.y);
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = Vector3.one * size;
                go.AddComponent<MeshFilter>().sharedMesh = padMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = padMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return root;
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>
        /// Unit petal along +Z (length 1), cup opening to +Y: widest a third of the way up, pointed tip,
        /// edges raised (cupped) and the tip curling up (= inwards once the petal is lifted). Double sided.
        /// UV: x across, y along (for the colour gradient). X extent is ±0.5 so localScale.x = width.
        /// </summary>
        static Mesh PetalMesh()
        {
            const int nu = 8, nv = 14;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int j = 0; j <= nv; j++)
            {
                float v = j / (float)nv;
                float w = Mathf.Sin(Mathf.PI * Mathf.Pow(v, 0.7f)) * 0.5f * (1f - 0.15f * v);   // half width
                for (int i = 0; i <= nu; i++)
                {
                    float u = i / (float)nu * 2f - 1f;
                    float x = u * w;
                    float y = u * u * w * 0.55f          // cupped across
                            + v * v * 0.22f;             // tip curls up
                    verts.Add(new Vector3(x, y, v));
                    uvs.Add(new Vector2(i / (float)nu, v));
                }
            }
            var tris = new List<int>();
            int row = nu + 1;
            for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
            return DoubleSided(verts, uvs, tris, "Lotus_Petal");
        }

        /// <summary>Unit lily pad (diameter 1) on the XZ plane: a disc with a wedge cut to the centre, rim slightly raised.</summary>
        static Mesh PadMesh()
        {
            const int seg = 40, rings = 4;
            const float notch = 28f * Mathf.Deg2Rad;
            var verts = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { new(0.5f, 0.5f) };
            for (int r = 1; r <= rings; r++)
            {
                float rad = 0.5f * r / rings;
                for (int s = 0; s <= seg; s++)
                {
                    float a = notch * 0.5f + (Mathf.PI * 2f - notch) * s / seg;
                    var p = new Vector3(Mathf.Cos(a) * rad, Mathf.Pow(r / (float)rings, 3f) * 0.04f, Mathf.Sin(a) * rad);
                    verts.Add(p);
                    uvs.Add(new Vector2(p.x + 0.5f, p.z + 0.5f));
                }
            }
            var tris = new List<int>();
            int row = seg + 1;
            for (int s = 0; s < seg; s++) tris.AddRange(new[] { 0, 1 + s + 1, 1 + s });
            for (int r = 0; r < rings - 1; r++)
            for (int s = 0; s < seg; s++)
            {
                int a = 1 + r * row + s, b = a + 1, c = a + row, d = c + 1;
                tris.AddRange(new[] { a, b, c, b, d, c });
            }
            return DoubleSided(verts, uvs, tris, "Lotus_Pad");
        }

        static Mesh DoubleSided(List<Vector3> verts, List<Vector2> uvs, List<int> tris, string name)
        {
            int n = verts.Count;
            var front = new Mesh { name = name };
            front.SetVertices(verts);
            front.SetUVs(0, uvs);
            front.SetTriangles(tris, 0);
            front.RecalculateNormals();
            var normals = new List<Vector3>();
            front.GetNormals(normals);

            var allV = new List<Vector3>(verts); allV.AddRange(verts);
            var allUV = new List<Vector2>(uvs); allUV.AddRange(uvs);
            var allN = new List<Vector3>(normals);
            foreach (var nm in normals) allN.Add(-nm);
            var allT = new List<int>(tris);
            for (int i = 0; i < tris.Count; i += 3) allT.AddRange(new[] { tris[i] + n, tris[i + 2] + n, tris[i + 1] + n });

            var m = new Mesh { name = name };
            m.SetVertices(allV);
            m.SetUVs(0, allUV);
            m.SetNormals(allN);
            m.SetTriangles(allT, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            Object.DestroyImmediate(front);
            return m;
        }

        // ------------------------------------------------------------------ materials / textures

        static Material PetalMaterial()
        {
            var tex = SaveTexture("Lotus_Petal_Gradient", 32, 64, (u, v) =>
            {
                var baseCol = Hex("#FFF4F8");
                var tip = Hex("#FF7FB8");
                float t = Mathf.Pow(v, 1.6f) * 0.85f + Mathf.Pow(Mathf.Abs(u * 2f - 1f), 3f) * 0.15f;   // pinker at tip + edges
                var c = Color.Lerp(baseCol, tip, Mathf.Clamp01(t));
                // faint veins along the petal
                float vein = 0.04f * Mathf.Pow(Mathf.Abs(Mathf.Sin((u - 0.5f) * Mathf.PI * 7f)), 12f) * (1f - v);
                return c * (1f - vein);
            });
            var mat = LitMaterial("Lotus_Petal", Color.white, 0.45f, Hex("#FF8FC4") * 0.35f, tex);
            mat.SetTexture("_EmissionMap", tex);
            mat.SetFloat("_Cull", (float)CullMode.Back);   // the mesh is already double sided
            return mat;
        }

        static Material PadMaterial()
        {
            var tex = SaveTexture("Lotus_Pad_Veins", 128, 128, (u, v) =>
            {
                float x = u - 0.5f, z = v - 0.5f;
                float r = Mathf.Sqrt(x * x + z * z) * 2f;
                float a = Mathf.Atan2(z, x);
                var c = Color.Lerp(Hex("#5DBB6E"), Hex("#2F7F4A"), Mathf.Clamp01(r * 1.1f));
                float veins = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 9f)), 40f) * (1f - r) * 0.5f;
                c = Color.Lerp(c, Hex("#9BE08A"), veins);
                if (r > 0.92f) c *= 0.8f;   // darker rim
                return c;
            });
            return LitMaterial("Lotus_Pad", Color.white, 0.55f, null, tex);
        }

        static Material LitMaterial(string name, Color baseColor, float smooth, Color? emission, Texture2D baseMap = null)
        {
            string path = $"{Dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smooth);
            mat.SetFloat("_Metallic", 0f);
            if (baseMap) mat.SetTexture("_BaseMap", baseMap);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else mat.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D SaveTexture(string name, int w, int h, System.Func<float, float, Color> f)
        {
            string path = $"{Dir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = f((x + 0.5f) / w, (y + 0.5f) / h);
                c.a = 1f;
                px[y * w + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.wrapMode != TextureWrapMode.Clamp)
            {
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ helpers

        static Mesh SaveMesh(Mesh m, string name)
        {
            string path = $"{Dir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                existing.Clear();
                EditorUtility.CopySerialized(m, existing);
                Object.DestroyImmediate(m);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, int layer)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static void EnsureDir()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art")) AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project/Art", "Lotus");
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
