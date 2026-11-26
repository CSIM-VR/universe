using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace SpaceBetweenUs
{
    /// <summary>
    /// One floating island. Listens to its three slots and runs the life succession:
    ///   ground filled  -> "Moss"
    ///   water filled   -> "Plants", then after waterLifeDelay -> "WaterLife"
    ///   crystal filled -> "Lotus"   (only after ground and water)
    /// When all three are filled the island is alive.
    ///
    /// A fast fragment (thrown hard, or the optional violent break) RUINS the island it hits: life withers, the three slots are empty
    /// again, and the island's rock / ice / crystal burst back out as fragments, so the Builder can rebuild it.
    /// </summary>
    public class Island : MonoBehaviour
    {
        public IslandSlot groundSlot;
        public IslandSlot waterSlot;
        public IslandSlot lifeSlot;
        public LifeGrowth life;
        public float waterLifeDelay = 4f;

        [Header("Ruin (hit by a fast fragment)")]
        public ParticleSystem damageEffect;
        [Tooltip("Spawned back out of a ruined island, one per filled slot (set by the scene builder).")]
        public MaterialFragment rockPrefab, icePrefab, crystalPrefab;
        [Tooltip("Where the scattered fragments drift to afterwards (the Builder's gathering point).")]
        public Transform driftTarget;
        public float scatterSpeed = 0.6f;
        [Tooltip("Ignore further hits for this long after a ruin (several fragments arrive together).")]
        public float ruinCooldown = 1.5f;

        public UnityEvent onAlive;
        [Tooltip("Was alive and got ruined -> GameFlowManager.IslandLost()")]
        public UnityEvent onLost;
        public bool IsAlive { get; private set; }
        public bool WasRuined { get; private set; }

        float _lastRuin = -99f;

        void OnEnable()
        {
            if (groundSlot) groundSlot.onFilled.AddListener(OnGround);
            if (waterSlot) waterSlot.onFilled.AddListener(OnWater);
            if (lifeSlot) lifeSlot.onFilled.AddListener(OnLife);
            if (lifeSlot && !(waterSlot && waterSlot.IsFilled))
                lifeSlot.gameObject.SetActive(false); // crystal slot appears once there is water
        }

        void OnDisable()
        {
            if (groundSlot) groundSlot.onFilled.RemoveListener(OnGround);
            if (waterSlot) waterSlot.onFilled.RemoveListener(OnWater);
            if (lifeSlot) lifeSlot.onFilled.RemoveListener(OnLife);
        }

        void OnGround() { life?.PlayStageByName("Moss"); CheckAlive(); }

        void OnWater()
        {
            life?.PlayStageByName("Plants");
            StartCoroutine(Delayed("WaterLife", waterLifeDelay));
            if (lifeSlot) lifeSlot.gameObject.SetActive(true);
            CheckAlive();
        }

        void OnLife() { life?.PlayStageByName("Lotus"); CheckAlive(); }

        IEnumerator Delayed(string stage, float delay)
        {
            yield return new WaitForSeconds(delay);
            life?.PlayStageByName(stage);
        }

        void CheckAlive()
        {
            if (IsAlive) return;
            if (groundSlot && groundSlot.IsFilled && waterSlot && waterSlot.IsFilled && lifeSlot && lifeSlot.IsFilled)
            {
                IsAlive = true;
                onAlive?.Invoke();
            }
        }

        /// <summary>Hit by a fast fragment (violent break). Ruins the island if anything was built on it.</summary>
        public void TakeDamage(Vector3 point)
        {
            Burst(point, 30);
            if (Time.time - _lastRuin < ruinCooldown) return;
            bool ground = groundSlot && groundSlot.IsFilled;
            bool water = waterSlot && waterSlot.IsFilled;
            bool lotus = lifeSlot && lifeSlot.IsFilled;
            if (!ground && !water && !lotus) return;   // nothing built yet: just dust
            _lastRuin = Time.time;
            WasRuined = true;

            StopAllCoroutines();                       // a pending "WaterLife" stage must not regrow
            life?.Wither();
            if (groundSlot) groundSlot.ResetSlot();
            if (waterSlot) waterSlot.ResetSlot();
            if (lifeSlot) { lifeSlot.ResetSlot(); lifeSlot.gameObject.SetActive(false); }   // back once there is water

            // what the island was made of bursts back out
            if (ground) Scatter(rockPrefab, point);
            if (water) Scatter(icePrefab, point);
            if (lotus) Scatter(crystalPrefab, point);
            Burst(point, 120);
            StartCoroutine(Shake());

            if (IsAlive) { IsAlive = false; onLost?.Invoke(); }
            Debug.Log($"[Island] {name} ruined by a violent break (ground={ground} water={water} lotus={lotus})");
        }

        void Scatter(MaterialFragment prefab, Vector3 point)
        {
            if (!prefab) return;
            float s = transform.lossyScale.x;
            var pos = transform.position + Vector3.up * 0.12f * s + Random.insideUnitSphere * 0.08f * s;
            var f = Instantiate(prefab, pos, Random.rotation);
            var dir = (pos - point + Vector3.up * 0.1f * s).normalized;
            f.Launch((dir + Random.insideUnitSphere * 0.4f).normalized * scatterSpeed * s, driftTarget);
        }

        void Burst(Vector3 point, int count)
        {
            if (!damageEffect) return;
            damageEffect.transform.position = point;
            damageEffect.Emit(count);
        }

        IEnumerator Shake()
        {
            Vector3 home = transform.position;
            float amp = 0.03f * transform.lossyScale.x;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                transform.position = home + Random.insideUnitSphere * amp * (1f - t / 0.8f);
                yield return null;
            }
            transform.position = home;
        }

        /// <summary>Lets life grow on its own (used when seeds drift across a bridge to island 2).</summary>
        public void PlayAll()
        {
            life?.PlayStageByName("Moss");
            life?.PlayStageByName("Plants");
            StartCoroutine(Delayed("WaterLife", waterLifeDelay));
        }
    }
}
