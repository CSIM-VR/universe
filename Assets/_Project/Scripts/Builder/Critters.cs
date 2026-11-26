using System.Collections.Generic;
using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Small moving life on one island:
    ///  - the fish (children named Fish_0..n) swim around the lake, wiggling, at their own speed
    ///  - butterflies flutter over the island once the plants have grown (needs wingMaterial)
    ///  - dragonflies hover and dart over the lake once the water life is there (needs wingMaterial)
    /// Creatures only show while their stage of life is visible, so a ruined island loses them and gets them back.
    /// Added to every island at runtime if missing (the fish work without any setup); Apply Imported Art adds it
    /// with the wing material for the flying ones.
    /// </summary>
    public class Critters : MonoBehaviour
    {
        [Tooltip("SpaceBetweenUs/CritterWing material. Empty = no butterflies / dragonflies.")]
        public Material wingMaterial;
        public int butterflies = 3;
        public int dragonflies = 2;
        [Tooltip("Swim the other way round (if the fish look like they swim backwards).")]
        public bool fishReverse;
        public float fishSpeed = 0.9f;

        static readonly Color[] ButterflyColours =
        {
            new(1f, 0.62f, 0.22f), new(0.45f, 0.75f, 1f), new(1f, 0.55f, 0.85f), new(0.98f, 0.92f, 0.5f), new(0.7f, 0.55f, 1f)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void AddToIslands()
        {
            foreach (var island in FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!island.GetComponent<Critters>()) island.gameObject.AddComponent<Critters>();
        }

        // ------------------------------------------------------------------ fish
        class Fish { public Transform t; public Quaternion rot0; public float a0, a, r, y, speed, phase; }
        readonly List<Fish> _fish = new();
        Vector3 _lake;            // lake centre in the fish's parent space

        // ------------------------------------------------------------------ flyers
        class Flyer
        {
            public Transform root, wingL, wingR, wingL2, wingR2;
            public bool dragonfly;
            public Vector3 target, vel;
            public float flap, rest, nextMove;
        }
        readonly List<Flyer> _flyers = new();
        Transform _flyRoot;
        Transform[] _plants, _waterLife;
        float _surfaceY;
        static Mesh _quad;

        void Start()
        {
            // fish: everything named Fish_* under this island, circling the centre of their group (the lake)
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Fish_")) _fish.Add(new Fish { t = t });
                else if (t.name == "Frog") Destroy(t.gameObject);   // the frog was taken out (scenes built before that)
            }
            if (_fish.Count > 0)
            {
                foreach (var f in _fish) _lake += f.t.localPosition;
                _lake /= _fish.Count;
                for (int i = 0; i < _fish.Count; i++)
                {
                    var f = _fish[i];
                    var d = f.t.localPosition - _lake;
                    f.a0 = f.a = Mathf.Atan2(d.z, d.x);
                    f.r = Mathf.Max(0.03f, new Vector2(d.x, d.z).magnitude);
                    f.y = f.t.localPosition.y;
                    f.rot0 = f.t.localRotation;
                    f.speed = fishSpeed * Random.Range(0.7f, 1.3f);
                    f.phase = Random.value * 10f;
                }
            }
            else _lake = new Vector3(0.12f, 0f, 0.1f);

            // flyers appear with their stage of life
            var life = GetComponent<LifeGrowth>();
            if (!life) life = GetComponentInChildren<LifeGrowth>(true);
            if (life && life.stages != null)
                foreach (var s in life.stages)
                {
                    if (s.name == "Plants") _plants = s.growObjects;
                    else if (s.name == "WaterLife") _waterLife = s.growObjects;
                }
            // lake height in island-local units (the island may be scaled)
            _surfaceY = _fish.Count > 0 ? (_fish[0].t.parent.TransformPoint(_lake).y - transform.position.y) / Mathf.Max(1e-4f, transform.lossyScale.y) : 0f;
            if (wingMaterial) BuildFlyers();
        }

        void Update()
        {
            float t = Time.time, dt = Time.deltaTime;
            SwimFish(t, dt);
            if (_flyRoot) Fly(t, dt);
        }

        void SwimFish(float t, float dt)
        {
            float dir = fishReverse ? -1f : 1f;
            foreach (var f in _fish)
            {
                if (!f.t || !f.t.gameObject.activeInHierarchy) continue;
                // speed drifts, the circle breathes in and out a little
                float v = f.speed * (0.75f + 0.25f * Mathf.Sin(t * 0.7f + f.phase));
                f.a += dir * v * dt;
                float r = f.r * (1f + 0.18f * Mathf.Sin(t * 0.45f + f.phase * 1.7f));
                f.t.localPosition = _lake + new Vector3(Mathf.Cos(f.a) * r, f.y - _lake.y + 0.002f * Mathf.Sin(t * 2f + f.phase), Mathf.Sin(f.a) * r);
                float wiggle = 9f * Mathf.Sin(t * 7f * (0.6f + v) + f.phase);
                f.t.localRotation = Quaternion.Euler(0f, -(f.a - f.a0) * Mathf.Rad2Deg + wiggle, 0f) * f.rot0
                                    * (fishReverse ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);
            }
        }

        // ------------------------------------------------------------------ butterflies + dragonflies

        static Mesh Quad()
        {
            if (_quad) return _quad;
            // unit quad from the wing root (x = 0) to the tip (x = 1), centred along z
            _quad = new Mesh { name = "CritterQuad" };
            _quad.vertices = new[] { new Vector3(0f, 0f, -0.5f), new Vector3(1f, 0f, -0.5f), new Vector3(1f, 0f, 0.5f), new Vector3(0f, 0f, 0.5f) };
            _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateNormals();
            _quad.RecalculateBounds();
            return _quad;
        }

        Transform Part(string name, Transform parent, Vector3 pos, Vector3 scale, float shape, Color colour, Color edge)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = wingMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", colour);
            mpb.SetColor("_Edge", edge);
            mpb.SetFloat("_Shape", shape);
            mr.SetPropertyBlock(mpb);
            return go.transform;
        }

        void BuildFlyers()
        {
            _flyRoot = new GameObject("Critters_Flying").transform;
            _flyRoot.SetParent(transform, false);
            int seed = Mathf.Abs(name.GetHashCode());
            for (int i = 0; i < butterflies; i++)
            {
                var c = ButterflyColours[(seed + i) % ButterflyColours.Length];
                var root = new GameObject($"Butterfly_{i}").transform;
                root.SetParent(_flyRoot, false);
                const float span = 0.016f;   // one wing, metres (island scale)
                var f = new Flyer
                {
                    root = root,
                    wingR = Part("WingR", root, Vector3.zero, new Vector3(span, 1f, span * 1.1f), 0f, c, new Color(0.1f, 0.07f, 0.09f)),
                    wingL = Part("WingL", root, Vector3.zero, new Vector3(-span, 1f, span * 1.1f), 0f, c, new Color(0.1f, 0.07f, 0.09f)),
                    flap = Random.value * 10f,
                };
                Part("Body", root, new Vector3(-0.0015f, 0f, 0f), new Vector3(0.003f, 1f, 0.014f), 2f, new Color(0.15f, 0.12f, 0.1f), Color.black);
                root.localPosition = RandomOver(island: true);
                f.target = RandomOver(island: true);
                _flyers.Add(f);
            }
            for (int i = 0; i < dragonflies; i++)
            {
                var root = new GameObject($"Dragonfly_{i}").transform;
                root.SetParent(_flyRoot, false);
                var wingC = new Color(0.75f, 0.95f, 1f, 1f);
                const float span = 0.017f;
                var f = new Flyer
                {
                    root = root,
                    dragonfly = true,
                    wingR = Part("WingR", root, new Vector3(0f, 0f, 0.003f), new Vector3(span, 1f, span * 0.5f), 1f, wingC, wingC),
                    wingL = Part("WingL", root, new Vector3(0f, 0f, 0.003f), new Vector3(-span, 1f, span * 0.5f), 1f, wingC, wingC),
                    wingR2 = Part("WingR2", root, new Vector3(0f, 0f, -0.003f), new Vector3(span * 0.9f, 1f, span * 0.45f), 1f, wingC, wingC),
                    wingL2 = Part("WingL2", root, new Vector3(0f, 0f, -0.003f), new Vector3(-span * 0.9f, 1f, span * 0.45f), 1f, wingC, wingC),
                    flap = Random.value * 10f,
                };
                Part("Body", root, new Vector3(-0.0012f, 0f, -0.006f), new Vector3(0.0024f, 1f, 0.03f), 2f,
                    i % 2 == 0 ? new Color(0.2f, 0.75f, 0.8f) : new Color(0.35f, 0.5f, 1f), Color.black);
                root.localPosition = RandomOver(island: false);
                f.target = RandomOver(island: false);
                _flyers.Add(f);
            }
        }

        // a point in island space: over the whole island top (butterflies) or low over the lake (dragonflies)
        Vector3 RandomOver(bool island)
        {
            Vector3 lakeIsland = _fish.Count > 0 ? transform.InverseTransformPoint(_fish[0].t.parent.TransformPoint(_lake)) : new Vector3(0.12f, 0f, 0.1f);
            if (island)
            {
                var p = Random.insideUnitCircle * 0.27f;
                return new Vector3(p.x, _surfaceY + Random.Range(0.04f, 0.14f), p.y);
            }
            var q = Random.insideUnitCircle * 0.11f;
            return new Vector3(lakeIsland.x + q.x, _surfaceY + Random.Range(0.025f, 0.07f), lakeIsland.z + q.y);
        }

        static bool AnyActive(Transform[] list)
        {
            if (list == null) return false;
            foreach (var t in list) if (t && t.gameObject.activeInHierarchy) return true;
            return false;
        }

        void Fly(float t, float dt)
        {
            bool plants = AnyActive(_plants), water = AnyActive(_waterLife);
            foreach (var f in _flyers)
            {
                bool show = f.dragonfly ? water : plants;
                if (f.root.gameObject.activeSelf != show) f.root.gameObject.SetActive(show);
                if (!show) continue;

                Vector3 p = f.root.localPosition;
                if (f.dragonfly)
                {
                    // hover, then dart quickly to a new spot over the lake
                    if (t >= f.nextMove)
                    {
                        f.target = RandomOver(island: false);
                        f.nextMove = t + Random.Range(1.2f, 3.5f);
                    }
                    p = Vector3.SmoothDamp(p, f.target, ref f.vel, 0.18f, 0.6f, dt);
                    p.y += Mathf.Sin(t * 3f + f.flap) * 0.0006f;
                    f.flap += dt * 40f;
                    float a = 25f * Mathf.Sin(f.flap);
                    float b = 25f * Mathf.Sin(f.flap + 1.6f);
                    f.wingR.localRotation = Quaternion.Euler(0f, 0f, a);
                    f.wingL.localRotation = Quaternion.Euler(0f, 0f, -a);
                    f.wingR2.localRotation = Quaternion.Euler(0f, 0f, b);
                    f.wingL2.localRotation = Quaternion.Euler(0f, 0f, -b);
                }
                else
                {
                    // flutter toward a target with a wobbly path; now and then stop on a plant with slow wing beats
                    bool resting = t < f.rest;
                    if (!resting && (p - f.target).sqrMagnitude < 0.0004f)
                    {
                        if (Random.value < 0.35f) { f.rest = t + Random.Range(1.5f, 3.5f); f.vel = Vector3.zero; resting = true; }
                        f.target = RandomOver(island: true);
                    }
                    if (!resting)
                    {
                        var wobble = new Vector3(Mathf.Sin(t * 2.3f + f.flap), Mathf.Sin(t * 3.1f + f.flap * 2f), Mathf.Cos(t * 1.9f + f.flap)) * 0.03f;
                        var desired = (f.target - p).normalized * 0.07f + wobble;
                        f.vel = Vector3.Lerp(f.vel, desired, dt * 2.5f);
                        p += f.vel * dt;
                        p.y += Mathf.Sin(t * 9f + f.flap) * 0.0004f;    // bobs with each beat
                    }
                    float rate = resting ? 2.5f : 11f, amp = resting ? 20f : 65f;
                    float w = amp * (0.5f + 0.5f * Mathf.Sin(t * rate * Mathf.PI * 2f + f.flap)) + (resting ? 50f : 5f);
                    f.wingR.localRotation = Quaternion.Euler(0f, 0f, w);
                    f.wingL.localRotation = Quaternion.Euler(0f, 0f, -w);
                }
                f.root.localPosition = p;
                // face where it is going (level), smoothly
                var v = f.vel;
                v.y = 0f;
                if (v.sqrMagnitude > 1e-6f)
                    f.root.localRotation = Quaternion.Slerp(f.root.localRotation, Quaternion.LookRotation(v), dt * 4f);
            }
        }
    }
}
