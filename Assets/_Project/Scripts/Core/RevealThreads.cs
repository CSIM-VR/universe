using System.Collections;
using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Step 6: threads of light grow from each island to the asteroid it came from.
    /// islands[i] is linked to asteroids[i]. Wire GameFlowManager.onReveal -> Play().
    ///
    /// Each thread is a soft glowing curve (rises from the island, arcs over to the asteroid) with beads of light
    /// flowing along it, warm gold at the island turning violet at the asteroid. The thread itself is drawn like the
    /// slot halos: no hard core, a soft breathing glow.
    /// </summary>
    public class RevealThreads : MonoBehaviour
    {
        public Transform[] islands;
        public Transform[] asteroids;
        [Tooltip("SpaceBetweenUs/LightThread material (falls back to anything that works on a LineRenderer).")]
        public Material threadMaterial;
        public float width = 0.02f;
        public float growTime = 4f;
        [Tooltip("How high the curve rises, as a fraction of the island-asteroid distance.")]
        public float arc = 0.18f;
        public int segments = 40;
        public Gradient colour;
        public GameObject[] enableOnReveal;   // e.g. the glowing deer

        [Header("Bring the asteroids into view")]
        [Tooltip("At the reveal each asteroid's remains drift to the sky above its island, where the player can see it " +
                 "(the step-4 asteroid is otherwise straight overhead, so its thread seems to vanish into the sky).")]
        public bool gatherAsteroids = true;
        [Tooltip("Distance from the player (metres, at Builder scale).")]
        public float gatherDistance = 22f;
        [Tooltip("Height above the horizon, in degrees.")]
        public float gatherElevation = 26f;
        public float gatherTime = 5f;

        static readonly Color Gold = new(1f, 0.82f, 0.45f), Violet = new(0.72f, 0.55f, 1f);

        void Reset() => colour = DefaultGradient();

        public void Play() => StartCoroutine(Grow());

        static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Gold, 0f), new GradientColorKey(Color.Lerp(Gold, Violet, 0.5f), 0.5f), new GradientColorKey(Violet, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        IEnumerator Grow()
        {
            foreach (var g in enableOnReveal) if (g) g.SetActive(true);
            if (colour == null || colour.colorKeys.Length < 2) colour = DefaultGradient();

            int n = Mathf.Min(islands.Length, asteroids.Length);
            if (gatherAsteroids) StartCoroutine(Gather(n));
            var lines = new LineRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"Thread_{i}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = threadMaterial;
                lr.positionCount = 0;
                lr.useWorldSpace = true;
                lr.textureMode = LineTextureMode.Stretch;
                lr.numCapVertices = 4;
                lr.alignment = LineAlignment.View;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.colorGradient = colour;
                // thin at the island, wider far away so it still reads at the asteroids' distance
                lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.4f, 4f), new Keyframe(1f, 22f));
                lr.widthMultiplier = width;
                lines[i] = lr;
            }

            var pts = new Vector3[segments + 1];
            // keep drawing while the asteroids are still drifting into place
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / growTime));
                for (int i = 0; i < n; i++)
                {
                    if (!islands[i] || !asteroids[i]) continue;
                    Vector3 a = islands[i].position, b = asteroids[i].position;
                    // quadratic curve: rises out of the island, arcs over to the asteroid
                    Vector3 c = (a + b) * 0.5f + Vector3.up * (Vector3.Distance(a, b) * arc);
                    int count = Mathf.Max(2, Mathf.CeilToInt(segments * k) + 1);
                    for (int s = 0; s < count; s++)
                    {
                        float u = k * s / (count - 1);
                        pts[s] = (1 - u) * (1 - u) * a + 2 * (1 - u) * u * c + u * u * b;
                    }
                    lines[i].positionCount = count;
                    lines[i].SetPositions(pts);
                }
                if (t > growTime && (!gatherAsteroids || t > gatherTime + 0.2f)) break;
                yield return null;
            }
        }

        /// <summary>Each asteroid glides to a spot in the sky in the direction of its island, as seen by the player.</summary>
        IEnumerator Gather(int n)
        {
            var cam = Camera.main;
            if (!cam) yield break;
            Vector3 eye = cam.transform.position;
            float scale = Mathf.Max(0.01f, cam.transform.lossyScale.x);
            var from = new Vector3[n];
            var to = new Vector3[n];
            var spin = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                if (!islands[i] || !asteroids[i]) continue;
                asteroids[i].gameObject.SetActive(true);
                var flat = islands[i].position - eye;
                flat.y = 0f;
                if (flat.sqrMagnitude < 1e-6f) flat = cam.transform.forward;
                flat.y = 0f;
                flat.Normalize();
                float el = gatherElevation * Mathf.Deg2Rad;
                from[i] = asteroids[i].position;
                to[i] = eye + (flat * Mathf.Cos(el) + Vector3.up * Mathf.Sin(el)) * gatherDistance * scale;
                spin[i] = Random.onUnitSphere * Random.Range(4f, 9f);
            }
            for (float t = 0f; ; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / gatherTime));
                for (int i = 0; i < n; i++)
                {
                    if (!islands[i] || !asteroids[i]) continue;
                    asteroids[i].position = Vector3.Lerp(from[i], to[i], k);
                    asteroids[i].Rotate(spin[i] * Time.deltaTime, Space.World);   // a slow tumble, now and forever
                }
                if (t > gatherTime) break;
                yield return null;
            }
            // keep tumbling gently
            while (true)
            {
                for (int i = 0; i < n; i++)
                    if (asteroids[i]) asteroids[i].Rotate(spin[i] * 0.5f * Time.deltaTime, Space.World);
                yield return null;
            }
        }
    }
}
