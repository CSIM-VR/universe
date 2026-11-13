using UnityEngine;
using UnityEngine.UI;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Prototype HUD: a small text in front of the eyes telling the player the goal of the current step.
    /// Replace with diegetic hints (glows, sounds) later.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class FlowHints : MonoBehaviour
    {
        public GameFlowManager flow;
        public PerspectiveSwitcher switcher;
        public DesktopRigSimulator simulator;

        Text _text;

        void Awake() => _text = GetComponent<Text>();

        void Update()
        {
            if (flow == null) return;
            string body = flow.Step switch
            {
                1 => "BUILDER\nThe islands are barren. There is nothing to build with.\nTouch the glowing artifact (low, to your right) to switch.",
                2 => $"DESTROYER\nPunch the two asteroids open: shell (rock) > inside (ice) > core (crystal).\nOpened {flow.AsteroidsOpened}/2",
                3 => $"BUILDER\nTrigger = attract fragments, grip = grab.\nRock: drop on the gold disc.  Ice: hold over the lake and tilt.\nCrystal: hold still over the lake centre.\nIslands alive {flow.IslandsAlive}/2  (also give island 3 ground + lake)",
                4 => "DESTROYER\nThe last asteroid floats right above the islands.\nTap it GENTLY 3 times. A hard punch smashes the islands!",
                5 => $"BUILDER\nPlant the last crystal in island 3 (ground + lake first).\nIslands alive {flow.IslandsAlive}/3",
                _ => "Were we ever truly separate?"
            };
            if (flow.GoalDone && flow.Step < 6) body += "\n\n>> Goal done - touch the artifact to switch <<";
            if (simulator != null && simulator.Active && flow.Step < 6)
                body += switcher != null && switcher.Current == Role.Destroyer
                    ? "\n\n[desktop] RMB+mouse look | LMB punch | F gentle tap | Tab touch artifact"
                    : "\n\n[desktop] RMB+mouse look | WASD/QE move | Space attract | LMB grab/release\n R tilt (pour) | wheel reach | Tab touch artifact";
            _text.text = body;
        }
    }
}
