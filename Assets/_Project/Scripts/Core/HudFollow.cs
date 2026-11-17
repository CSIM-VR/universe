using UnityEngine;
using UnityEngine.UI;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Comfortable VR hint panel. Instead of being glued to the head (which makes it jitter with every tiny head
    /// movement), it floats in the world and only glides back in front of the player when they look away by more
    /// than <see cref="deadZone"/> degrees. It is also drawn on top, so hands, fragments and particles passing in
    /// front no longer cut it in and out.
    /// Put it on a world-space canvas that starts as a child of the head (its local position = where it rests).
    /// </summary>
    public class HudFollow : MonoBehaviour
    {
        public Transform head;
        [Tooltip("Degrees the player can look away before the panel follows.")]
        public float deadZone = 22f;
        public float followTime = 0.35f;
        public bool drawOnTop = true;
        [Tooltip("If the panel stays more than this many degrees off-centre for settleDelay seconds, it recentres.")]
        public float settleAngle = 10f;
        public float settleDelay = 1.2f;

        Vector3 _offset;      // resting position in head space (at rig scale 1)
        Vector3 _scale;       // panel scale at rig scale 1
        float _baseRig = 1f;
        Vector3 _vel;
        bool _following;
        float _offCentreFor;
        Vector3 _lastHead;
        float _lastRig;

        // the rig is scaled up for the giant Destroyer: the panel scales and moves out with it
        float Rig => head ? head.lossyScale.x / _baseRig : 1f;

        void Start()
        {
            if (!head) head = transform.parent;
            if (!head) { enabled = false; return; }
            _offset = transform.localPosition;
            _baseRig = head.lossyScale.x;
            _scale = transform.lossyScale;
            transform.SetParent(null, true);
            _lastHead = head.position;
            _lastRig = 1f;
            Snap();
            if (drawOnTop) DrawOnTop();
        }

        void OnEnable() { if (head && transform.parent == null) Snap(); }

        Vector3 Target()
        {
            // follow yaw + pitch of the head but ignore roll, so the text stays level
            var fwd = head.forward;
            var rot = Quaternion.LookRotation(fwd, Vector3.up);
            return head.position + rot * (_offset * Rig);
        }

        void Snap()
        {
            transform.localScale = _scale * Rig;
            transform.position = Target();
            _vel = Vector3.zero;
            _following = false;
            Face();
        }

        void Face()
        {
            var dir = transform.position - head.position;
            if (dir.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        void LateUpdate()
        {
            if (!head) return;
            // switched perspective (rig teleported / rescaled): jump straight to the new view
            float rig = Rig;
            if (Mathf.Abs(rig - _lastRig) > 0.01f || (head.position - _lastHead).magnitude > 0.5f * rig)
                Snap();
            _lastRig = rig;
            _lastHead = head.position;

            Vector3 target = Target();
            // how far the panel is from where it should rest (it rests a bit below eye level)
            float angle = Vector3.Angle(target - head.position, transform.position - head.position);
            _offCentreFor = angle > settleAngle ? _offCentreFor + Time.deltaTime : 0f;
            if (angle > deadZone || _offCentreFor > settleDelay) _following = true;
            if (_following)
            {
                transform.position = Vector3.SmoothDamp(transform.position, target, ref _vel, followTime);
                if ((transform.position - target).sqrMagnitude < 0.0004f * rig * rig) { _following = false; _offCentreFor = 0f; }
            }
            else
            {
                // keep the distance right when the player walks, without chasing small head turns
                var d = transform.position - head.position;
                transform.position = head.position + d.normalized * (_offset.magnitude * rig);
            }
            Face();
        }

        void DrawOnTop()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas) canvas.sortingOrder = 100;
            foreach (var g in GetComponentsInChildren<Graphic>(true))
            {
                var mat = new Material(g.materialForRendering ? g.materialForRendering : Canvas.GetDefaultCanvasMaterial());
                mat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                mat.renderQueue = 4000;
                g.material = mat;
            }
        }
    }
}
