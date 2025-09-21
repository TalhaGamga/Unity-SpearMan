using System.Collections.Generic;
using UnityEngine;

public sealed class VFXPool
{
    private readonly Transform _poolRoot;
    private readonly int _maxPerPrefab;
    private readonly Dictionary<int, GenericPool<GameObject>> _pools = new();

    public VFXPool(Transform poolRoot, int maxPerPrefab = 50)
    {
        _poolRoot = poolRoot;
        _maxPerPrefab = maxPerPrefab;
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        int prefabId = prefab.GetInstanceID();
        if (!_pools.TryGetValue(prefabId, out var pool))
        {
            pool = new GenericPool<GameObject>(
                createFunc: () => createNewInstance(prefab),
                onGet: go => go.SetActive(true),
                onRelease: resetInstance,
                maxSize: _maxPerPrefab
                );
            _pools[prefabId] = pool;
        }

        var instance = pool.Get();
        if (parent != null)
        {
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
        }
        else
        {
            instance.transform.SetParent(_poolRoot, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = rotation;
        }

        return instance;
    }

    public void Return(GameObject instance)
    {
        if (instance == null) return;

        var prefabId = instance.GetComponent<VFXMarker>()?.PrefabId ?? instance.GetInstanceID();
        if (!_pools.TryGetValue(prefabId, out var pool)) { Object.Destroy(instance); return; }

        pool.Release(instance);
    }

    private GameObject createNewInstance(GameObject prefab)
    {
        var go = Object.Instantiate(prefab, _poolRoot);
        var marker = go.GetComponent<VFXMarker>();
        if (marker == null)
            marker = go.AddComponent<VFXMarker>();
        marker.PrefabId = prefab.GetInstanceID();
        return go;
    }

    private void resetInstance(GameObject go)
    {
        go.SetActive(false);
        go.transform.SetParent(_poolRoot, false);

        var ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var audio = go.GetComponentInChildren<AudioSource>();
        if (audio != null)
            audio.Stop();
    }
}

public class VFXMarker : MonoBehaviour
{
    public int PrefabId;
}