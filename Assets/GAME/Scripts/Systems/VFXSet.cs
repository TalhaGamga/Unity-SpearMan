using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class VFXSet
{
    [SerializeField] private List<VFXEntry> entries = new();
    private Dictionary<VFXType, GameObject> lookup;

    [System.Serializable]
    public struct VFXEntry
    {
        public VFXType Id;
        public GameObject Prefab;
    }

    public void Initialize(bool logDuplicates = true)
    {
        if (lookup == null)
            lookup = new Dictionary<VFXType, GameObject>(entries.Count);
        else
            lookup.Clear();

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.Prefab == null) continue;

            if (!lookup.ContainsKey(e.Id))
            {
                lookup.Add(e.Id, e.Prefab);
            }
            else
            {
#if UNITY_EDITOR
                if (logDuplicates)
                    Debug.LogWarning($"[VFXSet] Duplicate VFXType '{e.Id}' found; keeping the first assignment.", e.Prefab);
#endif
            }
        }
    }

    /// <summary>Null-safe; will lazy-init if needed.</summary>
    public GameObject GetPrefab(VFXType type)
    {
        if (lookup == null) Initialize(false);
        return lookup.TryGetValue(type, out var prefab) ? prefab : null;
    }

    public bool TryGetPrefab(VFXType type, out GameObject prefab)
    {
        if (lookup == null) Initialize(false);
        return lookup.TryGetValue(type, out prefab);
    }

    public bool Contains(VFXType type)
    {
        if (lookup == null) Initialize(false);
        return lookup.ContainsKey(type);
    }

    public int Count => lookup?.Count ?? 0;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Keep the lookup fresh in-editor for quick play-mode starts
        Initialize();
    }
#endif
}

