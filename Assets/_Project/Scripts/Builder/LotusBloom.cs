using System.Collections;
using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Opens the generated lotus when it appears (outer petals first), then lets it sway gently.
    /// Petal rotation = yaw around the stem * lift above the horizontal (petal meshes point along +Z, cup facing +Y).
    /// </summary>
    public class LotusBloom : MonoBehaviour
    {
        public Transform[] petals;
        public float[] yaw;
        public float[] closedLift;   // degrees above horizontal when closed (almost upright)
        public float[] openLift;     // degrees above horizontal when open
        public float[] delay;        // seconds before this petal starts opening
        public float openTime = 2.4f;
        public float sway = 2.5f;    // degrees of idle breathing once open

        float _bloomStart = -1f;

        void OnEnable()
        {
            Apply(0f, 0f);
            StopAllCoroutines();
            StartCoroutine(Bloom());
        }

        IEnumerator Bloom()
        {
            _bloomStart = Time.time;
            float total = openTime;
            if (delay != null) foreach (var d in delay) total = Mathf.Max(total, d + openTime);
            while (Time.time - _bloomStart < total + 0.1f)
            {
                Apply(Time.time - _bloomStart, 0f);
                yield return null;
            }
            _bloomStart = -1f;
        }

        void Update()
        {
            if (_bloomStart >= 0f || petals == null) return;
            Apply(float.MaxValue, Mathf.Sin(Time.time * 0.9f) * sway);
        }

        void Apply(float elapsed, float extra)
        {
            if (petals == null) return;
            for (int i = 0; i < petals.Length; i++)
            {
                if (!petals[i]) continue;
                float d = delay != null && i < delay.Length ? delay[i] : 0f;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - d) / openTime));
                float lift = Mathf.Lerp(closedLift[i], openLift[i], k) + extra * k * (i % 2 == 0 ? 1f : -1f);
                petals[i].localRotation = Quaternion.Euler(0f, yaw[i], 0f) * Quaternion.Euler(-lift, 0f, 0f);
            }
        }
    }
}
