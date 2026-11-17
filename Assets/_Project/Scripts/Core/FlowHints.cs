using UnityEngine;
using UnityEngine.UI;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Prototype HUD: a small text in front of the eyes telling the player the goal of the current step.
    /// Replace with diegetic hints (glows, sounds) later.
    /// </summary>
    public class FlowHints : MonoBehaviour
    {
        public GameFlowManager flow;
        public PerspectiveSwitcher switcher;
        public DesktopRigSimulator simulator;
        [Tooltip("3D text used instead of the Canvas Text (Canvas text flickers in the headset). Set by Apply Imported Art.")]
        public TextMesh mesh;
        [Tooltip("Height of one line of text in metres (TextMesh only).")]
        public float lineHeight = 0.02f;
        public int wrapChars = 58;

        Text _text;
        string _last;
        MeshRenderer _mr;

        void Awake()
        {
            _text = GetComponent<Text>();
            if (!mesh) return;
            if (_text) _text.enabled = false;
            _mr = mesh.GetComponent<MeshRenderer>();
            if (_mr) _mr.material = new Material(_mr.sharedMaterial);   // own copy: the font atlas is set on it at runtime
            // calibrate: make one line lineHeight metres tall whatever the font size
            mesh.text = "Hg";
            mesh.characterSize = 1f;
            float h = _mr ? _mr.localBounds.size.y : 0f;
            var ls = mesh.transform.lossyScale.y;
            if (h > 1e-5f && ls > 1e-7f) mesh.characterSize = lineHeight / (h * ls);
        }

        void Update()
        {
            if (flow == null) return;
            const string builder = "<size=34><b><color=#FFC94A>YOU ARE THE BUILDER</color></b></size>  <color=#FFC94A>(gold hand)</color>\n";
            const string destroyer = "<size=34><b><color=#5AA0FF>YOU ARE THE DESTROYER</color></b></size>  <color=#5AA0FF>(blue hand, giant)</color>\n";
            string body = flow.Step switch
            {
                1 => builder + "The islands below are barren. Look UP: the asteroids are in the sky.\nTouch the glowing gold gem (low, to your right) to become the Destroyer.",
                2 => destroyer + $"Punch the two asteroids open: shell (rock) > inside (ice) > core (crystal).\nOpened {flow.AsteroidsOpened}/2",
                3 => builder + $"Trigger = attract fragments, grip = grab.\nRock: let go near the gold ring.  Ice: let go in the cyan ring (or hold + tilt to pour faster).\nCrystal: let go in the violet ring once there is water.\nIslands alive {flow.IslandsAlive}/2  (also give island 3 ground + lake)",
                4 => destroyer + "The last asteroid floats right above the islands.\nTap it GENTLY 3 times. A hard punch smashes the islands!",
                5 => builder + $"Plant the last crystal in island 3 (ground + lake first).\nIslands alive {flow.IslandsAlive}/3",
                _ => "<size=34><b>Were we ever truly separate?</b></size>"
            };
            if (flow.GoalDone && flow.Step < 6) body += "\n\n>> Goal done - touch the artifact to switch <<";
            if (simulator != null && simulator.Active && flow.Step < 6)
                body += switcher != null && switcher.Current == Role.Destroyer
                    ? "\n\n[desktop] RMB+drag look | LEFT CLICK = punch (aim at an asteroid) | F gentle tap | Tab touch gem"
                    : "\n\n[desktop] RMB+drag look | WASD/QE move | Space attract | LEFT CLICK grab/release\n R tilt (pour) | wheel reach | Tab touch gem";
            else if (flow.Step < 6)
                body += switcher != null && switcher.Current == Role.Destroyer
                    ? "\n\n[controller] punch the asteroid, or TRIGGER near it = punch, GRIP = gentle tap | A = switch"
                    : "\n\n[controller] TRIGGER = attract | GRIP = grab | tilt to pour | A = switch";
            if (body == _last) return;
            _last = body;
            if (mesh)
            {
                mesh.text = Wrap(body.Replace("<size=34>", "<size=" + Mathf.RoundToInt(mesh.fontSize * 1.5f) + ">"), wrapChars);
                // the dynamic font atlas can be rebuilt (new texture) when new characters are added
                if (_mr && mesh.font) _mr.material.mainTexture = mesh.font.material.mainTexture;
            }
            else if (_text) _text.text = body;
        }

        /// <summary>TextMesh does not wrap: break lines at spaces, counting only visible characters (not rich-text tags).</summary>
        static string Wrap(string s, int max)
        {
            var sb = new System.Text.StringBuilder(s.Length + 16);
            int col = 0, lastSpace = -1, colAtSpace = 0;
            bool tag = false;
            foreach (char c in s)
            {
                sb.Append(c);
                if (c == '<') { tag = true; continue; }
                if (tag) { if (c == '>') tag = false; continue; }
                if (c == '\n') { col = 0; lastSpace = -1; continue; }
                col++;
                if (c == ' ') { lastSpace = sb.Length - 1; colAtSpace = col; }
                if (col > max && lastSpace >= 0)
                {
                    sb[lastSpace] = '\n';
                    col -= colAtSpace;
                    lastSpace = -1;
                }
            }
            return sb.ToString();
        }
    }
}
