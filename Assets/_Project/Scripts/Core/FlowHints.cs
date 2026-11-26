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

        // colours: role headers and the three materials (same as the hands and the slot rings)
        const string Gold = "#FFC94A", Blue = "#5AA0FF", Grey = "#8A8F99";
        const string RockC = "#FFC94A", IceC = "#6FD8FF", CrystalC = "#C08BFF";
        const string Big = "<size=34>";   // replaced by 1.5 x the font size

        Island[] _islands;

        static string C(string colour, string s) => $"<color={colour}>{s}</color>";
        static string Label(string s) => C(Grey, s.PadRight(6));

        /// <summary>One dot per thing: filled in its colour when done, a grey ring when not.</summary>
        static string Dot(bool done, string colour) => done ? C(colour, "\u25CF") : C(Grey, "\u25CB");

        string Destroyer()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"{Big}<b>{C(Blue, "DESTROYER")}</b></size>   {C(Grey, "step 1 of 2  \u00B7  you are the giant")}\n\n");
            sb.Append($"{Label("GOAL")}Free the materials for the islands\n");
            sb.Append($"{Label("HOW")}Punch each asteroid 3 times:\n");
            sb.Append($"{Label("")}{C(RockC, "rock")}  \u2192  {C(IceC, "ice")}  \u2192  {C(CrystalC, "crystal")}\n");
            sb.Append($"{Label("DONE")}");
            for (int i = 0; i < flow.asteroidsToOpen; i++) sb.Append(Dot(i < flow.AsteroidsOpened, Blue)).Append(' ');
            sb.Append($"  {flow.AsteroidsOpened} / {flow.asteroidsToOpen} asteroids");
            return sb.ToString();
        }

        string Builder()
        {
            if (_islands == null || _islands.Length == 0)
            {
                _islands = FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                System.Array.Sort(_islands, (x, y) => string.CompareOrdinal(x.name, y.name));
            }
            var sb = new System.Text.StringBuilder();
            sb.Append($"{Big}<b>{C(Gold, "BUILDER")}</b></size>   {C(Grey, "step 2 of 2  \u00B7  you are small again")}\n\n");
            sb.Append($"{Label("GOAL")}Bring the 3 islands to life\n");
            sb.Append($"{Label("HOW")}Give each island, in this order:\n");
            sb.Append($"{Label("")}1  {C(RockC, "Rock")}  \u2192  gold ring  (ground)\n");
            sb.Append($"{Label("")}2  {C(IceC, "Ice")}  \u2192  blue ring  (lake, tilt to pour)\n");
            sb.Append($"{Label("")}3  {C(CrystalC, "Crystal")}  \u2192  violet ring  (after the lake)\n");
            sb.Append($"{Label("DONE")}");
            for (int i = 0; i < _islands.Length; i++)
            {
                var isl = _islands[i];
                if (!isl) continue;
                if (i > 0) sb.Append("   ");
                sb.Append(isl.IsAlive ? C(Gold, $"Island {i + 1}") : $"Island {i + 1}").Append(' ');
                sb.Append(Dot(isl.groundSlot && isl.groundSlot.IsFilled, RockC));
                sb.Append(Dot(isl.waterSlot && isl.waterSlot.IsFilled, IceC));
                sb.Append(Dot(isl.lifeSlot && isl.lifeSlot.IsFilled, CrystalC));
            }
            return sb.ToString();
        }

        void Update()
        {
            if (flow == null) return;
            bool destroyerNow = switcher != null && switcher.Current == Role.Destroyer;
            string body = flow.Step switch
            {
                1 => Destroyer(),
                2 => Builder(),
                _ => $"{Big}<b>Were we ever truly separate?</b></size>"
            };
            bool sizing = switcher != null && switcher.IsSwitching && switcher.xrOrigin;
            if (sizing)
            {
                // while the size changes: show it, and tell the player where to look
                int x = Mathf.RoundToInt(switcher.xrOrigin.localScale.x);
                body = destroyerNow
                    ? $"{Big}<b>{C(Blue, $"GROWING  x{x}")}</b></size>\n\nYou rise up to the asteroids.\nLook down: the islands stay at your feet."
                    : $"{Big}<b>{C(Gold, $"SHRINKING  x{x}")}</b></size>\n\nYou sink back down to the islands.\nThe materials are drifting to them.";
            }
            else if (!flow.Revealed)
            {
                if (flow.GoalDone)
                    body += $"\n\n<b>{C(Gold, "\u25BA  NEXT   Press A / B to become the Builder")}</b>\n{Label("")}{C(Gold, "(or touch the gold gem)")}";
                string keys;
                if (simulator != null && simulator.Active)
                    keys = destroyerNow
                        ? "Right-drag look  \u00B7  Left click punch\nF gentle tap  \u00B7  Tab gem"
                        : "Right-drag look  \u00B7  WASD/QE move\nSpace pull  \u00B7  Left click grab  \u00B7  R tilt (pour)\nWheel reach  \u00B7  Tab gem";
                else
                    keys = destroyerNow
                        ? "TRIGGER punch  \u00B7  GRIP gentle tap\nA / B switch  \u00B7  hold MENU restart"
                        : "TRIGGER pull  \u00B7  GRIP grab / let go  \u00B7  tilt to pour\nA / B switch  \u00B7  hold MENU restart";
                body += $"\n\n{Label("KEYS")}{C(Grey, keys.Replace("\n", "\n" + "      "))}";
            }
            if (body == _last) return;
            _last = body;
            if (mesh)
            {
                mesh.text = Wrap(body.Replace(Big, "<size=" + Mathf.RoundToInt(mesh.fontSize * 1.5f) + ">"), wrapChars);
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
