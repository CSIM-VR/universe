using UnityEngine;
using UnityEngine.Events;

namespace SpaceBetweenUs
{
    /// <summary>
    /// The six-step flow. The central artifact only lets the user switch when the current step's goal is done.
    ///  1 Builder   - islands barren (goal: look around, then touch the artifact)
    ///  2 Destroyer - open the two normal asteroids
    ///  3 Builder   - bring islands 1 and 2 to life
    ///  4 Destroyer - open the last asteroid (careful mode)
    ///  5 Builder   - plant the last crystal, island 3 comes alive
    ///  6 Reveal    - both views overlap, closing line
    /// Wire the UnityEvents in the Inspector: Asteroid.onOpened -> AsteroidOpened(), Island.onAlive -> IslandAlive().
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public PerspectiveSwitcher switcher;
        public int normalAsteroids = 2;

        [Header("Events per step (show hints, play sounds, enable objects)")]
        public UnityEvent[] onStepStarted = new UnityEvent[7];   // index = step number
        public UnityEvent onReveal;

        public int Step { get; private set; } = 1;
        public bool GoalDone { get; private set; } = true;   // step 1: free to switch

        int _opened, _alive;

        public int AsteroidsOpened => _opened;
        public int IslandsAlive => _alive;
        /// <summary>How many islands a violent break ruined (they must be rebuilt).</summary>
        public int IslandsLost { get; private set; }

        void Start() => StartStep(1);

        /// <summary>Called by the central artifact when touched.</summary>
        public void RequestSwitch()
        {
            if (!GoalDone || Step >= 6) return;
            if (switcher == null || switcher.IsSwitching) return;
            switcher.Switch();
            StartStep(Step + 1);
        }

        void StartStep(int step)
        {
            Step = step;
            GoalDone = step == 1;
            if (onStepStarted != null && step < onStepStarted.Length) onStepStarted[step]?.Invoke();
            if (step == 6) onReveal?.Invoke();
            Debug.Log($"[Flow] Step {step} started");
        }

        public void AsteroidOpened()
        {
            _opened++;
            if (Step == 2 && _opened >= normalAsteroids) GoalDone = true;
            if (Step == 4) GoalDone = true;
        }

        /// <summary>Island.onLost: an alive island was ruined by the violent break in step 4.</summary>
        public void IslandLost()
        {
            _alive = Mathf.Max(0, _alive - 1);
            IslandsLost++;
        }

        public void IslandAlive()
        {
            _alive++;
            if (Step == 3 && _alive >= 2) GoalDone = true;
            if (Step == 5 && _alive >= 3) { GoalDone = true; StartStep(6); }
        }
    }
}
