using System.Collections;
using System.Linq;
using UnityEngine;

namespace SpaceBetweenUs.Tests
{
    /// <summary>
    /// Automated playthrough of the whole 6-step loop in Prototype_Core, driven through the DesktopRigSimulator
    /// (so it exercises real triggers, physics, grabbing, pouring and the flow).
    /// Started from the Editor menu "Space Between Us/Run Core Loop Smoke Test" (or batch mode).
    /// </summary>
    public class CoreLoopSmokeTest : MonoBehaviour
    {
        public float timeScale = 2f;
        public bool exitEditorWhenDone;

        GameFlowManager _flow;
        PerspectiveSwitcher _switcher;
        DesktopRigSimulator _sim;
        Transform _driftTarget;
        int _failures;
        bool _violent;

#if UNITY_EDITOR
        const string RunKey = "SBU_SmokeTest_Run";
        const string ExitKey = "SBU_SmokeTest_Exit";

        public static void Request(bool exitWhenDone)
        {
            UnityEditor.SessionState.SetBool(RunKey, true);
            UnityEditor.SessionState.SetBool(ExitKey, exitWhenDone);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (!UnityEditor.SessionState.GetBool(RunKey, false)) return;
            UnityEditor.SessionState.SetBool(RunKey, false);
            var t = new GameObject("[CoreLoopSmokeTest]").AddComponent<CoreLoopSmokeTest>();
            t.exitEditorWhenDone = UnityEditor.SessionState.GetBool(ExitKey, false);
        }
#endif

        IEnumerator Start()
        {
            Time.timeScale = timeScale;
            float started = Time.realtimeSinceStartup;
            yield return Run();
            Time.timeScale = 1f;
            bool pass = _failures == 0;
            (pass ? (System.Action<object>)Debug.Log : Debug.LogError)(pass
                ? $"[SmokeTest] PASSED - full loop completed in {Time.realtimeSinceStartup - started:F1}s real time"
                : $"[SmokeTest] FAILED with {_failures} failure(s)");
#if UNITY_EDITOR
            if (exitEditorWhenDone) UnityEditor.EditorApplication.Exit(pass ? 0 : 1);
#endif
        }

        void Check(bool ok, string what)
        {
            if (ok) Debug.Log($"[SmokeTest] ok   - {what}");
            // warning, not error: an error would trigger the Console's "Error Pause" and stop the run
            else { _failures++; Debug.LogWarning($"[SmokeTest] FAIL - {what}"); }
        }

        IEnumerator WaitUntil(System.Func<bool> cond, float timeout)
        {
            float end = Time.time + timeout;
            while (!cond() && Time.time < end) yield return null;
        }

