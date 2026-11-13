using System.Collections;
using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Step 6: threads of light grow from each island to the asteroid it came from.
    /// islands[i] is linked to asteroids[i]. Wire GameFlowManager.onReveal -> Play().
    /// </summary>
    public class RevealThreads : MonoBehaviour
    {
        public Transform[] islands;
        public Transform[] asteroids;
        public Material threadMaterial;
        public float width = 0.03f;
        public float growTime = 4f;
        public GameObject[] enableOnReveal;   // e.g. the glowing deer

        public void Play() => StartCoroutine(Grow());

        IEnumerator Grow()
        {
            foreach (var g in enableOnReveal) if (g) g.SetActive(true);

            int n = Mathf.Min(islands.Length, asteroids.Length);
            var lines = new LineRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"Thread_{i}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.material = threadMaterial;
                lr.positionCount = 2;
                lr.startWidth = width;
                lr.endWidth = width * 20f;   // thicker far away so it reads at the asteroids' distance
                lr.useWorldSpace = true;
                lines[i] = lr;
            }

            for (float t = 0f; t <= growTime; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / growTime);
                for (int i = 0; i < n; i++)
                {
                    if (!islands[i] || !asteroids[i]) continue;
                    Vector3 a = islands[i].position, b = asteroids[i].position;
                    lines[i].SetPosition(0, a);
                    lines[i].SetPosition(1, Vector3.Lerp(a, b, k));
                }
                yield return null;
            }
        }
    }
}
