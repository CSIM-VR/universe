using UnityEngine;
using UnityEngine.Events;

namespace SpaceBetweenUs
{
    /// <summary>
    /// A dead asteroid with three layers: shell (rock) -> inside (ice) -> core (crystal).
    /// Each strong hit opens one layer and releases fragments of that layer's material.
    ///
    /// "Careful" mode (step 4, the last asteroid next to the islands):
    ///  - a punch faster than maxSafeSpeed shatters it violently (fragments fly fast and can damage islands)
    ///  - gentle taps (between minTapSpeed and maxSafeSpeed) open it slowly; after tapsNeeded taps the crystal drifts out safely.
    ///
    /// Setup: a trigger collider; destroyer hands need a collider + HandVelocityTracker and the tag "DestroyerHand".
    /// Assign one visual GameObject per layer (e.g. pre-fractured meshes).
    /// </summary>
    public class Asteroid : MonoBehaviour
    {
        [Header("Visuals for each state (enable one at a time)")]
        public GameObject shellVisual;
        public GameObject insideVisual;
        public GameObject coreVisual;
        public GameObject brokenVisual;

        [Header("Fragments")]
        public MaterialFragment rockPrefab;
        public MaterialFragment icePrefab;
        public MaterialFragment crystalPrefab;
        public int fragmentsPerLayer = 3;
        public Transform islandsDriftTarget;
        [Tooltip("Fragments spawn inside this radius around the centre (world units).")]
        public float spawnSpread = 0.6f;

        [Header("Punch")]
        public float minBreakSpeed = 1.5f;
        public float fragmentSpeedPerHandSpeed = 1.2f;
        public float hitCooldown = 0.4f;

        [Header("Careful mode (last asteroid)")]
        public bool carefulMode;
        public float minTapSpeed = 0.3f;
        public float maxSafeSpeed = 2.0f;
        public int tapsNeeded = 3;
        [Tooltip("A violent break throws the fragments at these (the islands).")]
        public Transform[] violentTargets;
        public float violentFragmentSpeed = 12f;

        public UnityEvent onOpened;          // all layers released
        public UnityEvent onViolentBreak;    // careful mode failed
        public UnityEvent<int> onTap;        // careful mode: number of gentle taps so far

        public int LayerIndex => _layer;
        public int Taps => _taps;

        int _layer;        // 0 shell, 1 inside, 2 core, 3 broken
        int _taps;
        float _lastHit;

        void Start() => ShowLayer(0);

        public bool IsOpen => _layer >= 3;

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("DestroyerHand")) return;
            var hand = other.GetComponentInParent<HandVelocityTracker>();
            if (hand == null) return;
            bool left = hand.name.ToLowerInvariant().Contains("left");
            ReceiveHit(hand.Speed, hand.Velocity, left ? Hand.Left : Hand.Right);
        }

        /// <summary>A hit with a physical hand speed (m/s). Also used by the button assist (trigger = punch, grip = tap).</summary>
        public bool ReceiveHit(float speed, Vector3 direction, Hand hand)
        {
            if (Time.time - _lastHit < hitCooldown || _layer >= 3) return false;
            _lastHit = Time.time;

            if (carefulMode) HandleCareful(speed, direction);
            else if (speed >= minBreakSpeed) OpenNextLayer(speed, direction);
            else return false;

            // a little kick in the controller that touched it
            XRHandInput.Haptic(hand, speed > maxSafeSpeed ? 1f : 0.5f, 0.12f);
            return true;
        }

        void HandleCareful(float speed, Vector3 dir)
        {
            if (speed > maxSafeSpeed)
            {
                // too much force: everything flies out fast, straight into the islands
                for (int l = _layer; l < 3; l++) Release(LayerMaterial(l), violentFragmentSpeed, dir, true);
                _layer = 3;
                ShowLayer(3);
                onViolentBreak?.Invoke();
                onOpened?.Invoke();
                return;
            }
            if (speed < minTapSpeed) return;

            _taps++;
            onTap?.Invoke(_taps);
            StartCoroutine(Wobble());
            if (_taps >= tapsNeeded)
            {
                // gentle: only the crystal comes out, slowly
                Release(MaterialType.Crystal, 0.3f, dir);
                _layer = 3;
                ShowLayer(3);
                onOpened?.Invoke();
            }
        }

        void OpenNextLayer(float speed, Vector3 dir)
        {
            Release(LayerMaterial(_layer), speed * fragmentSpeedPerHandSpeed, dir);
            _layer++;
            ShowLayer(_layer);
            if (_layer >= 3) onOpened?.Invoke();
        }

        System.Collections.IEnumerator Wobble()
        {
            Vector3 s = transform.localScale;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                transform.localScale = s * (1f + 0.06f * Mathf.Sin(t / 0.3f * Mathf.PI * 3f));
                yield return null;
            }
            transform.localScale = s;
        }

        void Release(MaterialType type, float speed, Vector3 dir, bool aimAtIslands = false)
        {
            var prefab = type switch
            {
                MaterialType.Rock => rockPrefab,
                MaterialType.Ice => icePrefab,
                _ => crystalPrefab
            };
            if (prefab == null) return;

            int count = type == MaterialType.Crystal ? 1 : fragmentsPerLayer;
            for (int i = 0; i < count; i++)
            {
                var f = Instantiate(prefab, transform.position + Random.insideUnitSphere * spawnSpread, Random.rotation);
                Vector3 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Random.onUnitSphere;
                if (aimAtIslands && violentTargets != null && violentTargets.Length > 0)
                {
                    var target = violentTargets[Random.Range(0, violentTargets.Length)];
                    if (target) d = (target.position - f.transform.position).normalized;
                    f.Launch(d * speed, islandsDriftTarget);
                    continue;
                }
                Vector3 v = (d + Random.insideUnitSphere * 0.6f).normalized * speed;
                f.Launch(v, islandsDriftTarget);
            }
        }

        static MaterialType LayerMaterial(int layer) =>
            layer == 0 ? MaterialType.Rock : layer == 1 ? MaterialType.Ice : MaterialType.Crystal;

        void ShowLayer(int layer)
        {
            if (shellVisual) shellVisual.SetActive(layer == 0);
            if (insideVisual) insideVisual.SetActive(layer == 1);
            if (coreVisual) coreVisual.SetActive(layer == 2);
            if (brokenVisual) brokenVisual.SetActive(layer == 3);
        }
    }
}
