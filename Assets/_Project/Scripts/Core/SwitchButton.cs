using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Pressing any face button (A / B on the right controller, X / Y on the left) does the same as touching the
    /// central artifact. In the Meta XR Simulator the A button is the "B" key.
    /// </summary>
    public class SwitchButton : MonoBehaviour
    {
        public GameFlowManager flow;

        void Update()
        {
            if (flow == null) return;
            bool right = XRHandInput.FaceButtonDown(Hand.Right), left = XRHandInput.FaceButtonDown(Hand.Left);
            if (!right && !left) return;
            Debug.Log($"[SwitchButton] {(right ? "right" : "left")} face button pressed");
            flow.RequestSwitch();
        }
    }
}
