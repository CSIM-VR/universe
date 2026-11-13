using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// A piece of rock, ice or crystal freed from an asteroid.
    /// Floats in zero gravity, then drifts toward the islands so the Builder can find it.
    /// Needs a Rigidbody (useGravity off) and a Collider.
    /// Grabbed by FragmentGrabber, or by Meta's Grabbable (wire select/unselect to OnGrabbed/OnReleased).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MaterialFragment : MonoBehaviour
    {
        public MaterialType type;

        [Header("Drift to the islands")]
        public float driftDelay = 2f;
        public float driftSpeed = 4f;
        [Tooltip("Fragments spread out in a cloud of this radius around the drift target instead of piling up.")]
        public float driftSpread = 0.3f;

        [Header("Damage (punch vs tap twist)")]
        [Tooltip("Above this speed the fragment damages an island it hits.")]
        public float damageSpeed = 6f;

        Rigidbody _rb;
        Transform _driftTarget;
        Vector3 _driftOffset;
        float _launchTime;
        float _attractUntil;
        RigidbodyInterpolation _interpolation;
        Transform _holder;

        public bool IsHeld { get; private set; }
        /// <summary>True once the Builder has held it at least once (slots only accept placed fragments).</summary>
        public bool WasGrabbed { get; private set; }
        public float CurrentSpeed => _rb.linearVelocity.magnitude;

        // For Meta Interaction SDK: Interactable Unity Event Wrapper > When Select -> OnGrabbed(), When Unselect -> OnReleased()
        public void OnGrabbed() { IsHeld = true; WasGrabbed = true; }
        public void OnReleased() { IsHeld = false; _launchTime = Time.time; }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            // never sleep: a sleeping body stops sending OnTriggerStay, so "hold the crystal still" would never finish
            _rb.sleepThreshold = 0f;
        }

        public void Launch(Vector3 velocity, Transform driftTarget)
        {
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = Random.insideUnitSphere * 2f;
            _driftTarget = driftTarget;
            _driftOffset = Random.insideUnitSphere * driftSpread;
            _launchTime = Time.time;
        }

        /// <summary>Held by FragmentGrabber: follows the hand exactly, aligned with it (tilt the hand to pour).</summary>
        public void Grab(Transform hand)
        {
            OnGrabbed();
            // interpolation on a kinematic child would keep overwriting the transform with the body's old pose,
            // so the fragment would lag behind (or stay behind) the hand
            _interpolation = _rb.interpolation;
            _rb.interpolation = RigidbodyInterpolation.None;
            _rb.isKinematic = true;
            _holder = hand;
            transform.SetParent(hand, true);
            SnapToHolder();
        }

        public void Release(Vector3 handVelocity)
        {
            if (!IsHeld) return;
            transform.SetParent(null, true);
            _holder = null;
            _rb.isKinematic = false;
            _rb.interpolation = _interpolation;
            _rb.linearVelocity = handVelocity;
            _rb.angularVelocity = Vector3.zero;
            OnReleased();
        }

        void SnapToHolder()
        {
            if (_holder == null) return;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _rb.position = _holder.position;
            _rb.rotation = _holder.rotation;
        }

        void LateUpdate()
        {
            if (IsHeld) SnapToHolder();
        }

        /// <summary>Called every physics step by FragmentAttractor while the Builder's trigger is held.</summary>
        public void Attract(Vector3 point, float strength, float stopDistance)
        {
            if (IsHeld || _rb.isKinematic) return;
            _attractUntil = Time.time + 0.1f;
            Vector3 to = point - transform.position;
            if (to.magnitude < stopDistance) { _rb.linearVelocity *= 0.8f; return; }
            _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, to.normalized * strength, 0.15f);
        }

        void FixedUpdate()
        {
            if (IsHeld) { SnapToHolder(); _rb.WakeUp(); return; }
            if (_rb.isKinematic || _driftTarget == null) return;
            if (Time.time < _attractUntil) return;               // the Builder is pulling it
            if (Time.time - _launchTime < driftDelay) return;

            // gently steer toward the islands, and slow down on arrival
            Vector3 toTarget = _driftTarget.position + _driftOffset - transform.position;
            Vector3 desired = toTarget.normalized * Mathf.Min(driftSpeed, toTarget.magnitude);
            _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, desired, 0.03f);
            _rb.angularVelocity *= 0.98f;
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.relativeVelocity.magnitude < damageSpeed) return;
            var island = c.collider.GetComponentInParent<Island>();
            if (island != null) island.TakeDamage(c.GetContact(0).point);
        }

        public void Consume() => Destroy(gameObject);
    }
}
