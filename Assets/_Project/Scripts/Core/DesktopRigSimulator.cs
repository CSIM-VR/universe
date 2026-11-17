using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Plays the core loop in the Editor without a headset (only active when no XR device is running).
    /// Turns off OVRCameraRig's tracking and drives the head + right hand with mouse and keyboard:
    ///   RMB + mouse = look, WASD / QE = move, wheel = hand reach, Tab = touch the artifact
    ///   Builder:   Space = attract (trigger), LMB = grab / release (grip), R = tilt hand (pour)
    ///   Destroyer: LMB = punch, F = gentle tap
    /// Also has a small public API (AimAt, Punch, SetGrip...) used by the automated smoke test.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class DesktopRigSimulator : MonoBehaviour
    {
        [Tooltip("Use the simulator even when a headset is connected.")]
        public bool forceSimulation;

        public Transform head;          // CenterEyeAnchor
        public Transform leftHand;      // LeftHandAnchor
        public Transform rightHand;     // RightHandAnchor
        public PerspectiveSwitcher switcher;
        public CentralArtifact artifact;

        [Header("Feel (rig units = real metres)")]
        public float eyeHeight = 1.6f;
        public float lookSensitivity = 0.12f;
        public float moveSpeed = 1.2f;
        public Vector3 handOffset = new Vector3(0.06f, -0.06f, 0f);
        public float builderReach = 0.5f;
        public float destroyerReach = 0.3f;
        public float punchDistance = 0.35f;
        public float punchTime = 0.12f;       // ~2.9 m/s: breaks a layer / violent in careful mode
        public float tapTime = 0.45f;         // ~0.8 m/s: a gentle tap

        public bool Active { get; private set; }
        public bool IsThrusting => _thrustT >= 0f;

        /// <summary>When set, the right hand is placed exactly here (world space). Used by tests.</summary>
        public Vector3? RightHandOverride { get; set; }

        float _yaw, _pitch;
        float _thrustT = -1f, _thrustDuration;
        float _touchUntil;
        bool _grip, _trigger, _tilt;

        void Awake()
        {
            Active = forceSimulation || !UnityEngine.XR.XRSettings.isDeviceActive;
            XRHandInput.SimulationActive = Active;
            if (!Active) { enabled = false; return; }

            // stop the Meta rig from overwriting the anchors with (absent) tracking data
            var ovrRig = GetComponent("OVRCameraRig") as Behaviour;
            if (ovrRig) ovrRig.enabled = false;
            if (head) head.localPosition = new Vector3(0f, eyeHeight, 0f);
            Debug.Log("[DesktopRigSimulator] No headset - desktop controls active.");
        }

        bool IsDestroyer => switcher != null && switcher.Current == Role.Destroyer;

        // ---------- API (also used by the smoke test) ----------

        public void AimAt(Vector3 worldPoint)
        {
            Vector3 d = head.parent.InverseTransformDirection(worldPoint - head.position);
            _yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            _pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            ApplyHead();
        }

        public void Punch(bool gentle)
        {
            if (IsThrusting) return;
            _thrustDuration = gentle ? tapTime : punchTime;
            _thrustT = 0f;
        }

        public void SetGrip(bool on) => _grip = on;
        public void SetTrigger(bool on) => _trigger = on;
        public void SetTilt(bool on) => _tilt = on;
        public void TouchArtifact() => _touchUntil = Time.time + 0.25f;

        // ---------- per frame ----------

        void Update()
        {
            // XR can start a moment after Awake (e.g. the Meta XR Simulator): hand control back to the real rig
            if (!forceSimulation && UnityEngine.XR.XRSettings.isDeviceActive)
            {
                var ovrRig = GetComponent("OVRCameraRig") as Behaviour;
                if (ovrRig) ovrRig.enabled = true;
                XRHandInput.SimulationActive = false;
                Active = false;
                enabled = false;
                Debug.Log("[DesktopRigSimulator] XR device detected - desktop controls off, using the headset / XR Simulator.");
                return;
            }

            ReadDevices();

            XRHandInput.SetSimulated(Hand.Right, _trigger, _grip);
            XRHandInput.SetSimulated(Hand.Left, false, false);

            ApplyHead();
            PlaceHands();
        }

        void ReadDevices()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            float dt = Time.unscaledDeltaTime;

            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    _yaw += delta.x * lookSensitivity;
                    _pitch = Mathf.Clamp(_pitch - delta.y * lookSensitivity, -85f, 85f);
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float step = Mathf.Sign(scroll) * 0.03f;
                    if (IsDestroyer) destroyerReach = Mathf.Clamp(destroyerReach + step, 0.15f, 0.8f);
                    else builderReach = Mathf.Clamp(builderReach + step, 0.15f, 1.5f);
                }
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (IsDestroyer) Punch(false);
                    else _grip = !_grip;
                }
            }

            if (kb != null)
            {
                if (kb.fKey.wasPressedThisFrame && IsDestroyer) Punch(true);
                if (kb.tabKey.wasPressedThisFrame) TouchArtifact();
                if (kb.spaceKey.wasPressedThisFrame) _trigger = true;
                if (kb.spaceKey.wasReleasedThisFrame) _trigger = false;
                if (kb.rKey.wasPressedThisFrame) _tilt = true;
                if (kb.rKey.wasReleasedThisFrame) _tilt = false;

                Vector3 move = Vector3.zero;
                if (kb.wKey.isPressed) move.z += 1f;
                if (kb.sKey.isPressed) move.z -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                if (kb.eKey.isPressed) move.y += 1f;
                if (kb.qKey.isPressed) move.y -= 1f;
                if (move != Vector3.zero)
                {
                    Vector3 world = transform.rotation * (Quaternion.Euler(0f, _yaw, 0f) * move.normalized);
                    transform.position += world * moveSpeed * transform.lossyScale.x * dt;
                }
            }
        }

        void ApplyHead()
        {
            if (head) head.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        void PlaceHands()
        {
            if (!head) return;

            float reach = IsDestroyer ? destroyerReach : builderReach;
            if (IsThrusting)
            {
                // straight out over _thrustDuration, then back twice as slowly
                _thrustT += Time.deltaTime;
                float outT = _thrustT / _thrustDuration;
                float k = outT <= 1f ? outT : Mathf.Max(0f, 1f - (outT - 1f) * 0.5f);
                reach += punchDistance * k;
                if (outT >= 3f) _thrustT = -1f;
            }

            if (rightHand)
            {
                if (RightHandOverride.HasValue) rightHand.position = RightHandOverride.Value;
                else if (Time.time < _touchUntil && artifact) rightHand.position = artifact.transform.position;
                else rightHand.position = head.TransformPoint(handOffset + Vector3.forward * reach);

                // hand stays upright (yaw only) so a held ice cube only pours when R tilts it
                Quaternion yawOnly = head.parent.rotation * Quaternion.Euler(0f, _yaw, 0f);
                rightHand.rotation = yawOnly * Quaternion.Euler(0f, 0f, _tilt ? -110f : 0f);
            }
            if (leftHand)
            {
                leftHand.position = head.TransformPoint(new Vector3(-0.22f, -0.35f, 0.25f));
                leftHand.rotation = head.rotation;
            }
        }
    }
}
