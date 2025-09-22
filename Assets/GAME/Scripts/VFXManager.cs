using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class VFXManager : MonoBehaviour
{
    [System.Serializable]
    public struct SystemSetEntry
    {
        public SystemType System;
        public VFXSet Set;
    }

    [Header("VFX Sets")]
    [SerializeField] private List<SystemSetEntry> _initialSets = new();
    [HideInInspector] private VFXSet _fallbackSet = null;

    [Header("Pooling")]
    [SerializeField] private Transform _poolRoot;
    [SerializeField, Min(1)] private int _poolMaxPerPrefab = 50;

    private VFXPool _pool;

    private readonly Dictionary<SystemType, VFXSet> _sets = new();

    private readonly Dictionary<int, HashSet<GameObject>> _activeByInstance = new();

    private readonly Dictionary<GameObject, Coroutine> _lifetimes = new();

    private class PendingHandle { public bool Cancelled; }
    private readonly Dictionary<int, List<PendingHandle>> _pendingByInstance = new();

    private void Awake()
    {
        if (_poolRoot == null)
        {
            var root = GameObject.Find("PoolRoot").transform;
            if (root != null)
            {
                _poolRoot = root;
            }
        }

        _sets.Clear();
        for (int i = 0; i < _initialSets.Count; i++)
        {
            var e = _initialSets[i];
            if (e.Set != null)
            {
                e.Set.Initialize();
                _sets[e.System] = e.Set;
            }
        }
        if (_fallbackSet != null)
            _fallbackSet.Initialize();

        _pool = new VFXPool(_poolRoot, Mathf.Max(1, _poolMaxPerPrefab));
    }

    private void OnDisable()
    {
        foreach (var kv in _pendingByInstance)
        {
            var handles = kv.Value;
            if (handles == null) continue;
            for (int i = 0; i < handles.Count; i++)
                handles[i].Cancelled = true;
        }
        _pendingByInstance.Clear();

        StopAllCoroutines();

        var toCleanup = new List<KeyValuePair<int, HashSet<GameObject>>>(_activeByInstance);
        foreach (var kv in toCleanup)
        {
            var id = kv.Key;
            var set = kv.Value;
            if (set == null) continue;
            var list = new List<GameObject>(set);
            for (int i = 0; i < list.Count; i++)
                cleanupInstance(list[i], id);
        }

        _activeByInstance.Clear();
        _lifetimes.Clear();
    }

    private void OnDestroy() => OnDisable();


    public void SetVFXSet(SystemType system, VFXSet set, bool initialize = true)
    {
        if (set == null) { _sets.Remove(system); return; }
        if (initialize) set.Initialize();
        _sets[system] = set;
    }

    public void SwapVFXSet(SystemType system, VFXSet set) => SetVFXSet(system, set);

    public VFXSet GetVFXSet(SystemType system)
    {
        return _sets.TryGetValue(system, out var s) ? s : _fallbackSet;
    }

    public void HandlePlayVFXSignal(VFXPlaySignal signal)
    {
        if (signal.VFXType == VFXType.None)
            return;

        var set = GetVFXSet(signal.SystemType);
        var prefab = set?.GetPrefab(signal.VFXType);
        if (prefab == null)
            return;

        if (signal.StartDelay > 0f)
        {
            PendingHandle handle = null;
            if (signal.InstanceId != 0)
            {
                handle = new PendingHandle();
                if (!_pendingByInstance.TryGetValue(signal.InstanceId, out var list))
                {
                    list = new List<PendingHandle>(2);
                    _pendingByInstance[signal.InstanceId] = list;
                }
                list.Add(handle);
            }
            StartCoroutine(spawnDelayed(prefab, signal, handle));
            return;
        }

        spawnImmediate(prefab, signal);
    }

    public void HandleStopVFXSignal(VFXStopSignal signal)
    {
        if (signal.InstanceId == 0)
            return;

        if (_pendingByInstance.TryGetValue(signal.InstanceId, out var handles) && handles != null)
        {
            for (int i = 0; i < handles.Count; i++)
                handles[i].Cancelled = true;

            _pendingByInstance.Remove(signal.InstanceId);
        }

        if (_activeByInstance.TryGetValue(signal.InstanceId, out var set) && set != null)
        {
            var list = new List<GameObject>(set);
            for (int i = 0; i < list.Count; i++)
                cleanupInstance(list[i], signal.InstanceId);
        }

#if UNITY_EDITOR
        // Optional debug log for tracing
        Debug.Log($"[VFXManager] Stop signal received (System={signal.SystemType}, InstanceId={signal.InstanceId})");
#endif
    }

    private IEnumerator spawnDelayed(GameObject prefab, VFXPlaySignal signal, PendingHandle handle)
    {
        yield return new WaitForSeconds(signal.StartDelay);

        if (handle != null && handle.Cancelled)
            yield break;

        spawnImmediate(prefab, signal);

        if (signal.InstanceId != 0 && handle != null && _pendingByInstance.TryGetValue(signal.InstanceId, out var list))
        {
            list.Remove(handle);
            if (list.Count == 0) _pendingByInstance.Remove(signal.InstanceId);
        }
    }

    private void spawnImmediate(GameObject prefab, VFXPlaySignal signal)
    {
        var instance = _pool.Spawn(prefab, signal.Position, signal.Rotation, signal.Parent);
        if (instance == null) return;

        if (signal.Parent != null && signal.FollowParent)
        {
            instance.transform.SetParent(signal.Parent, false);
            instance.transform.localPosition = signal.PositionOffset;
            instance.transform.localRotation = Quaternion.Euler(signal.RotationOffset);
        }
        else
        {
            instance.transform.SetParent(_poolRoot, false);
            instance.transform.position = signal.Position + signal.PositionOffset;
            instance.transform.rotation = signal.Rotation * Quaternion.Euler(signal.RotationOffset);
        }
        instance.transform.localScale = signal.Scale;

        if (signal.InstanceId != 0)
        {
            if (!_activeByInstance.TryGetValue(signal.InstanceId, out var set))
            {
                set = new HashSet<GameObject>();
                _activeByInstance[signal.InstanceId] = set;
            }
            set.Add(instance);
        }

        var rate = (signal.PlaybackRate <= 0f) ? 1f : signal.PlaybackRate;
        applyPlaybackRate(instance, Mathf.Max(0.0001f, rate));

        if (signal.OneShot)
        {
            float lifetime = signal.Lifetime > 0f ? signal.Lifetime : determineDefaultLifetime(instance);
            if (lifetime > 0f)
            {
                var coro = StartCoroutine(returnAfter(instance, lifetime, signal.InstanceId));
                _lifetimes[instance] = coro;
            }
        }
    }

    private IEnumerator returnAfter(GameObject go, float t, int instanceId)
    {
        yield return new WaitForSeconds(t);
        cleanupInstance(go, instanceId);
    }

    private void cleanupInstance(GameObject go, int instanceId)
    {
        if (go == null) return;

        if (_lifetimes.TryGetValue(go, out var c) && c != null)
        {
            StopCoroutine(c);
            _lifetimes.Remove(go);
        }

        if (instanceId != 0 && _activeByInstance.TryGetValue(instanceId, out var set))
        {
            set.Remove(go);
            if (set.Count == 0) _activeByInstance.Remove(instanceId);
        }

        var psAll = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < psAll.Length; i++)
            psAll[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        _pool.Return(go);
    }

    private void applyPlaybackRate(GameObject go, float rate)
    {
        var systems = go.GetComponentsInChildren<ParticleSystem>(true);
        if (systems == null || systems.Length == 0) return;

        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            var main = ps.main;
            main.simulationSpeed = rate;
            ps.Play(true);
        }
    }

    private float determineDefaultLifetime(GameObject go)
    {
        var systems = go.GetComponentsInChildren<ParticleSystem>(true);
        if (systems == null || systems.Length == 0)
            return 1f;

        float maxLifetime = 0f;
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            var main = ps.main;

            float duration = main.duration;
            float startLifetime = 0f;

            switch (main.startLifetime.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    startLifetime = main.startLifetime.constant;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    startLifetime = Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax);
                    break;
                case ParticleSystemCurveMode.Curve:
                case ParticleSystemCurveMode.TwoCurves:
                    var c = main.startLifetime;
                    var ac = (c.mode == ParticleSystemCurveMode.TwoCurves) ? c.curveMax : c.curve;
                    if (ac != null && ac.length > 0)
                    {
                        float peak = 0f;
                        for (int k = 0; k < ac.length; k++)
                            peak = Mathf.Max(peak, ac.keys[k].value);
                        startLifetime = peak;
                    }
                    break;
            }

            maxLifetime = Mathf.Max(maxLifetime, duration + startLifetime);
        }

        return (maxLifetime > 0f) ? maxLifetime : 1f;
    }
}