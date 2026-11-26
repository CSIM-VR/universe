using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// A deer made of light: the reward once all three islands are alive.
    /// It fades in on the last island and then lives between the three: on each island it grazes the grass, drinks at
    /// the lake, lifts its head to look at you, then trots to the edge and LEAPS across the gap to a neighbouring island.
    /// Ears flick and the tail swishes; a few gold motes drift off it so it catches the eye. If any island is ruined it
    /// fades away and comes back once all of them live again.
    ///
    /// Built from simple shapes at runtime and drawn with the glowing GhostHand shader (same as the hands of light),
    /// with a strong inner fill so it reads as a creature of light. No model, no animation clip. Added to the scene
    /// automatically. Island-local positions are in metres (the islands are ~0.7 m tabletop models).
    /// </summary>
    public class GlowDeer : MonoBehaviour
    {
        [Tooltip("Island it appears on first. Empty = Island_3 (the one finished last).")]
        public Island startIsland;
        [Tooltip("GhostHand material. Empty = borrowed from the hands of light.")]
        public Material glowMaterial;
        public Color colour = new(1.6f, 1.35f, 0.85f, 1f);
        [Range(0f, 1f)] public float innerFill = 0.6f;
        [Tooltip("Gold motes drifting off the deer (borrows the fireflies' material).")]
        public bool motes = true;
        [Tooltip("Wait for all islands alive (true) or only the start island (false).")]
        public bool waitForAllIslands = true;
        [Tooltip("Height of the back is ~4.5 cm x size.")]
        public float size = 2.4f;
        public float walkSpeed = 0.06f;
        public float leapHeight = 0.14f;
        public float leapTime = 1.0f;

        [Header("Island layout (island-local metres)")]
        public Vector3 lakeCentre = new(0.12f, 0f, 0.1f);
        public float lakeRadius = 0.12f;
        public float islandRadius = 0.27f;
        public float groundY = 0.004f;

        // body plan (deer local space, metres at size 1; +z = forward, +y = up)
        const float HipY = 0.036f, LegSeg = 0.019f, NeckLen = 0.038f, HeadLen = 0.022f;

        GameFlowManager _flow;
        Island[] _islands;
        Island _on;
        Transform _root, _body, _neck, _head, _earL, _earR, _tail;
        float _k = 1f;   // island scale: the deer, its steps and leaps grow with the islands
        readonly List<(Transform hip, Transform knee, float phase, bool front)> _legs = new();
        Material _mat;
        ParticleSystem _motes;
        float _gaitPhase, _walkBlend, _leap, _neckPitch = 35f, _headPitch = 15f, _lookYaw, _chew;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void AddToScene()
        {
            if (FindAnyObjectByType<GlowDeer>() || !FindAnyObjectByType<Island>()) return;
            new GameObject("GlowDeer").AddComponent<GlowDeer>();
        }

        void Start()
        {
            _flow = FindAnyObjectByType<GameFlowManager>();
            _islands = FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(_islands, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (_islands.Length == 0) { enabled = false; return; }
            if (!startIsland)
            {
                startIsland = _islands[_islands.Length - 1];
                foreach (var i in _islands) if (i.name == "Island_3") startIsland = i;
            }
            _k = Mathf.Max(0.01f, _islands[0].transform.lossyScale.x);
            size *= _k;
            walkSpeed *= _k;
            leapHeight *= _k;
            _mat = MakeMaterial();
            if (!_mat) { Debug.LogWarning("[GlowDeer] no GhostHand material found"); enabled = false; return; }
            Build();
            StartCoroutine(Life());
        }

        Material MakeMaterial()
        {
            var src = glowMaterial;
            if (!src)
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (r.sharedMaterial && r.sharedMaterial.shader && r.sharedMaterial.shader.name == "SpaceBetweenUs/GhostHand") { src = r.sharedMaterial; break; }
            if (!src) return null;
            var m = new Material(src) { name = "GlowDeer" };
            if (m.HasProperty("_Fill")) m.SetFloat("_Fill", innerFill);
            if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 2.2f);
            if (m.HasProperty("_RimPower")) m.SetFloat("_RimPower", 1.8f);
            if (m.HasProperty("_Shimmer")) m.SetFloat("_Shimmer", 0.35f);
            m.SetColor("_Color", Color.black);
            return m;
        }

        // ------------------------------------------------------------------ body

        Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        void Shape(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void Build()
        {
            _root = Pivot("Deer", transform, Vector3.zero);
            _root.localScale = Vector3.one * size;
            _body = Pivot("Body", _root, Vector3.zero);

            // torso, chest, rump
            Shape(PrimitiveType.Sphere, _body, new Vector3(0f, 0.043f, 0f), new Vector3(0.021f, 0.022f, 0.052f));
            Shape(PrimitiveType.Sphere, _body, new Vector3(0f, 0.046f, 0.016f), new Vector3(0.019f, 0.022f, 0.022f));
            Shape(PrimitiveType.Sphere, _body, new Vector3(0f, 0.045f, -0.018f), new Vector3(0.019f, 0.02f, 0.02f));

            // legs: hip pivot -> upper leg -> knee pivot -> lower leg (+ a tiny hoof)
            void Leg(string n, float x, float z, float phase, bool front)
            {
                var hip = Pivot(n, _body, new Vector3(x, HipY, z));
                Shape(PrimitiveType.Capsule, hip, new Vector3(0f, -LegSeg * 0.5f, 0f), new Vector3(0.0065f, LegSeg * 0.55f, 0.0065f));
                var knee = Pivot(n + "_Knee", hip, new Vector3(0f, -LegSeg, 0f));
                Shape(PrimitiveType.Capsule, knee, new Vector3(0f, -LegSeg * 0.5f, 0f), new Vector3(0.0045f, LegSeg * 0.55f, 0.0045f));
                Shape(PrimitiveType.Sphere, knee, new Vector3(0f, -LegSeg, 0.001f), new Vector3(0.005f, 0.003f, 0.006f));
                _legs.Add((hip, knee, phase, front));
            }
            Leg("LegFL", -0.0065f, 0.019f, 0f, true);
            Leg("LegFR", 0.0065f, 0.019f, Mathf.PI, true);
            Leg("LegBL", -0.0065f, -0.019f, Mathf.PI, false);
            Leg("LegBR", 0.0065f, -0.019f, 0f, false);

            // neck (pivot at the chest, grows along +y, tilted forward) and head (pivot at the neck tip, points along +z)
            _neck = Pivot("Neck", _body, new Vector3(0f, 0.05f, 0.022f));
            Shape(PrimitiveType.Capsule, _neck, new Vector3(0f, NeckLen * 0.5f, 0f), new Vector3(0.011f, NeckLen * 0.55f, 0.011f));
            _head = Pivot("Head", _neck, new Vector3(0f, NeckLen, 0f));
            Shape(PrimitiveType.Sphere, _head, new Vector3(0f, 0f, HeadLen * 0.35f), new Vector3(0.013f, 0.013f, HeadLen));
            Shape(PrimitiveType.Sphere, _head, new Vector3(0f, -0.002f, HeadLen * 0.85f), new Vector3(0.008f, 0.008f, 0.009f));   // muzzle
            _earL = Pivot("EarL", _head, new Vector3(-0.006f, 0.005f, 0.002f));
            _earR = Pivot("EarR", _head, new Vector3(0.006f, 0.005f, 0.002f));
            Shape(PrimitiveType.Sphere, _earL, new Vector3(-0.004f, 0.003f, 0f), new Vector3(0.009f, 0.004f, 0.005f), new Vector3(0f, 0f, 25f));
            Shape(PrimitiveType.Sphere, _earR, new Vector3(0.004f, 0.003f, 0f), new Vector3(0.009f, 0.004f, 0.005f), new Vector3(0f, 0f, -25f));
            // antlers: a main beam and two tines per side
            foreach (float s in new[] { -1f, 1f })
            {
                var beam = Pivot("Antler", _head, new Vector3(0.004f * s, 0.006f, 0.004f));
                beam.localRotation = Quaternion.Euler(-20f, 0f, -28f * s);
                Shape(PrimitiveType.Capsule, beam, new Vector3(0f, 0.011f, 0f), new Vector3(0.0022f, 0.011f, 0.0022f));
                Shape(PrimitiveType.Capsule, beam, new Vector3(0f, 0.009f, 0.004f), new Vector3(0.0018f, 0.005f, 0.0018f), new Vector3(50f, 0f, 0f));
                Shape(PrimitiveType.Capsule, beam, new Vector3(0f, 0.018f, 0.003f), new Vector3(0.0016f, 0.004f, 0.0016f), new Vector3(45f, 0f, 0f));
            }
            _tail = Pivot("Tail", _body, new Vector3(0f, 0.05f, -0.036f));
            Shape(PrimitiveType.Sphere, _tail, new Vector3(0f, -0.002f, -0.002f), new Vector3(0.006f, 0.008f, 0.005f));

            if (motes) BuildMotes();
            _root.gameObject.SetActive(false);
        }

        // a few gold motes drifting off the deer (world space, so they trail behind when it moves or leaps)
        void BuildMotes()
        {
            Material mat = null;
            foreach (var psr in FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (psr.sharedMaterial && psr.name.Contains("Firefl")) { mat = psr.sharedMaterial; break; }
            if (!mat) return;
            var go = new GameObject("Motes");
            go.transform.SetParent(_body, false);
            go.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            _motes = go.AddComponent<ParticleSystem>();
            _motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _motes.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f * size, 0.015f * size);
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f * size, 0.008f * size);
            main.startColor = new Color(1f, 0.85f, 0.5f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.002f;
            main.maxParticles = 40;
            var em = _motes.emission;
            em.rateOverTime = 7f;
            var shape = _motes.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f * _k;
            var col = _motes.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ places

        bool AllAlive()
        {
            if (!waitForAllIslands) return startIsland && startIsland.IsAlive;
            foreach (var i in _islands) if (i && !i.IsAlive) return false;
            return true;
        }

        Vector3 OnIsland(Island i, Vector3 local) { local.y = groundY; return i.transform.TransformPoint(local); }

        Vector3 LakeEdge(Island i, Vector3 fromWorld)
        {
            var fromLocal = i.transform.InverseTransformPoint(fromWorld);
            var d = fromLocal - lakeCentre; d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) d = Vector3.left;
            return OnIsland(i, lakeCentre + d.normalized * (lakeRadius + 0.024f * size / _k));
        }

        Vector3 GrassSpot(Island i)
        {
            // somewhere on the lawn: on the island, off the lake and off the stream
            for (int n = 0; n < 30; n++)
            {
                var p = Random.insideUnitCircle * (islandRadius - 0.04f);
                bool lake = (p - new Vector2(lakeCentre.x, lakeCentre.z)).magnitude < lakeRadius + 0.04f;
                bool stream = p.x > 0.15f && Mathf.Abs(p.y - 0.06f) < 0.08f;
                if (!lake && !stream) return OnIsland(i, new Vector3(p.x, 0f, p.y));
            }
            return OnIsland(i, new Vector3(-0.15f, 0f, -0.1f));
        }

        Island NextIsland()
        {
            // a neighbour: the closest other island (when two are about as close, either)
            Island best = null, second = null;
            float bd = float.MaxValue, sd = float.MaxValue;
            foreach (var i in _islands)
            {
                if (!i || i == _on) continue;
                float d = (i.transform.position - _on.transform.position).sqrMagnitude;
                if (d < bd) { second = best; sd = bd; best = i; bd = d; }
                else if (d < sd) { second = i; sd = d; }
            }
            if (second && sd < bd * 1.4f && Random.value < 0.5f) return second;
            return best;
        }

        // ------------------------------------------------------------------ behaviour

        IEnumerator Life()
        {
            while (true)
            {
                while (!AllAlive()) yield return new WaitForSeconds(0.5f);
                yield return new WaitForSeconds(1.5f);

                // appear on the start island, at the lawn
                _on = startIsland;
                _root.position = GrassSpot(_on);
                _root.rotation = Quaternion.LookRotation(Flat(_on.transform.position - _root.position + Vector3.forward * 1e-3f));
                _neckPitch = 35f; _headPitch = 15f; _lookYaw = 0f; _leap = 0f;
                _root.gameObject.SetActive(true);
                if (_motes) _motes.Play();
                yield return Fade(0f, 1f, 2f);

                while (AllAlive())
                {
                    // a visit: graze once or twice, drink, look at you
                    int grazes = Random.Range(1, 3);
                    for (int g = 0; g < grazes && AllAlive(); g++)
                    {
                        yield return WalkTo(GrassSpot(_on));
                        yield return Graze(Random.Range(3f, 6f));
                    }
                    if (!AllAlive()) break;
                    yield return WalkTo(LakeEdge(_on, _root.position));
                    yield return FaceTo(OnIsland(_on, lakeCentre), 0.6f);
                    yield return Drink(Random.Range(3, 5));
                    float until = Time.time + Random.Range(2.5f, 5f);
                    while (AllAlive() && Time.time < until) { LookAtPlayer(); yield return null; }
                    _lookYaw = 0f;
                    _headPitch = 15f;

                    // then on to a neighbouring island
                    var next = NextIsland();
                    if (!next || !AllAlive()) continue;
                    Vector3 across = Flat(next.transform.position - _on.transform.position).normalized;
                    Vector3 takeOff = _on.transform.position + across * (islandRadius - 0.01f) * _k; takeOff.y = OnIsland(_on, Vector3.zero).y;
                    Vector3 landing = next.transform.position - across * (islandRadius - 0.03f) * _k; landing.y = OnIsland(next, Vector3.zero).y;
                    yield return WalkTo(takeOff);
                    yield return FaceTo(landing, 0.4f);
                    yield return Leap(takeOff, landing);
                    _on = next;
                }

                // an island was ruined: fade away, try again once all live again
                yield return Fade(1f, 0f, 1.5f);
                if (_motes) _motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                _root.gameObject.SetActive(false);
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        IEnumerator WalkTo(Vector3 target)
        {
            _walkBlend = 1f;
            float guard = Time.time + 20f;
            while (AllAlive() && Time.time < guard && Flat(_root.position - target).magnitude > 0.004f * size)
            {
                Vector3 to = target - _root.position;
                Face(to, Time.deltaTime * 5f);
                // walk where it is facing (so it turns along a curve instead of sliding sideways)
                float speed = walkSpeed * Mathf.Clamp01(Vector3.Dot(_root.forward, Flat(to).normalized) + 0.3f);
                var p = _root.position + _root.forward * speed * Time.deltaTime;
                p.y = Mathf.Lerp(p.y, target.y, Time.deltaTime * 6f);
                _root.position = p;
                _gaitPhase += Time.deltaTime * 9f;
                yield return null;
            }
            _walkBlend = 0f;
        }

        IEnumerator FaceTo(Vector3 point, float time)
        {
            for (float t = 0f; t < time; t += Time.deltaTime) { Face(point - _root.position, Time.deltaTime * 6f); yield return null; }
        }

        void Face(Vector3 dir, float k)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-8f) return;
            _root.rotation = Quaternion.Slerp(_root.rotation, Quaternion.LookRotation(dir), Mathf.Clamp01(k));
        }

        IEnumerator Leap(Vector3 from, Vector3 to)
        {
            // crouch, spring in an arc with the legs tucked, land with a little dip
            yield return Pose(25f, 5f, 0.25f);
            float dist = Flat(to - from).magnitude;
            float time = leapTime * Mathf.Lerp(0.8f, 1.3f, Mathf.InverseLerp(0.1f, 0.6f, dist));
            float h = leapHeight + dist * 0.15f;
            Quaternion level = Quaternion.LookRotation(Flat(to - from));
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                var p = Vector3.Lerp(from, to, k) + Vector3.up * (4f * h * k * (1f - k));
                _root.position = p;
                // body follows the arc: nose up on the way up, down on the way down
                float pitch = Mathf.Lerp(-30f, 30f, k);
                _root.rotation = level * Quaternion.Euler(pitch, 0f, 0f);
                _leap = Mathf.Sin(k * Mathf.PI);
                yield return null;
            }
            _root.position = to;
            _root.rotation = level;
            _leap = 0f;
            yield return Pose(35f, 15f, 0.3f);
        }

        IEnumerator Graze(float seconds)
        {
            yield return Pose(155f, 88f, 1.1f);                      // muzzle to the grass
            float end = Time.time + seconds;
            while (AllAlive() && Time.time < end)
            {
                _chew = Mathf.Sin(Time.time * 9f);                    // nibbling
                // sometimes take a step to the next tuft
                if (Random.value < Time.deltaTime * 0.25f) yield return Nudge();
                yield return null;
            }
            _chew = 0f;
            yield return Pose(35f, 15f, 0.9f);
        }

        IEnumerator Nudge()
        {
            _walkBlend = 0.6f;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                _root.position += _root.forward * walkSpeed * 0.25f * Time.deltaTime;
                _gaitPhase += Time.deltaTime * 7f;
                yield return null;
            }
            _walkBlend = 0f;
        }

        IEnumerator Drink(int sips)
        {
            _lookYaw = 0f;
            yield return Pose(150f, 75f, 1.3f);                       // head down to the water
            for (int i = 0; i < sips && AllAlive(); i++)
            {
                yield return Pose(140f, 70f, 0.35f);
                yield return Pose(150f, 78f, 0.35f);
            }
            yield return Pose(35f, 10f, 1.1f);                        // head up
        }

        IEnumerator Pose(float neck, float head, float time)
        {
            float n0 = _neckPitch, h0 = _headPitch;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / time);
                _neckPitch = Mathf.Lerp(n0, neck, k);
                _headPitch = Mathf.Lerp(h0, head, k);
                yield return null;
            }
            _neckPitch = neck; _headPitch = head;
        }

        void LookAtPlayer()
        {
            var cam = Camera.main;
            if (!cam) return;
            var local = _root.InverseTransformPoint(cam.transform.position);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70f, 70f);
            _lookYaw = Mathf.MoveTowardsAngle(_lookYaw, yaw, 60f * Time.deltaTime);
            _headPitch = Mathf.MoveTowards(_headPitch, local.y > 0.1f ? -10f : 10f, 20f * Time.deltaTime);
        }

        IEnumerator Fade(float from, float to, float time)
        {
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                _mat.SetColor("_Color", colour * Mathf.Lerp(from, to, t / time));
                yield return null;
            }
            _mat.SetColor("_Color", colour * to);
        }

        void Update()
        {
            if (!_root || !_root.gameObject.activeSelf) return;
            float t = Time.time;

            // legs: trot while walking (diagonal pairs together); tucked in a leap (front forward, hind back)
            foreach (var (hip, knee, phase, front) in _legs)
            {
                float s = Mathf.Sin(_gaitPhase + phase);
                float swing = 28f * s * _walkBlend;
                float bend = 35f * Mathf.Max(0f, Mathf.Cos(_gaitPhase + phase)) * _walkBlend;
                swing += (front ? -55f : 45f) * _leap;
                bend += (front ? 70f : -50f) * _leap;
                hip.localRotation = Quaternion.Euler(swing, 0f, 0f);
                knee.localRotation = Quaternion.Euler(-bend, 0f, 0f);
            }
            _body.localPosition = new Vector3(0f, _walkBlend * 0.0015f * Mathf.Abs(Mathf.Sin(_gaitPhase)) + 0.0004f * Mathf.Sin(t * 1.3f), 0f);

            // neck + head (rotations about the same x axis add up; head pitch is relative to the body)
            _neck.localRotation = Quaternion.Euler(_neckPitch + 3f * _chew, _lookYaw * 0.6f, 0f);
            _head.localRotation = Quaternion.Euler(_headPitch - _neckPitch + 4f * _chew, _lookYaw * 0.4f, 0f);

            // little life: ear flicks, tail swish
            float flick = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 0.9f)), 30f) * 35f;
            _earL.localRotation = Quaternion.Euler(0f, 0f, flick);
            _earR.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 0.7f + 2f)), 30f) * 35f);
            _tail.localRotation = Quaternion.Euler(20f * Mathf.Sin(t * 3f) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 0.5f)), 4f) + 30f * _leap, 0f, 0f);
        }
    }
}
