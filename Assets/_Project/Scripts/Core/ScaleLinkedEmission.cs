using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Emits less as the parent shrinks (the ice melting into the lake): full rate at its original size,
    /// nothing at <see cref="goneAt"/> of it. Used by the snowflake sparkles around ice fragments.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ScaleLinkedEmission : MonoBehaviour
    {
        [Range(0f, 1f)] public float goneAt = 0.4f;

        ParticleSystem _ps;
        float _rate, _baseScale;

        void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            _rate = _ps.emission.rateOverTimeMultiplier;
            _baseScale = transform.parent ? transform.parent.localScale.x : 1f;
        }

        void Update()
        {
            if (!transform.parent || _baseScale <= 0f) return;
            float k = Mathf.InverseLerp(goneAt, 1f, transform.parent.localScale.x / _baseScale);
            var em = _ps.emission;
            em.rateOverTimeMultiplier = _rate * k;
        }
    }
}
