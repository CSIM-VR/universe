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

        void OnTriggerStay(Collider other)
        {
            if (IsFilled) return;
            var frag = other.GetComponentInParent<MaterialFragment>();
            if (frag == null || frag.type != accepts) return;

            switch (mode)
            {
                case Mode.Place:
                    if (!frag.IsHeld) Fill(frag);   // released inside the slot
                    break;

                case Mode.Pour:
                    float tilt = Vector3.Angle(frag.transform.up, Vector3.up);
                    bool pouring = frag.IsHeld && tilt > pourAngle;
                    SetPourEffect(pouring, frag.transform.position);
                    if (pouring) Advance(Time.deltaTime / pourTime, frag);
                    break;

                case Mode.HoldStill:
                    // measure movement from position change (works whatever the grab's movement type is)
                    float moved = (frag.transform.position - _lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                    _lastPos = frag.transform.position;
                    if (frag.IsHeld && moved < stillSpeed) Advance(Time.deltaTime / holdTime, frag);
                    else if (_progress > 0f) { _progress = 0f; onProgress?.Invoke(0f); }
                    break;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (mode == Mode.Pour) SetPourEffect(false, Vector3.zero);
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
