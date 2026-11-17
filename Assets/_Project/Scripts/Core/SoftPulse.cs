using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>Slowly breathes a renderer's colour (used by the soft slot halos). No material instances are created.</summary>
    [RequireComponent(typeof(Renderer))]
    public class SoftPulse : MonoBehaviour
    {
        public Color color = Color.white;
        [Range(0f, 1f)] public float depth = 0.35f;   // how much it dims at the low point
        public float period = 2.4f;                   // seconds per breath
        public float spin = 8f;                       // degrees per second around the up axis

        Renderer _renderer;
        MaterialPropertyBlock _block;
        float _offset;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            _offset = Random.value * period;
        }

        void Update()
        {
            float k = 1f - depth * (0.5f + 0.5f * Mathf.Sin((Time.time + _offset) / period * Mathf.PI * 2f));
            _block.SetColor(BaseColor, color * k);
            _renderer.SetPropertyBlock(_block);
            if (spin != 0f) transform.Rotate(0f, 0f, spin * Time.deltaTime, Space.Self);
        }
    }
}
