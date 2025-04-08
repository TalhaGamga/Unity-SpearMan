using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/FireSystem")]
public class FireSystemSO : ScriptableObject, IFireTriggerSystem
{
    public event Action OnFired;
    public event Action OnFireAttempt;

    public float FireRate => _fireRate;
    public bool CanFire => Time.time >= _lastFireTime + _fireRate;

    [SerializeField] private float _fireRate = 0.2f;

    private float _lastFireTime = 0f;
    private bool _isTryingFire;

    public void Init()
    {
        _lastFireTime = 0f;
    }

    public void AttemptFire()
    {
        _isTryingFire = true;
    }

    public void StopFire()
    {
        _isTryingFire = false;
    }

    public void Tick()
    {
        if (_isTryingFire && CanFire)
        {
            _lastFireTime = Time.time;
            OnFireAttempt?.Invoke();
        }
    }
}