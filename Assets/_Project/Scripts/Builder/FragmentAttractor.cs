using UnityEngine;
using UnityEngine.XR;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Builder's "Attract": while the trigger is held, nearby fragments float toward this hand.
    /// Uses Unity's built-in XR input, so it works with any XR SDK.
    /// </summary>
    public class FragmentAttractor : MonoBehaviour
    {
        public XRNode hand = XRNode.RightHand;   // LeftHand or RightHand
        public float radius = 3f;
        public float pullStrength = 4f;
        public float stopDistance = 0.15f;
        public ParticleSystem beamEffect;

        readonly Collider[] _hits = new Collider[32];

        bool TriggerHeld()
        {
            var device = InputDevices.GetDeviceAtXRNode(hand);
            return device.isValid
                && device.TryGetFeatureValue(CommonUsages.trigger, out float value)
                && value > 0.5f;
        }

        void FixedUpdate()
        {
            bool pulling = TriggerHeld();

            if (beamEffect != null)
            {
                if (pulling && !beamEffect.isPlaying) beamEffect.Play();
                if (!pulling && beamEffect.isPlaying) beamEffect.Stop();
            }
            if (!pulling) return;

            int n = Physics.OverlapSphereNonAlloc(transform.position, radius, _hits);
            for (int i = 0; i < n; i++)
            {
                var frag = _hits[i].GetComponentInParent<MaterialFragment>();
                if (frag == null || frag.IsHeld) continue;
                var rb = frag.GetComponent<Rigidbody>();
                Vector3 to = transform.position - frag.transform.position;
                if (to.magnitude < stopDistance) { rb.linearVelocity *= 0.8f; continue; }
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, to.normalized * pullStrength, 0.1f);
            }
        }
    }
}