        IEnumerator Run()
        {
            _flow = FindAnyObjectByType<GameFlowManager>();
            _switcher = FindAnyObjectByType<PerspectiveSwitcher>();
            _sim = FindAnyObjectByType<DesktopRigSimulator>();
            _driftTarget = GameObject.Find("IslandsDriftTarget")?.transform;
            var islands = FindObjectsByType<Island>(FindObjectsSortMode.None).OrderBy(i => i.name).ToArray();
            var allAsteroids = FindObjectsByType<Asteroid>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var normal = allAsteroids.Where(a => !a.carefulMode).OrderBy(a => a.name).ToArray();
            var careful = allAsteroids.FirstOrDefault(a => a.carefulMode);

            if (!_flow || !_switcher || !_sim || !_driftTarget || islands.Length != 3 || normal.Length != 2 || !careful)
            {
                Check(false, "scene is wired (flow, switcher, simulator, 3 islands, 2+1 asteroids)");
                yield break;
            }
            Check(_sim.Active, "desktop simulator active (no headset)");
            careful.onViolentBreak.AddListener(() => _violent = true);

            yield return new WaitForSeconds(0.5f);

            // ---- Step 1 -> 2
            Check(_flow.Step == 1 && _switcher.Current == Role.Builder, "step 1 starts as Builder");
            yield return SwitchVia(2, Role.Destroyer);

            // ---- Step 2: punch both asteroids open
            foreach (var ast in normal)
            {
                for (int i = 0; i < 6 && ast.LayerIndex < 3; i++)
                    yield return Thrust(ast.transform.position, gentle: false);
                Check(ast.LayerIndex == 3, $"{ast.name} opened (shell > ice > crystal)");
            }
            Check(_flow.AsteroidsOpened == 2 && _flow.GoalDone, "step 2 goal done");
            yield return SwitchVia(3, Role.Builder);

            // ---- Step 3: fragments arrive, build two islands (+ ground & lake on the third)
            yield return WaitUntil(() => FreeFragments().Count(f => Near(f, 1f)) >= 12, 40f);
            Check(FreeFragments().Count(f => Near(f, 1f)) >= 12, $"fragments drifted to the islands ({FreeFragments().Count(f => Near(f, 1f))}/14)");

            yield return TestAttract();

            for (int i = 0; i < 3; i++)
            {
                yield return Deliver(islands[i].groundSlot, MaterialType.Rock);
                yield return Deliver(islands[i].waterSlot, MaterialType.Ice);
                if (i < 2) yield return Deliver(islands[i].lifeSlot, MaterialType.Crystal);
            }
            Check(islands[0].IsAlive && islands[1].IsAlive, "islands 1 and 2 alive");
            Check(_flow.IslandsAlive == 2 && _flow.GoalDone, "step 3 goal done");
            yield return SwitchVia(4, Role.Destroyer);

            // ---- Step 4: the last asteroid frees its crystal (one touch, or gentle taps if requireGentle is on)
            Check(careful.gameObject.activeInHierarchy, "last asteroid appeared next to the islands");
            for (int i = 0; i < 6 && careful.LayerIndex < 3; i++)
                yield return Thrust(careful.transform.position, gentle: true);
            Check(careful.LayerIndex == 3 && careful.OpenedGently && !_violent, $"last asteroid freed its crystal gently ({careful.Taps} tap(s), no violent break)");
            Check(_flow.GoalDone, "step 4 goal done");
            yield return SwitchVia(5, Role.Builder);

            // ---- Step 5: plant the last crystal
            yield return WaitUntil(() => FreeFragments().Any(f => f.type == MaterialType.Crystal && Near(f, 1f)), 30f);
            yield return Deliver(islands[2].lifeSlot, MaterialType.Crystal);
            Check(islands[2].IsAlive, "island 3 alive");

            // ---- Step 6: reveal
            Check(_flow.Step == 6, "step 6 reveal reached");
            yield return new WaitForSeconds(1f);
            Check(GameObject.Find("Thread_0") != null, "light threads drawn");
        }

        // ------------------------------------------------------------ helpers

