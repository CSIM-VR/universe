using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Every few seconds a shooting star streaks across the upper sky, far from the player (a short glowing trail).
    /// One reused 2-point LineRenderer (always a straight streak), so it costs almost nothing on Quest. Silent on purpose (space is quiet).
    /// </summary>
    public class ShootingStars : MonoBehaviour
    {
        public Material trailMaterial;
        public Vector2 interval = new(8f, 15f);
        public Vector2 duration = new(0.7f, 1.2f);
        [Tooltip("Distance from the camera (metres).")]
        public float distance = 350f;
        public float trailWidth = 1.6f;
        [Tooltip("Length of the glowing tail, as a fraction of the whole path.")]
        public float tailLength = 0.35f;
        public Color colour = new(0.85f, 0.92f, 1f, 1f);
        [Tooltip("Degrees left/right of where the player looks that a streak may appear.")]
        public float viewSpread = 50f;
        [Tooltip("Height above the horizon (degrees) where streaks start.")]
        public Vector2 elevation = new(15f, 40f);

        // a straight 2-point line (head + tail) slid along a straight path: never kinks, unlike a TrailRenderer
        LineRenderer _line;
        float _next, _t0, _dur;
        Vector3 _from, _to;
        bool _flying;

        void Start()
        {
            var go = new GameObject("ShootingStarLine");
            go.transform.SetParent(transform, false);
            _line = go.AddComponent<LineRenderer>();
            _line.sharedMaterial = trailMaterial;
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));   // 0 = head
            _line.widthMultiplier = trailWidth;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(colour, 0f), new GradientColorKey(colour, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            _line.colorGradient = g;
            _line.textureMode = LineTextureMode.Stretch;
            _line.numCapVertices = 0;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.enabled = false;
            _next = Time.time + Random.Range(3f, interval.y);
        }

        void Update()
        {
            var cam = Camera.main;
            if (!cam || !_line) return;
            if (!_flying && Time.time >= _next) Launch(cam.transform);
            if (!_flying) return;
            float k = (Time.time - _t0) / _dur;
            if (k >= 1f + tailLength)
            {
                _flying = false;
                _line.enabled = false;
                _next = Time.time + Random.Range(interval.x, interval.y);
                return;
            }
            // head runs 0..1, tail follows; the head stops at the end so the tail catches up and the streak fades out
            float head = Mathf.Min(k, 1f), tail = Mathf.Max(0f, k - tailLength);
            _line.SetPosition(0, Vector3.Lerp(_from, _to, head));
            _line.SetPosition(1, Vector3.Lerp(_from, _to, tail));
            float fade = Mathf.Clamp01(k / 0.1f) * Mathf.Clamp01((1f + tailLength - k) / tailLength);
            _line.widthMultiplier = trailWidth * Mathf.Lerp(0.3f, 1f, fade);
        }

        void Launch(Transform cam)
        {
            // a point in the sky roughly where the player is looking (so they actually see it), then a short slanted
            // straight path across it
            var centre = cam.position;
            var fwd = cam.forward;
            fwd.y = 0f;
            float yaw = fwd.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fwd).eulerAngles.y : 0f;
            yaw += Random.Range(-viewSpread, viewSpread);
            var dir = Quaternion.Euler(-Random.Range(elevation.x, elevation.y), yaw, 0f) * Vector3.forward;
            var start = centre + dir * distance;
            var across = Vector3.Cross(dir, Vector3.up).normalized;
            if (Random.value < 0.5f) across = -across;
            var path = (across + Vector3.down * Random.Range(0.2f, 0.6f)).normalized * distance * Random.Range(0.25f, 0.4f);
            _from = start;
            _to = start + path;
            _dur = Random.Range(duration.x, duration.y);
            _t0 = Time.time;
            _line.SetPosition(0, _from);
            _line.SetPosition(1, _from);
            _line.enabled = true;
            _flying = true;
        }
    }
}
