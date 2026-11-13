using UnityEngine;

namespace SpaceBetweenUs
{
    public enum Hand { Left, Right }

    /// <summary>
    /// One place to read the controller buttons the game uses (index trigger = attract, grip = grab).
    /// On Quest it reads OVRInput (Meta XR Core SDK). When DesktopRigSimulator is active it returns the
    /// simulated values instead, so the whole loop can be played in the Editor without a headset.
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

        public static bool Trigger(Hand hand) => SimulationActive
            ? SimTrigger[(int)hand]
            : OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, Controller(hand)) > 0.5f;

        public static bool Grip(Hand hand) => SimulationActive
            ? SimGrip[(int)hand]
            : OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, Controller(hand)) > 0.5f;

        public static void Haptic(Hand hand, float amplitude, float seconds)
        {
            if (SimulationActive) return;
            OVRInput.SetControllerVibration(1f, amplitude, Controller(hand));
            HapticStopper.StopAfter(hand, seconds);
        }

        static OVRInput.Controller Controller(Hand hand) =>
            hand == Hand.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

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
