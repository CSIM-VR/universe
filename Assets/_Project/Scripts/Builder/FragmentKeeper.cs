using System.Collections.Generic;
using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// The asteroids give exactly the materials the three islands need (3 rock, 3 ice, 3 crystal), so one lost
    /// fragment would make the game impossible to finish. While the Builder is building this:
    ///  - brings back any free fragment that stayed too far from the gathering point (flew off, got stuck), and
    ///  - spawns a new one at the gathering point when fewer fragments of a material exist than empty slots need it.
    /// Added at runtime; needs no setup (prefabs and gathering point are read from the islands).
    /// </summary>
    public class FragmentKeeper : MonoBehaviour
    {
        [Tooltip("A free fragment farther than this from the gathering point (metres, Builder scale) counts as lost.")]
        public float maxDistance = 2.5f;
        [Tooltip("Seconds a fragment must stay that far before it is brought back.")]
        public float lostAfter = 6f;
        [Tooltip("Seconds after the Builder step starts before missing fragments are replaced (they may still be drifting in).")]
        public float graceTime = 8f;
        public float checkInterval = 1f;

        GameFlowManager _flow;
        PerspectiveSwitcher _switcher;
        Island[] _islands;
        Transform _target;
        float _builderSince = -1f, _nextCheck;
        readonly Dictionary<MaterialFragment, float> _farSince = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void AddToScene()
        {
            var flow = FindAnyObjectByType<GameFlowManager>();
            if (flow && !flow.GetComponent<FragmentKeeper>()) flow.gameObject.AddComponent<FragmentKeeper>();
        }

        void Start()
        {
            _flow = GetComponent<GameFlowManager>();
            _switcher = FindAnyObjectByType<PerspectiveSwitcher>();
            _islands = FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var i in _islands) if (i.driftTarget) { _target = i.driftTarget; break; }
            if (!_target) _target = GameObject.Find("IslandsDriftTarget")?.transform;
        }

        void Update()
        {
            if (!_flow || !_target || _flow.Step != 2 || (_switcher && _switcher.IsSwitching)) { _builderSince = -1f; return; }
            if (_builderSince < 0f) _builderSince = Time.time;
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + checkInterval;

            var all = FindObjectsByType<MaterialFragment>(FindObjectsSortMode.None);
            BringBackLost(all);
            if (Time.time - _builderSince >= graceTime) ReplaceMissing(all);
        }

        void BringBackLost(MaterialFragment[] all)
        {
            float now = Time.time;
            var seen = new HashSet<MaterialFragment>();
            foreach (var f in all)
            {
                seen.Add(f);
                bool far = !f.IsHeld && (f.transform.position - _target.position).magnitude > maxDistance;
                if (!far) { _farSince.Remove(f); continue; }
                if (!_farSince.TryGetValue(f, out float since)) { _farSince[f] = now; continue; }
                if (now - since < lostAfter) continue;
                _farSince.Remove(f);
                f.transform.position = _target.position + Random.insideUnitSphere * 0.15f;
                f.Launch(Vector3.zero, _target);
                Debug.Log($"[FragmentKeeper] {f.type} fragment was lost, brought back to the islands");
            }
            // forget destroyed fragments
            var gone = new List<MaterialFragment>();
            foreach (var k in _farSince.Keys) if (!k || !seen.Contains(k)) gone.Add(k);
            foreach (var k in gone) _farSince.Remove(k);
        }

        void ReplaceMissing(MaterialFragment[] all)
        {
            foreach (MaterialType type in System.Enum.GetValues(typeof(MaterialType)))
            {
                int needed = 0;
                MaterialFragment prefab = null;
                foreach (var island in _islands)
                {
                    var slot = SlotFor(island, type);
                    if (slot && !slot.IsFilled) needed++;
                    if (!prefab) prefab = type == MaterialType.Rock ? island.rockPrefab : type == MaterialType.Ice ? island.icePrefab : island.crystalPrefab;
                }
                int have = 0;
                foreach (var f in all) if (f.type == type) have++;
                for (int i = have; i < needed && prefab; i++)
                {
                    var f = Instantiate(prefab, _target.position + Random.insideUnitSphere * 0.15f, Random.rotation);
                    f.Launch(Vector3.zero, _target);
                    Debug.Log($"[FragmentKeeper] a {type} fragment was missing ({have}/{needed}), made a new one");
                }
            }
        }

        static IslandSlot SlotFor(Island island, MaterialType type)
        {
            foreach (var s in new[] { island.groundSlot, island.waterSlot, island.lifeSlot })
                if (s && s.accepts == type) return s;
            return null;
        }
    }
}
