using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// The crystal gyroscope in the middle of the world. Touching it with either hand asks the flow to switch VS.
    /// It glows when switching is allowed. Needs a trigger collider; hands need the tag "PlayerHand" or "DestroyerHand".
    /// </summary>
    public class CentralArtifact : MonoBehaviour
    {
        public GameFlowManager flow;
        public Renderer glowRenderer;
        public Color readyColor = new Color(1f, 0.85f, 0.4f);
        public Color lockedColor = new Color(0.3f, 0.3f, 0.4f);
        public float cooldown = 2f;
        public float spinSpeed = 40f;

        float _last = float.NegativeInfinity;   // no cooldown at the start of the game
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        void Update()
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
            if (glowRenderer == null || flow == null) return;
            glowRenderer.material.SetColor(EmissionColor, flow.GoalDone ? readyColor : lockedColor);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!(other.CompareTag("PlayerHand") || other.CompareTag("DestroyerHand"))) return;
            if (Time.time - _last < cooldown) return;
            _last = Time.time;
            if (flow) flow.RequestSwitch();
        }
    }
}
