using System.Collections.Generic;
using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Size cue: when you switch, a glowing "echo" of the self you leave stays behind, frozen in the pose you had.
    ///  - Grow into the Destroyer and look down: your golden Builder echo (head, body, hands) stands tiny among the
    ///    islands, about the size of your giant thumb.
    ///  - Shrink back into the Builder and look up: the Destroyer's huge blue hands (hands only) still hang by the asteroids.
    /// Each echo is only shown while you are the other role. Added at runtime next to the PerspectiveSwitcher;
    /// uses the hands' own glow materials, so it needs no setup.
    /// </summary>
    public class RoleEcho : MonoBehaviour
    {
        public PerspectiveSwitcher switcher;
        [Tooltip("Echo brightness compared to the real hands.")]
        [Range(0f, 1f)] public float strength = 0.55f;
        public bool builderBody = true;

        readonly Dictionary<Role, GameObject> _echo = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void AddToScene()
        {
            var sw = FindAnyObjectByType<PerspectiveSwitcher>();
            if (sw && !FindAnyObjectByType<RoleEcho>()) sw.gameObject.AddComponent<RoleEcho>();   // finds the switcher itself
        }

        void OnEnable()
        {
            if (!switcher) switcher = FindAnyObjectByType<PerspectiveSwitcher>();
            if (switcher) switcher.BeforeSwitch += Snapshot;
        }

        void OnDisable()
        {
            if (switcher) switcher.BeforeSwitch -= Snapshot;
        }

        void Update()
        {
            if (!switcher) return;
            foreach (var kv in _echo)
                if (kv.Value) kv.Value.SetActive(kv.Key != switcher.Current);
        }

        void Snapshot(Role role)
        {
            var hands = role == Role.Builder ? switcher.builderOnly : switcher.destroyerOnly;
            if (hands == null) return;
            if (_echo.TryGetValue(role, out var old) && old) Destroy(old);
            var root = new GameObject($"Echo_{role}");
            _echo[role] = root;

            Material glow = null;
            foreach (var h in hands)
            {
                if (!h) continue;
                foreach (var r in h.GetComponentsInChildren<Renderer>(false))
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    Mesh mesh = null;
                    Vector3 scale;
                    if (r is SkinnedMeshRenderer smr)
                    {
                        mesh = new Mesh { name = "EchoHand" };
                        smr.BakeMesh(mesh, true);
                        scale = Vector3.one;
                    }
                    else if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh)
                    {
                        mesh = mf.sharedMesh;
                        scale = r.transform.lossyScale;
                    }
                    else continue;
                    var go = new GameObject("EchoPart");
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                    go.transform.localScale = scale;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = Dim(r.sharedMaterials);
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    glow ??= mr.sharedMaterial;
                }
            }

            // a simple glowing body so the Builder echo reads as a person (the giant's would wrap around the Builder)
            var cam = switcher.xrCamera ? switcher.xrCamera.transform : null;
            if (cam && glow)
            {
                float s = switcher.xrOrigin ? switcher.xrOrigin.lossyScale.x : 1f;
                var headPos = cam.position;
                var yaw = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
                // Builder only: the giant's head would be a big blue ball hanging in the sky
                if (role == Role.Builder && builderBody)
                {
                    Body(root.transform, PrimitiveType.Sphere, headPos, yaw, Vector3.one * 0.2f * s, glow);
                    float feet = switcher.xrOrigin ? switcher.xrOrigin.position.y : headPos.y - 1.6f * s;
                    float h = Mathf.Max(0.5f * s, headPos.y - 0.15f * s - feet);
                    Body(root.transform, PrimitiveType.Capsule, new Vector3(headPos.x, feet + h * 0.5f, headPos.z) - yaw * Vector3.forward * 0.05f * s,
                         yaw, new Vector3(0.36f * s, h * 0.5f, 0.22f * s), glow);
                }
            }
            root.SetActive(false);   // Update shows it once the role has changed
        }

        void Body(Transform parent, PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "EchoBody";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // own copies, a bit fainter than the live hands
        Material[] Dim(Material[] src)
        {
            var dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                if (!src[i]) continue;
                var m = new Material(src[i]);
                if (m.HasProperty("_Color")) m.SetColor("_Color", m.GetColor("_Color") * strength);
                if (m.HasProperty("_Shimmer")) m.SetFloat("_Shimmer", 0.6f);
                dst[i] = m;
            }
            return dst;
        }
    }
}
