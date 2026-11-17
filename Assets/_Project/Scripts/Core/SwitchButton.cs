using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Pressing A (right controller) or X (left) does the same as touching the central artifact.
    /// In the Meta XR Simulator the A button is the "B" key.
    /// </summary>
    public class SwitchButton : MonoBehaviour
    {
        public GameFlowManager flow;

        void Update()
        {
            if (flow == null) return;
            if (XRHandInput.PrimaryButtonDown(Hand.Right) || XRHandInput.PrimaryButtonDown(Hand.Left))
                flow.RequestSwitch();
        }
    }
}
