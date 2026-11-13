using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Put on each hand object. Tracks a smoothed hand velocity.
    /// Speed is in REAL-WORLD m/s: when the rig is scaled up (Destroyer x20), world movement is divided by the
    /// hand's scale, so "punch faster than 1.5 m/s" means the same physical punch in both VSs.
    /// </summary>
    public class HandVelocityTracker : MonoBehaviour
    {
        [Tooltip("Higher = smoother but slower to react.")]
        [Range(0f, 0.95f)] public float smoothing = 0.6f;

        /// <summary>World-space velocity (use for direction).</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>Physical hand speed in m/s, independent of rig scale.</summary>
        public float Speed => Velocity.magnitude / Mathf.Max(transform.lossyScale.x, 0.0001f);

        Vector3 _lastPosition;

        void OnEnable()
        {
            _lastPosition = transform.position;
            Velocity = Vector3.zero;
        }

        void Update()
        {
            if (Time.deltaTime <= 0f) return;
            Vector3 raw = (transform.position - _lastPosition) / Time.deltaTime;
            Velocity = Vector3.Lerp(raw, Velocity, smoothing);
            _lastPosition = transform.position;
        }
    }
}
