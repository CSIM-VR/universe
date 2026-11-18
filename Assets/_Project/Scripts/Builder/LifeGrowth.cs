using System.Collections;
using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Plays one stage of life on an island: objects grow from scale 0 to their full size,
    /// one after another, and particle systems (fireflies, fish, pollen) start.
    /// Stage examples: Moss (after ground), Plants (after water), WaterLife (auto), Lotus (after crystal), Animals (all islands alive).
    /// Put all objects of a stage under one parent, deactivated at start.
    /// </summary>
    public class LifeGrowth : MonoBehaviour
    {
        [System.Serializable]
        public class Stage
        {
            public string name = "Moss";
            public Transform[] growObjects;
            public ParticleSystem[] effects;
            public float delayBetween = 0.25f;
            public float growDuration = 1.5f;
            public AudioSource sound;
        }

        public Stage[] stages;

        // authored (full) size of every grow object, so a regrow after Wither() reaches the right size
        readonly System.Collections.Generic.Dictionary<Transform, Vector3> _full = new();

        void Awake()
        {
            if (stages == null) return;
            foreach (var s in stages)
                if (s.growObjects != null)
                    foreach (var t in s.growObjects)
                        if (t && !_full.ContainsKey(t)) _full[t] = t.localScale == Vector3.zero ? Vector3.one : t.localScale;
        }

        /// <summary>Everything that has grown shrinks away (island ruined). Stages can be played again afterwards.</summary>
        public void Wither(float duration = 1.2f)
        {
            StopAllCoroutines();
            foreach (var s in stages)
            {
                foreach (var fx in s.effects) if (fx) fx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                foreach (var t in s.growObjects)
                    if (t && t.gameObject.activeSelf) StartCoroutine(ShrinkAway(t, duration * Random.Range(0.6f, 1f)));
            }
        }

        IEnumerator ShrinkAway(Transform t, float duration)
        {
            Vector3 from = t.localScale;
            for (float e = 0f; e < duration; e += Time.deltaTime)
            {
                t.localScale = from * (1f - Mathf.SmoothStep(0f, 1f, e / duration));
                yield return null;
            }
            t.gameObject.SetActive(false);
            t.localScale = _full.TryGetValue(t, out var full) ? full : from;
        }

        public void PlayStage(int index)
        {
            if (index < 0 || index >= stages.Length) return;
            StartCoroutine(Grow(stages[index]));
        }

        public void PlayStageByName(string stageName)
        {
            for (int i = 0; i < stages.Length; i++)
                if (stages[i].name == stageName) { PlayStage(i); return; }
        }

        IEnumerator Grow(Stage s)
        {
            if (s.sound) s.sound.Play();
            foreach (var fx in s.effects) if (fx) fx.Play();

            foreach (var t in s.growObjects)
            {
                if (t == null) continue;
                StartCoroutine(ScaleUp(t, s.growDuration));
                yield return new WaitForSeconds(s.delayBetween);
            }
        }

        IEnumerator ScaleUp(Transform t, float duration)
        {
            Vector3 target = _full.TryGetValue(t, out var full) ? full : t.localScale == Vector3.zero ? Vector3.one : t.localScale;
            t.localScale = Vector3.zero;
            t.gameObject.SetActive(true);
            for (float e = 0f; e < duration; e += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / duration);
                t.localScale = target * k;
                yield return null;
            }
            t.localScale = target;
        }
    }
}
