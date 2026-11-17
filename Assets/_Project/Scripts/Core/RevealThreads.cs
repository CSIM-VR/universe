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
                if (t > growTime) break;
                yield return null;
            }
        }
    }
}