        IEnumerator SwitchVia(int expectedStep, Role expectedRole)
        {
            yield return new WaitForSeconds(0.3f);
            _sim.TouchArtifact();
            yield return WaitUntil(() => _flow.Step == expectedStep && !_switcher.IsSwitching && _switcher.Current == expectedRole, 12f);
            bool ok = _flow.Step == expectedStep && _switcher.Current == expectedRole;
            Check(ok, $"artifact switch -> step {expectedStep} as {expectedRole}" +
                      (ok ? "" : $" (now step {_flow.Step} as {_switcher.Current}, goal done: {_flow.GoalDone})"));
            if (!ok)
            {
                // later steps depend on this one: force the switch so the rest of the loop still gets tested
                Debug.LogWarning("[SmokeTest] forcing the switch to keep testing");
                _flow.RequestSwitch();
                yield return WaitUntil(() => !_switcher.IsSwitching && _flow.Step == expectedStep, 12f);
            }
            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator Thrust(Vector3 target, bool gentle)
        {
            _sim.AimAt(target);
            yield return null;
            _sim.Punch(gentle);
            yield return WaitUntil(() => !_sim.IsThrusting, 5f);
            yield return new WaitForSeconds(0.25f);
        }

        IEnumerator TestAttract()
        {
            var frag = FreeFragments().FirstOrDefault();
            if (!frag) { Check(false, "attract: no fragment"); yield break; }
            Vector3 handPos = frag.transform.position + Vector3.up * 0.5f;
            float before = Vector3.Distance(frag.transform.position, handPos);
            _sim.RightHandOverride = handPos;
            _sim.SetTrigger(true);
            yield return new WaitForSeconds(1f);
            _sim.SetTrigger(false);
            float after = frag ? Vector3.Distance(frag.transform.position, handPos) : 0f;
            Check(after < before * 0.5f, $"attract pulls fragments to the hand ({before:F2} m -> {after:F2} m)");
            _sim.RightHandOverride = null;
            yield return new WaitForSeconds(0.2f);
        }

        IEnumerator Deliver(IslandSlot slot, MaterialType type)
        {
            string what = $"{slot.transform.parent.name}/{slot.name} <- {type}";
            var frag = FreeFragments().Where(f => f.type == type)
                .OrderBy(f => (f.transform.position - _driftTarget.position).sqrMagnitude).FirstOrDefault();
            if (!frag) { Check(false, $"{what}: no free {type} fragment"); yield break; }

            // grab: keep the hand on the (possibly still drifting) fragment until it is held
            for (int attempt = 0; attempt < 10 && frag && !frag.IsHeld; attempt++)
            {
                _sim.SetGrip(false);
                _sim.RightHandOverride = frag.transform.position;
                yield return new WaitForFixedUpdate();
                _sim.RightHandOverride = frag.transform.position;
                yield return null;
                _sim.SetGrip(true);
                yield return null;
                yield return null;
            }
            if (!frag || !frag.IsHeld) { Check(false, $"{what}: grab failed"); _sim.SetGrip(false); _sim.RightHandOverride = null; yield break; }

            // carry it over the slot
            Vector3 from = frag.transform.position, to = slot.transform.position;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                _sim.RightHandOverride = Vector3.Lerp(from, to, t / 0.6f);
                yield return null;
            }
            _sim.RightHandOverride = to;
            yield return new WaitForFixedUpdate();

            string state = "";
            switch (slot.mode)
            {
                case IslandSlot.Mode.Place:
                    _sim.SetGrip(false);
                    yield return WaitUntil(() => slot.IsFilled, 1f);
                    break;
                case IslandSlot.Mode.Pour:
                    _sim.SetTilt(true);
                    yield return WaitUntil(() => slot.IsFilled, slot.pourTime + 3f);
                    state = slot.DescribeState(frag) + $" handTilt={Vector3.Angle(_sim.rightHand.up, Vector3.up):F0}" +
                        $" fragToHand={(frag ? Vector3.Distance(frag.transform.position, _sim.rightHand.position) : -1f):F2}" +
                        $" handToSlot={Vector3.Distance(_sim.rightHand.position, slot.transform.position):F2}";
                    _sim.SetTilt(false);
                    _sim.SetGrip(false);
                    break;
                case IslandSlot.Mode.HoldStill:
                    yield return WaitUntil(() => slot.IsFilled, slot.holdTime + 3f);
                    state = slot.DescribeState(frag) + $" handTilt={Vector3.Angle(_sim.rightHand.up, Vector3.up):F0}" +
                        $" fragToHand={(frag ? Vector3.Distance(frag.transform.position, _sim.rightHand.position) : -1f):F2}" +
                        $" handToSlot={Vector3.Distance(_sim.rightHand.position, slot.transform.position):F2}";
                    _sim.SetGrip(false);
                    break;
            }
            Check(slot.IsFilled, slot.IsFilled ? what : $"{what}  [{state}]");
            _sim.SetGrip(false);
            _sim.SetTilt(false);
            _sim.RightHandOverride = null;
            yield return new WaitForSeconds(0.3f);
        }

        MaterialFragment[] FreeFragments() =>
            FindObjectsByType<MaterialFragment>(FindObjectsSortMode.None).Where(f => !f.IsHeld).ToArray();

        bool Near(MaterialFragment f, float dist) => (f.transform.position - _driftTarget.position).magnitude < dist;
    }
}
