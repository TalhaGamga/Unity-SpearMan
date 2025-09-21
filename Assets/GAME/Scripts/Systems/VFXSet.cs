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

    public void Initialize()
    {
        lookup = new Dictionary<VFXType, GameObject>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Prefab != null && !lookup.ContainsKey(entry.Id))
                lookup.Add(entry.Id, entry.Prefab);
        }
    }

    public GameObject GetPrefab(VFXType type)
    {
        if (lookup == null)
            Initialize();
        return lookup.TryGetValue(type, out var prefab) ? prefab : null;
    }
}

