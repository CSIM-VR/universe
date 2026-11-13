using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Builder's grab: press grip near a fragment to hold it, release grip to let go (with the hand's velocity).
    /// A light-weight alternative to the Interaction SDK Grabbable, so the core loop runs with no extra setup.
    /// (You can still use Meta's Grabbable instead: wire its select/unselect to MaterialFragment.OnGrabbed/OnReleased.)
    /// </summary>
    [RequireComponent(typeof(HandVelocityTracker))]
    public class FragmentGrabber : MonoBehaviour
    {
        public Hand hand = Hand.Right;
        [Tooltip("Grab reach around the hand, in metres (hand scale).")]
        public float grabRadius = 0.12f;

        public MaterialFragment Held => _held;

        MaterialFragment _held;
        HandVelocityTracker _velocity;
        bool _wasGripping;
        readonly Collider[] _hits = new Collider[32];

        void Awake() => _velocity = GetComponent<HandVelocityTracker>();

        void Update()
        {
            bool grip = XRHandInput.Grip(hand);
            if (grip && !_wasGripping) TryGrab();
            if (!grip && _wasGripping) Release();
            _wasGripping = grip;
        }

        void TryGrab()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, grabRadius * transform.lossyScale.x, _hits,
                ~0, QueryTriggerInteraction.Ignore);
            MaterialFragment best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var f = _hits[i].GetComponentInParent<MaterialFragment>();
                if (f == null || f.IsHeld) continue;
                float d = (f.transform.position - transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = f; }
            }
            if (best == null) return;
            _held = best;
            _held.Grab(transform);
            XRHandInput.Haptic(hand, 0.4f, 0.08f);
        }

        void Release()
        {
            if (_held != null) _held.Release(_velocity ? _velocity.Velocity : Vector3.zero);
            _held = null;
        }

        void OnDisable()
        {
            Release();
            _wasGripping = false;
        }
    }
}
