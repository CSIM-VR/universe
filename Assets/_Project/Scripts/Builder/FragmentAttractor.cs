using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Builder's "Attract": while the index trigger is held, nearby fragments float toward this hand.
    /// Radius and distances are in the hand's own scale (so they still feel right if the rig is scaled).
    /// </summary>
    public class FragmentAttractor : MonoBehaviour
    {
        public Hand hand = Hand.Right;
        public float radius = 3f;
        public float pullStrength = 2.5f;
        public float stopDistance = 0.12f;
        public ParticleSystem beamEffect;

        readonly Collider[] _hits = new Collider[64];

        public bool Pulling { get; private set; }

        void FixedUpdate()
        {
            Pulling = XRHandInput.Trigger(hand);

            if (beamEffect != null)
            {
                if (Pulling && !beamEffect.isPlaying) beamEffect.Play();
                if (!Pulling && beamEffect.isPlaying) beamEffect.Stop();
            }
            if (!Pulling) return;

            float scale = transform.lossyScale.x;
            int n = Physics.OverlapSphereNonAlloc(transform.position, radius * scale, _hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var frag = _hits[i].GetComponentInParent<MaterialFragment>();
                if (frag == null || frag.IsHeld) continue;
                frag.Attract(transform.position, pullStrength * scale, stopDistance * scale);
            }
        }

        void OnDisable() => Pulling = false;
    }
}
