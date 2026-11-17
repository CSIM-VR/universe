using System.Collections.Generic;
using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Curls the fingers of the Meta hand model (OculusHand_L/R bones: b_l_index1 ... b_r_pinky3).
    /// Builder: open hand, closes into a grab with GRIP. Destroyer: a loose fist that clenches with GRIP / TRIGGER.
    /// Finger joints bend around their local Z axis; flip <see cref="curlSign"/> if they bend backwards.
    /// </summary>
    public class HandPose : MonoBehaviour
    {
        public Hand hand = Hand.Right;
        public bool destroyer;
        [Tooltip("-1 or 1: direction the joints bend.")]
        public float curlSign = -1f;
        public float speed = 12f;

        [Range(0f, 1f)] public float restCurl = 0.12f;      // builder, nothing pressed
        [Range(0f, 1f)] public float fistCurl = 0.95f;
        [Range(0f, 1f)] public float destroyerRestCurl = 0.7f;

        struct Joint { public Transform t; public Quaternion rest; public float maxAngle; }
        readonly List<Joint> _joints = new();
        float _curl;

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (!n.StartsWith("b_l_") && !n.StartsWith("b_r_")) continue;
                float max = 0f;
                if (n.EndsWith("thumb1")) max = 15f;
                else if (n.EndsWith("thumb2")) max = 30f;
                else if (n.EndsWith("thumb3")) max = 40f;
                else if (n.EndsWith("1") && !n.Contains("thumb")) max = 70f;   // knuckle
                else if (n.EndsWith("2") && !n.Contains("thumb")) max = 95f;   // middle joint
                else if (n.EndsWith("3") && !n.Contains("thumb")) max = 65f;   // tip joint
                if (max > 0f) _joints.Add(new Joint { t = t, rest = t.localRotation, maxAngle = max });
            }
            _curl = destroyer ? destroyerRestCurl : restCurl;
            Apply();
        }

        void LateUpdate()
        {
            bool squeeze = XRHandInput.Grip(hand) || (destroyer && XRHandInput.Trigger(hand));
            float target = squeeze ? fistCurl : destroyer ? destroyerRestCurl : restCurl;
            _curl = Mathf.MoveTowards(_curl, target, speed * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            foreach (var j in _joints)
                if (j.t) j.t.localRotation = j.rest * Quaternion.Euler(0f, 0f, curlSign * j.maxAngle * _curl);
        }
    }
}
