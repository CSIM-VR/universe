using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Button alternative to punching (handy in the Meta XR Simulator, where a real fast punch is hard to fake,
    /// and as an accessibility option on Quest). Put on each Destroyer hand:
    ///   trigger near an asteroid = a strong punch,  grip near an asteroid = a gentle tap.
    /// "Near" = within assistReach of the asteroid's surface (rig-scale metres).
    /// </summary>
    public class DestroyerButtonAssist : MonoBehaviour
    {
        public Hand hand = Hand.Right;
        public float assistReach = 0.6f;
        public float punchSpeed = 3f;   // counts as a hard punch (opens a layer / violent in careful mode)
        public float tapSpeed = 0.8f;   // counts as a gentle tap

        void Update()
        {
            bool punch = XRHandInput.TriggerDown(hand);
            bool tap = XRHandInput.GripDown(hand);
            if (!punch && !tap) return;

            Asteroid best = null;
            float bestGap = assistReach * transform.lossyScale.x;
            foreach (var a in FindObjectsByType<Asteroid>(FindObjectsSortMode.None))
            {
                if (a.IsOpen) continue;
                var col = a.GetComponent<SphereCollider>();
                float radius = col ? col.radius * a.transform.lossyScale.x : 0f;
                float gap = Vector3.Distance(a.transform.position, transform.position) - radius;
                if (gap < bestGap) { bestGap = gap; best = a; }
            }
            if (best == null) return;

            Vector3 dir = best.transform.position - transform.position;
            best.ReceiveHit(punch ? punchSpeed : tapSpeed, dir, hand);
        }
    }
}
