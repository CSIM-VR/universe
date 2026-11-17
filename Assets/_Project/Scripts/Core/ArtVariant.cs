using UnityEngine;

namespace SpaceBetweenUs
{
    /// <summary>
    /// Shows one of several art variants so instances of the same prefab look different.
    /// Picks by the trailing number of the owner's name (Island_1 -> variant 0, Island_2 -> 1 ...),
    /// otherwise by a hash of the name. Purely visual - no gameplay code depends on it.
    /// </summary>
    public class ArtVariant : MonoBehaviour
    {
        public Transform nameSource;
        public GameObject[] variants;

        void Awake()
        {
            if (variants == null || variants.Length == 0) return;
            string n = (nameSource ? nameSource : transform.root).name;
            int end = n.Length, start = end;
            while (start > 0 && char.IsDigit(n[start - 1])) start--;
            int idx = start < end && int.TryParse(n.Substring(start), out int num)
                ? Mathf.Max(0, num - 1)
                : StableHash(n);
            idx %= variants.Length;
            for (int i = 0; i < variants.Length; i++)
                if (variants[i]) variants[i].SetActive(i == idx);
        }

        static int StableHash(string s)
        {
            int h = 0;
            foreach (char c in s) h = (h * 31 + c) & 0x7fffffff;
            return h;
        }
    }
}
