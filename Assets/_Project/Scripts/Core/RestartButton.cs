using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Hold the menu button (left controller, three lines) for <see cref="holdTime"/> seconds to restart the game from
    /// step 1: reloads the scene. A light buzz when the hold starts, a strong one when it restarts. In the Editor
    /// without a headset: hold Backspace. Lives outside the scenes (created at start), and after a reload adds the
    /// helpers that are normally added once at start (critters, deer, role echo, controller fallback).
    /// </summary>
    public class RestartButton : MonoBehaviour
    {
        public float holdTime = 2f;

        float _held;
        bool _fired;
        static bool _created;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (_created) return;
            _created = true;
            var go = new GameObject("[RestartButton]");
            DontDestroyOnLoad(go);
            go.AddComponent<RestartButton>();
            SceneManager.sceneLoaded += (_, mode) => { if (mode == LoadSceneMode.Single) AddRuntimeHelpers(); };
        }

        static void AddRuntimeHelpers()
        {
            ControllerAnchorFallback.AddToScene();
            RoleEcho.AddToScene();
            Critters.AddToIslands();
            GlowDeer.AddToScene();
            FragmentKeeper.AddToScene();
        }

        void Update()
        {
            bool held = XRHandInput.MenuHeld() || DesktopKey();
            if (!held) { _held = 0f; _fired = false; return; }
            if (_fired) return;
            if (_held == 0f) XRHandInput.Haptic(Hand.Left, 0.25f, 0.08f);
            _held += Time.unscaledDeltaTime;
            if (_held < holdTime) return;
            _fired = true;
            XRHandInput.Haptic(Hand.Left, 0.8f, 0.25f);
            Debug.Log("[Restart] Menu button held - restarting from step 1");
            var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // in the Editor the open scene may not be in the build list
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        static bool DesktopKey()
        {
            if (!XRHandInput.SimulationActive) return false;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.backspaceKey.isPressed;
        }
    }
}
