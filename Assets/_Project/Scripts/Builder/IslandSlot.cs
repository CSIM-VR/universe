using UnityEngine;
using UnityEngine.Events;

namespace SpaceBetweenUs
{
    /// <summary>
    /// A glowing spot on an island that accepts one material, in one of three ways:
    ///  - Place     (Rock):    drop the rock inside the slot -> ground
    ///  - Pour      (Ice):     hold the ice above the lake bed, tilted > pourAngle, for pourTime -> water
    ///  - HoldStill (Crystal): keep the crystal in the slot, moving slower than stillSpeed, for holdTime -> lotus
    /// Needs a trigger collider sized to the area where the action counts.
    /// </summary>
    public class IslandSlot : MonoBehaviour
    {
        public enum Mode { Place, Pour, HoldStill }

        public MaterialType accepts = MaterialType.Rock;
        public Mode mode = Mode.Place;

        [Header("Pour (ice)")]
        public float pourAngle = 45f;
        public float pourTime = 3f;
        public ParticleSystem pourEffect;

        [Header("Hold still (crystal)")]
        public float holdTime = 2f;
        public float stillSpeed = 0.1f;   // metres per second

        [Header("Feedback")]
        public GameObject glowWhileEmpty;
        public UnityEvent<float> onProgress;   // 0..1, e.g. drive a ring or the lake's water level
        public UnityEvent onFilled;

        public bool IsFilled { get; private set; }
        float _progress;
        Vector3 _lastPos;
        MaterialFragment _tracked;

        public float Progress => _progress;

        // Place: a fragment released inside the slot (trigger event is enough)
        void OnTriggerStay(Collider other)
        {
            if (IsFilled || mode != Mode.Place) return;
            var frag = other.GetComponentInParent<MaterialFragment>();
            if (frag != null && frag.type == accepts && !frag.IsHeld && frag.WasGrabbed) Fill(frag);
        }

        // Pour / HoldStill: actively look for a HELD fragment every physics step.
        // (Doesn't rely on OnTriggerStay, and fragments drifting through the slot are ignored.)
        readonly Collider[] _hits = new Collider[64];
        int _dbgSteps, _dbgSeenHeld, _dbgHits;
        float _dbgMaxTilt;
        BoxCollider _box;

        void FixedUpdate()
        {
            if (IsFilled || mode == Mode.Place) return;
            if (_box == null) _box = GetComponent<BoxCollider>();
            if (_box == null) return;

            MaterialFragment held = null;
            Vector3 half = Vector3.Scale(_box.size * 0.5f, transform.lossyScale);
            int n = Physics.OverlapBoxNonAlloc(transform.TransformPoint(_box.center), half, _hits, transform.rotation,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n && held == null; i++)
            {
                var f = _hits[i].GetComponentInParent<MaterialFragment>();
                if (f != null && f.IsHeld && f.type == accepts) held = f;
            }

            float dt = Time.fixedDeltaTime;
            _dbgSteps++;
            if (held != null)
            {
                _dbgSeenHeld++;
                _dbgMaxTilt = Mathf.Max(_dbgMaxTilt, Vector3.Angle(held.transform.up, Vector3.up));
            }
            _dbgHits = n;
            if (mode == Mode.Pour)
            {
                bool pouring = held != null && Vector3.Angle(held.transform.up, Vector3.up) > pourAngle;
                SetPourEffect(pouring, pouring ? held.transform.position : Vector3.zero);
                if (pouring) Advance(dt / pourTime, held);
                return;
            }

            // HoldStill: measure movement from position change (works whatever the grab's movement type is)
            if (held == null || held != _tracked)
            {
                _tracked = held;
                if (held != null) _lastPos = held.transform.position;
                if (_progress > 0f) { _progress = 0f; onProgress?.Invoke(0f); }
                return;
            }
            float moved = (held.transform.position - _lastPos).magnitude / dt;
            _lastPos = held.transform.position;
            if (moved < stillSpeed * transform.lossyScale.x) Advance(dt / holdTime, held);
            else if (_progress > 0f) { _progress = 0f; onProgress?.Invoke(0f); }
        }

        /// <summary>Why isn't it filling? (used by the smoke test when a step fails)</summary>
        public string DescribeState(MaterialFragment frag)
        {
            if (_box == null) _box = GetComponent<BoxCollider>();
            Vector3 local = frag ? transform.InverseTransformPoint(frag.transform.position) - (_box ? _box.center : Vector3.zero) : Vector3.zero;
            bool inside = _box && Mathf.Abs(local.x) <= _box.size.x / 2 && Mathf.Abs(local.y) <= _box.size.y / 2 && Mathf.Abs(local.z) <= _box.size.z / 2;
            return $"active={gameObject.activeInHierarchy} progress={_progress:F2} fragAlive={(bool)frag} " +
                   (frag ? $"held={frag.IsHeld} insideBox={inside} tilt={Vector3.Angle(frag.transform.up, Vector3.up):F0} " : "") +
                   $"physicsSteps={_dbgSteps} sawHeld={_dbgSeenHeld} maxTilt={_dbgMaxTilt:F0} overlaps={_dbgHits}";
        }

        void Advance(float amount, MaterialFragment frag)
        {
            _progress = Mathf.Clamp01(_progress + amount);
            onProgress?.Invoke(_progress);
            if (_progress >= 1f) Fill(frag);
        }

        void Fill(MaterialFragment frag)
        {
            IsFilled = true;
            SetPourEffect(false, Vector3.zero);
            if (glowWhileEmpty) glowWhileEmpty.SetActive(false);
            frag.Consume();
            onFilled?.Invoke();
        }

        void SetPourEffect(bool on, Vector3 pos)
        {
            if (pourEffect == null) return;
            if (on) { pourEffect.transform.position = pos; if (!pourEffect.isPlaying) pourEffect.Play(); }
            else if (pourEffect.isPlaying) pourEffect.Stop();
        }
    }
}
