using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class VFXManager : MonoBehaviour
{
    [SerializeField] private VFXSet _currentSet;
    [SerializeField] private Transform _poolRoot;
    [SerializeField] private int _poolMaxPerPrefab = 50;

    private VFXPool _pool;
    private readonly Dictionary<int, GameObject> _activeByInstance = new();
    private readonly Dictionary<GameObject, Coroutine> _lifetimes = new();

    private void Awake()
    {
        if (_currentSet != null)
            _currentSet.Initialize();
        if (_poolRoot != null)
            _poolRoot = transform;
        _pool = new VFXPool(_poolRoot, _poolMaxPerPrefab);
    }

    public void SwapVFXSet(VFXSet set)
    {
        _currentSet = set;
        _currentSet.Initialize();
    }

    public void HandleVFXSignal(VFXSignal signal)
    {
        if (signal.Type == VFXType.None)
            return;
        var prefab = _currentSet?.GetPrefab(signal.Type);
        if (prefab == null) return;

        if (signal.StartDelay > 0)
        {
            StartCoroutine(spawnDelayed(prefab, signal));
            return;
        }

        spawnImmediate(prefab, signal);
    }

    public void StopInstance(int instanceId)
    {
        if (instanceId == 0)
            return;
        if (!_activeByInstance.TryGetValue(instanceId, out var go))
            return;
        cleanupInstance(go, instanceId);
    }

    private IEnumerator spawnDelayed(GameObject prefab, VFXSignal signal)
    {
        yield return new WaitForSeconds(signal.StartDelay);
        spawnImmediate(prefab, signal);
    }

    private void spawnImmediate(GameObject prefab, VFXSignal signal)
    {
        var instance = _pool.Spawn(prefab, signal.Position, signal.Rotation, signal.Parent);
        if (instance == null)
            return;

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
            _activeByInstance[signal.InstanceId] = instance;

        applyPlaybackRate(instance, signal.PlaybackRate);

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
        if (go == null)
            return;
        if (_lifetimes.TryGetValue(go, out var c))
        {
            StopCoroutine(c);
            _lifetimes.Remove(go);
        }

        if (instanceId != 0)
            _activeByInstance.Remove(instanceId);
        _pool.Return(go);
    }

    private void applyPlaybackRate(GameObject go, float rate)
    {
        var ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps == null)
            return;
        var main = ps.main;
        main.simulationSpeed = rate;
        ps.Play(true);
    }

    private float determineDefaultLifetime(GameObject go)
    {
        var ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps == null) return 1f;
        var mainPs = ps.main;
        float duration = mainPs.duration;
        float startLifetime = 0f;
        var mode = mainPs.startLifetime.mode;
        if (mode == ParticleSystemCurveMode.Constant)
            startLifetime = mainPs.startLifetime.constant;
        return duration + startLifetime;
    }
}