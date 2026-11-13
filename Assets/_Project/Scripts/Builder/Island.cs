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
    /// </summary>
    public class Island : MonoBehaviour
    {
        public IslandSlot groundSlot;
        public IslandSlot waterSlot;
        public IslandSlot lifeSlot;
        public LifeGrowth life;
        public float waterLifeDelay = 4f;

        [Header("Damage (violent break in step 4)")]
        public GameObject[] breakableParts;   // e.g. the bridge, one lotus
        public ParticleSystem damageEffect;

        public UnityEvent onAlive;
        public bool IsAlive { get; private set; }

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

        public void TakeDamage(Vector3 point)
        {
            if (damageEffect) { damageEffect.transform.position = point; damageEffect.Play(); }
            foreach (var part in breakableParts)
            {
                if (part != null && part.activeSelf) { part.SetActive(false); break; } // break one thing per hit
            }
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
