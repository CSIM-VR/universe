using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// A piece of rock, ice or crystal freed from an asteroid.
    /// Floats in zero gravity, then drifts toward the islands so the Builder can find it.
    /// Needs a Rigidbody (useGravity off) and a Collider. Make it grabbable with the Meta Interaction SDK (Grabbable).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MaterialFragment : MonoBehaviour
    {
        public MaterialType type;

        [Header("Drift to the islands")]
        public float driftDelay = 2f;
        public float driftSpeed = 1.5f;

        [Header("Damage (punch vs tap twist)")]
        [Tooltip("Above this speed the fragment damages an island it hits.")]
        public float damageSpeed = 6f;

        Rigidbody _rb;
        Transform _driftTarget;
        float _launchTime;

        public bool IsHeld { get; set; }
        public float CurrentSpeed => _rb.linearVelocity.magnitude;

        // Wire these in the Inspector (Meta Interaction SDK):
        // Interactable Unity Event Wrapper > When Select -> OnGrabbed(), When Unselect -> OnReleased()
        public void OnGrabbed() => IsHeld = true;
        public void OnReleased() => IsHeld = false;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
        }

        public void Launch(Vector3 velocity, Transform driftTarget)
        {
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = Random.insideUnitSphere * 2f;
            _driftTarget = driftTarget;
            _launchTime = Time.time;
        }

        void FixedUpdate()
        {
            if (IsHeld || _driftTarget == null) return;
            if (Time.time - _launchTime < driftDelay) return;

            // gently steer toward the islands, and slow down on arrival
            Vector3 toTarget = _driftTarget.position - transform.position;
            Vector3 desired = toTarget.normalized * Mathf.Min(driftSpeed, toTarget.magnitude);
            _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, desired, 0.02f);
        }

        void OnCollisionEnter(Collision c)
        {
            if (CurrentSpeed < damageSpeed) return;
            var island = c.collider.GetComponentInParent<Island>();
            if (island != null) island.TakeDamage(c.contacts[0].point);
        }

        public void Consume() => Destroy(gameObject);
    }
}
