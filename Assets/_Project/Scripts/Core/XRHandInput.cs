using UnityEngine;
using UnityEngine.XR;

namespace SpaceBetweenUs
{
    public enum Hand { Left, Right }

    /// <summary>
    /// One place to read the controller buttons the game uses (index trigger = attract, grip = grab, face buttons =
    /// switch). On a headset every input is read two ways, OVRInput (Meta XR Core SDK) and Unity's XR input devices
    /// (OpenXR controller profile), so a controller OVRInput does not report (seen with Quest 3 Touch Plus) still
    /// works. When DesktopRigSimulator is active it returns the simulated values instead, so the whole loop can be
    /// played in the Editor without a headset.
    /// </summary>
    public static class XRHandInput
    {
        public static bool SimulationActive;

        static readonly bool[] SimTrigger = new bool[2];
        static readonly bool[] SimGrip = new bool[2];

        public static void SetSimulated(Hand hand, bool trigger, bool grip)
        {
            SimTrigger[(int)hand] = trigger;
            SimGrip[(int)hand] = grip;
        }

        public static bool Trigger(Hand hand)
        {
            if (SimulationActive) return SimTrigger[(int)hand];
            Poll();
            return OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, Controller(hand)) > 0.5f || XR[(int)hand].trigger;
        }

        public static bool Grip(Hand hand)
        {
            if (SimulationActive) return SimGrip[(int)hand];
            Poll();
            return OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, Controller(hand)) > 0.5f || XR[(int)hand].grip;
        }

        /// <summary>A / X button pressed this frame (always false in desktop simulation).</summary>
        public static bool PrimaryButtonDown(Hand hand)
        {
            if (SimulationActive) return false;
            Poll();
            return OVRInput.GetDown(OVRInput.Button.One, Controller(hand)) || XR[(int)hand].primaryDown;
        }

        /// <summary>Any face button (A / B on the right, X / Y on the left) pressed this frame.</summary>
        public static bool FaceButtonDown(Hand hand)
        {
            if (SimulationActive) return false;
            Poll();
            var c = Controller(hand);
            return OVRInput.GetDown(OVRInput.Button.One, c) || OVRInput.GetDown(OVRInput.Button.Two, c) ||
                   XR[(int)hand].faceDown;
        }

        /// <summary>Menu button (left controller, the one with three lines) held down. False in desktop simulation.</summary>
        public static bool MenuHeld()
        {
            if (SimulationActive) return false;
            if (OVRInput.Get(OVRInput.Button.Start)) return true;
            var dev = Device(Hand.Left);
            return dev.isValid && dev.TryGetFeatureValue(CommonUsages.menuButton, out bool m) && m;
        }

        public static bool TriggerDown(Hand hand)
        {
            if (SimulationActive) return false;
            Poll();
            return OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, Controller(hand)) || XR[(int)hand].triggerDown;
        }

        public static bool GripDown(Hand hand)
        {
            if (SimulationActive) return false;
            Poll();
            return OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, Controller(hand)) || XR[(int)hand].gripDown;
        }

        public static void Haptic(Hand hand, float amplitude, float seconds)
        {
            if (SimulationActive) return;
            OVRInput.SetControllerVibration(1f, amplitude, Controller(hand));
            var dev = Device(hand);
            if (dev.isValid) dev.SendHapticImpulse(0, Mathf.Clamp01(amplitude), seconds);
            HapticStopper.StopAfter(hand, seconds);
        }

        /// <summary>The Unity XR controller device for a hand (invalid if none).</summary>
        public static InputDevice Device(Hand hand) =>
            InputDevices.GetDeviceAtXRNode(hand == Hand.Left ? XRNode.LeftHand : XRNode.RightHand);

        static OVRInput.Controller Controller(Hand hand) =>
            hand == Hand.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

        // ---------- Unity XR input, polled once per frame with edge detection ----------

        struct XRState
        {
            public bool trigger, grip, primary, face;
            public bool triggerDown, gripDown, primaryDown, faceDown;
        }

        static readonly XRState[] XR = new XRState[2];
        static int _polledFrame = -1;

        static void Poll()
        {
            if (_polledFrame == Time.frameCount) return;
            _polledFrame = Time.frameCount;
            for (int i = 0; i < 2; i++)
            {
                var dev = Device((Hand)i);
                bool trig = false, grip = false, a = false, b = false;
                if (dev.isValid)
                {
                    if (dev.TryGetFeatureValue(CommonUsages.trigger, out float t)) trig = t > 0.5f;
                    if (dev.TryGetFeatureValue(CommonUsages.grip, out float g)) grip = g > 0.5f;
                    dev.TryGetFeatureValue(CommonUsages.primaryButton, out a);
                    dev.TryGetFeatureValue(CommonUsages.secondaryButton, out b);
                }
                ref var s = ref XR[i];
                s.triggerDown = trig && !s.trigger;
                s.gripDown = grip && !s.grip;
                s.primaryDown = a && !s.primary;
                s.faceDown = (a || b) && !s.face;
                s.trigger = trig;
                s.grip = grip;
                s.primary = a;
                s.face = a || b;
            }
        }

        /// <summary>Tiny helper that turns the vibration off again.</summary>
        class HapticStopper : MonoBehaviour
        {
            static HapticStopper _instance;
            readonly float[] _stopAt = new float[2];

            public static void StopAfter(Hand hand, float seconds)
            {
                if (_instance == null)
                {
                    var go = new GameObject("[Haptics]") { hideFlags = HideFlags.HideAndDontSave };
                    _instance = go.AddComponent<HapticStopper>();
                }
                _instance._stopAt[(int)hand] = Time.unscaledTime + seconds;
            }

            void Update()
            {
                for (int i = 0; i < 2; i++)
                {
                    if (_stopAt[i] <= 0f || Time.unscaledTime < _stopAt[i]) continue;
                    _stopAt[i] = 0f;
                    OVRInput.SetControllerVibration(0f, 0f, Controller((Hand)i));
                }
            }
        }
    }
}
