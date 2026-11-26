using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace SpaceBetweenUs
{
    /// <summary>
    /// The three-step flow. The central artifact (or A / B / X / Y) only switches when the current step's goal is done.
    ///  1 Destroyer - you start as the giant: punch the three asteroids open to free the materials
    ///                (each gives exactly 1 rock, 1 ice, 1 crystal = one island's worth)
    ///  2 Builder   - bring all three islands to life
    ///  3 Reveal    - starts by itself as soon as the third island is alive: both views overlap, closing line
    /// Wire the UnityEvents in the Inspector: Asteroid.onOpened -> AsteroidOpened(), Island.onAlive -> IslandAlive().
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public const int RevealStep = 3;

        public PerspectiveSwitcher switcher;
        [FormerlySerializedAs("normalAsteroids")]
        public int asteroidsToOpen = 3;
        public int islandsToBuild = 3;

        [Header("Events per step (show hints, play sounds, enable objects)")]
        public UnityEvent[] onStepStarted = new UnityEvent[RevealStep + 1];   // index = step number
        public UnityEvent onReveal;

        public int Step { get; private set; } = 1;
        public bool GoalDone { get; private set; }

        int _opened, _alive;

        public int AsteroidsOpened => _opened;
        public int IslandsAlive => _alive;
        public bool Revealed => Step >= RevealStep;

        void Start() => StartStep(1);

        /// <summary>Called by the central artifact when touched (and by the A / B / X / Y buttons).</summary>
        public void RequestSwitch()
        {
            if (!GoalDone || Step >= 2) { Debug.Log($"[Flow] Switch refused: step {Step}, goal done = {GoalDone}"); return; }
            if (switcher == null || switcher.IsSwitching) { Debug.Log("[Flow] Switch refused: already switching"); return; }
            switcher.Switch();
            StartStep(Step + 1);
        }

        void StartStep(int step)
        {
            Step = step;
            GoalDone = false;
            if (onStepStarted != null && step < onStepStarted.Length) onStepStarted[step]?.Invoke();
            if (step == RevealStep) onReveal?.Invoke();
            Debug.Log($"[Flow] Step {step} started");
        }

        public void AsteroidOpened()
        {
            _opened++;
            if (Step == 1 && _opened >= asteroidsToOpen) GoalDone = true;
        }

        /// <summary>Island.onLost (only the old violent break used it).</summary>
        public void IslandLost() => _alive = Mathf.Max(0, _alive - 1);

        public void IslandAlive()
        {
            _alive++;
            if (Step == 2 && _alive >= islandsToBuild) { GoalDone = true; StartStep(RevealStep); }
        }
    }
}
