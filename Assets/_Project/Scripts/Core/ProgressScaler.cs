using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Scales an object from 0 to its authored size with a 0..1 value.
    /// Wire IslandSlot.onProgress -> SetProgress to make the lake fill up or a ring grow while holding still.
    /// </summary>
    public class ProgressScaler : MonoBehaviour
    {
        public bool hideAtZero = true;
        Vector3 _full;
        bool _init;

        void Awake() => Init();

        void Init()
        {
            if (_init) return;
            _init = true;
            _full = transform.localScale;
            SetProgress(0f);
        }

        public void SetProgress(float p)
        {
            Init();
            p = Mathf.Clamp01(p);
            transform.localScale = _full * p;
            if (hideAtZero) gameObject.SetActive(p > 0.001f);
        }
    }
}
