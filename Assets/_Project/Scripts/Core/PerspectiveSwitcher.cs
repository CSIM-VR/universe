using UnityEngine;
using UnityEngine.Events;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Switches the single XR rig between the two Virtual Subjectivenesses:
    ///  - Destroyer: rig scaled up (asteroids feel fist-sized), blue hands, x-ray layer visible, at the destroyer anchor
    ///  - Builder:   normal scale, golden hands, no x-ray, at the builder anchor
    /// Uses a short fade so the switch is comfortable.
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

        public UnityEvent<Role> onSwitched;
        public Role Current { get; private set; } = Role.Builder;
        public bool IsSwitching { get; private set; }

        /// <summary>Move where the Destroyer appears (step 4: next to the islands). Takes effect on the next switch.</summary>
        public void SetDestroyerAnchor(Transform anchor) => destroyerAnchor = anchor;

        void Start() => Apply(Role.Builder);

        public void Switch()
        {
            if (IsSwitching) return;
            StartCoroutine(SwitchRoutine(Current == Role.Builder ? Role.Destroyer : Role.Builder));
        }

        System.Collections.IEnumerator SwitchRoutine(Role next)
        {
            IsSwitching = true;
            yield return Fade(0f, 1f);
            Apply(next);
            yield return Fade(1f, 0f);
            IsSwitching = false;
            onSwitched?.Invoke(next);
        }

        void Apply(Role role)
        {
            Current = role;
            bool destroyer = role == Role.Destroyer;

            Transform anchor = destroyer ? destroyerAnchor : builderAnchor;
            if (anchor) xrOrigin.SetPositionAndRotation(anchor.position, anchor.rotation);
            xrOrigin.localScale = Vector3.one * (destroyer ? destroyerScale : 1f);

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
