using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Keeps the hands on the real controllers. OVRCameraRig places Left/RightHandAnchor from OVRInput; when OVRInput
    /// does not report a controller (seen on Quest 3 with Touch Plus) it puts the anchor at the tracking-space origin,
    /// so the hands lie on the floor. Right after OVRCameraRig updates, this places such an anchor from Unity's XR
    /// controller pose instead (same tracking space). Also logs what it sees a few times after start, so a device
    /// build can be checked with: adb logcat -s Unity | grep "\[Rig\]".
    /// Added at runtime to the OVRCameraRig; needs no setup.
    /// </summary>
    public class ControllerAnchorFallback : MonoBehaviour
    {
        OVRCameraRig _rig;
        readonly bool[] _usingFallback = new bool[2];
        float _nextLog = 3f;
        int _logs;
        string _lastKey;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void AddToScene()
        {
            var rig = FindAnyObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
            if (rig && !rig.GetComponent<ControllerAnchorFallback>()) rig.gameObject.AddComponent<ControllerAnchorFallback>();
        }

        void OnEnable()
        {
            _rig = GetComponent<OVRCameraRig>();
            if (_rig) _rig.UpdatedAnchors += Fix;
        }

        void OnDisable()
        {
            if (_rig) _rig.UpdatedAnchors -= Fix;
        }

        void Fix(OVRCameraRig rig)
        {
            if (XRHandInput.SimulationActive) return;
            Place(Hand.Left, rig.leftHandAnchor, OVRInput.Controller.LTouch, OVRInput.Controller.LHand);
            Place(Hand.Right, rig.rightHandAnchor, OVRInput.Controller.RTouch, OVRInput.Controller.RHand);
        }

        void Place(Hand hand, Transform anchor, OVRInput.Controller touch, OVRInput.Controller handCtrl)
        {
            if (!anchor) return;
            bool ovrHas = OVRInput.GetControllerPositionValid(touch) || OVRInput.GetControllerPositionValid(handCtrl);
            bool use = false;
            if (!ovrHas)
            {
                var dev = XRHandInput.Device(hand);
                if (dev.isValid &&
                    dev.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p) &&
                    dev.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion r) &&
                    (!dev.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || tracked))
                {
                    anchor.localPosition = p;
                    anchor.localRotation = r;
                    use = true;
                }
            }
            if (use != _usingFallback[(int)hand])
            {
                _usingFallback[(int)hand] = use;
                Debug.Log($"[Rig] {hand} hand now follows {(use ? "Unity XR controller pose (OVRInput has none)" : "OVRInput")}");
            }
        }

        void Update()
        {
            // log a few times after start, and again whenever the connected controllers change
            var key = OVRInput.GetConnectedControllers() + "|" + XRHandInput.Device(Hand.Left).isValid + XRHandInput.Device(Hand.Right).isValid;
            bool changed = key != _lastKey;
            _lastKey = key;
            if (!changed && (_logs >= 4 || Time.time < _nextLog)) return;
            if (!changed) _logs++;
            _nextLog = Time.time + 8f;
            var sb = new StringBuilder("[Rig] ");
            sb.Append($"xrActive={XRSettings.isDeviceActive} device='{XRSettings.loadedDeviceName}' sim={XRHandInput.SimulationActive} ");
            sb.Append($"ovrConnected={OVRInput.GetConnectedControllers()} ovrActive={OVRInput.GetActiveController()} ");
            var list = new List<InputDevice>();
            InputDevices.GetDevices(list);
            sb.Append("xrDevices=[");
            foreach (var d in list) sb.Append($"{d.name} ({d.characteristics}); ");
            sb.Append("] ");
            if (_rig)
                sb.Append($"leftAnchor={_rig.leftHandAnchor.localPosition} rightAnchor={_rig.rightHandAnchor.localPosition} " +
                          $"head={_rig.centerEyeAnchor.localPosition} rigEnabled={_rig.enabled}");
            Debug.Log(sb.ToString());
        }
    }
}
