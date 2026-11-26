using UnityEngine;
using UnityEngine.Events;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Switches the single XR rig between the two Virtual Subjectivenesses:
    ///  - Destroyer: rig scaled up (asteroids feel fist-sized), blue hands, x-ray layer visible, at the destroyer anchor
    ///  - Builder:   normal scale, golden hands, no x-ray, at the builder anchor
    ///
    /// Builder and Destroyer share ONE space: the Destroyer is the Builder grown x20 where they stand. The switch has
    /// no cut and no teleport:
    ///  - Builder -> Destroyer: your hands turn blue and you GROW in place (x1 -> x20 around your feet). You rise
    ///    straight up past the islands, which stay at your feet as a tiny model, and arrive at arm's reach of the
    ///    asteroids that were floating high above you.
    ///  - Destroyer -> Builder: the exact reverse: you sink back down from the asteroids and land among the islands.
    /// If you moved while in one role, the rig glides back to the other role's anchor during the same motion.
    /// The scale follows a log curve (steady doubling), no rotation unless the anchors differ. Untick growTransition for
    /// the old fade.
    /// </summary>
    public class PerspectiveSwitcher : MonoBehaviour
    {
        public Transform xrOrigin;            // the OVRCameraRig root
        public Camera xrCamera;

        [Header("Anchors (where the user appears)")]
        public Transform builderAnchor;
        public Transform destroyerAnchor;

        [Header("Scale")]
        public float destroyerScale = 20f;

        [Header("Hands and abilities (enable per role)")]
        public GameObject[] builderOnly;      // golden hands, FragmentAttractor, builder locomotion
        public GameObject[] destroyerOnly;    // blue hands with "DestroyerHand" colliders, push-off locomotion

        [Header("X-ray")]
        [Tooltip("Layer holding the glowing veins inside asteroids; only the Destroyer sees it.")]
        public string xrayLayerName = "XRay";

        [Header("Fade")]
        public CanvasGroup fadeOverlay;       // a black full-screen canvas in front of the camera
        public float fadeTime = 0.6f;

        [Header("Size change you can see")]
        public bool growTransition = true;
        [Tooltip("Seconds to grow from 1x to the Destroyer scale (and to shrink back).")]
        public float growTime = 3f;

        [Tooltip("The role the game starts in (the flow starts as the Destroyer).")]
        public Role startRole = Role.Destroyer;

        public UnityEvent<Role> onSwitched;
        /// <summary>Raised at the very start of a switch, while the role you are leaving is still fully shown.</summary>
        public event System.Action<Role> BeforeSwitch;
        public Role Current { get; private set; } = Role.Builder;
        public bool IsSwitching { get; private set; }

        /// <summary>Move where the Destroyer appears (e.g. next to the islands). Takes effect on the next switch.</summary>
        public void SetDestroyerAnchor(Transform anchor) => destroyerAnchor = anchor;

        void Awake() => Current = startRole;
        void Start() => Apply(startRole);

        public void Switch()
        {
            if (IsSwitching) return;
            StartCoroutine(SwitchRoutine(Current == Role.Builder ? Role.Destroyer : Role.Builder));
        }

        System.Collections.IEnumerator SwitchRoutine(Role next)
        {
            IsSwitching = true;
            BeforeSwitch?.Invoke(Current);
            if (!growTransition || !xrOrigin)
            {
                yield return Fade(0f, 1f);
                Apply(next);
                yield return Fade(1f, 0f);
            }
            else
            {
                // become the other role first (hands, x-ray), then grow / shrink on the spot
                ApplyRole(next);
                bool destroyer = next == Role.Destroyer;
                yield return ScaleMove(destroyer ? destroyerScale : 1f, destroyer ? destroyerAnchor : builderAnchor);
            }
            IsSwitching = false;
            onSwitched?.Invoke(next);
        }

        void Place(Transform anchor) => xrOrigin.SetPositionAndRotation(anchor.position, anchor.rotation);

        /// <summary>Scales the rig around its own origin (your feet) while gliding it to the anchor, if you moved.</summary>
        System.Collections.IEnumerator ScaleMove(float target, Transform anchor)
        {
            float from = Mathf.Max(0.001f, xrOrigin.localScale.x);
            Vector3 p0 = xrOrigin.position, p1 = anchor ? anchor.position : p0;
            Quaternion r0 = xrOrigin.rotation, r1 = anchor ? anchor.rotation : r0;
            float a = Mathf.Log(from), b = Mathf.Log(Mathf.Max(0.001f, target));
            for (float t = 0f; t < growTime; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / growTime);
                xrOrigin.localScale = Vector3.one * Mathf.Exp(Mathf.Lerp(a, b, k));
                xrOrigin.SetPositionAndRotation(Vector3.Lerp(p0, p1, k), Quaternion.Slerp(r0, r1, k));
                yield return null;
            }
            xrOrigin.localScale = Vector3.one * target;
            xrOrigin.SetPositionAndRotation(p1, r1);
        }

        void Apply(Role role)
        {
            bool destroyer = role == Role.Destroyer;
            Transform anchor = destroyer ? destroyerAnchor : builderAnchor;
            if (anchor) Place(anchor);
            xrOrigin.localScale = Vector3.one * (destroyer ? destroyerScale : 1f);
            ApplyRole(role);
        }

        /// <summary>Hands, abilities and x-ray for a role (no movement or scale).</summary>
        void ApplyRole(Role role)
        {
            Current = role;
            bool destroyer = role == Role.Destroyer;

            if (builderOnly != null) foreach (var g in builderOnly) if (g) g.SetActive(!destroyer);
            if (destroyerOnly != null) foreach (var g in destroyerOnly) if (g) g.SetActive(destroyer);

            int xray = LayerMask.NameToLayer(xrayLayerName);
            if (xray >= 0 && xrCamera)
            {
                if (destroyer) xrCamera.cullingMask |= 1 << xray;
                else xrCamera.cullingMask &= ~(1 << xray);
            }
        }

        System.Collections.IEnumerator Fade(float from, float to)
        {
            if (fadeOverlay == null) yield break;
            for (float t = 0f; t < fadeTime; t += Time.deltaTime)
            {
                fadeOverlay.alpha = Mathf.Lerp(from, to, t / fadeTime);
                yield return null;
            }
            fadeOverlay.alpha = to;
        }
    }
}
