using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/FireSystem")]
public class FireSystemSO : ScriptableObject, IFireSystem
{
    public event Action OnFired;
    public float FireRate => _fireRate;
    public bool CanFire => Time.time >= _lastFireTime + _fireRate;

    [SerializeField] private float _fireRate = 0.2f;
    [SerializeField] private float projectileSpeed = 50f;
    [SerializeField] private SoundData soundData;

    private float _lastFireTime = 0f;

    public void Init()
    {
        _lastFireTime = 0f;
    }

    public void Fire(IProjectile Projectile, Vector3 Origin, Vector3 Direction)
    {
        _lastFireTime = Time.time;

        Projectile.Fire(Origin, Direction, projectileSpeed);
        OnFired?.Invoke();
        SoundManager.Instance.CreateSoundBuilder()
        .WithRandomPitch(-0.25f, 0.25f)
        .Play(soundData);
    }
}