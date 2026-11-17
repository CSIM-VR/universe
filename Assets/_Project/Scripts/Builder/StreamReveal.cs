using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// The stream + waterfall only runs once the island's lake is full: watches the lake's scale
    /// (ProgressScaler) and lets the water run along the stream bed (FlowingWater _Fill), or dry up again.
    /// </summary>
    public class StreamReveal : MonoBehaviour
    {
        public Transform water;
        [Tooltip("Lake x scale when full (authored size).")]
        public float fullScale = 0.24f;
        public float runTime = 2.5f;

        static readonly int FillId = Shader.PropertyToID("_Fill");
        Renderer _r;
        MaterialPropertyBlock _mpb;
        float _fill = -1f;

        void Awake()
        {
            _r = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (!_r) return;
            bool full = water && water.gameObject.activeInHierarchy && water.localScale.x >= fullScale * 0.95f;
            float target = full ? 1f : 0f;
            float next = _fill < 0f ? 0f : Mathf.MoveTowards(_fill, target, Time.deltaTime / runTime);
            if (Mathf.Approximately(next, _fill)) return;
            _fill = next;
            _r.enabled = _fill > 0.001f;
            _r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FillId, _fill);
            _r.SetPropertyBlock(_mpb);
        }
    }
}
