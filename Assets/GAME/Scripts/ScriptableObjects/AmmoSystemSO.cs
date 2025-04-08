using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/AmmoSystem")]
public class AmmoSystemSO : ScriptableObject, IAmmoSystem
{
    public event Action<float> OnReloadStarted;
    public event Action OnAmmoConsumed;

    private bool _isConsumable => (_currentAmmo > 0) && !_isReloading;

    [SerializeField] private int _currentAmmo = 10;
    [SerializeField] private int maxAmmo = 30;
    [SerializeField] private float reloadTime = 1f;

    [SerializeField] private SoundData ammoConsumeSound;
    [SerializeField] private SoundData reloadSound;

    private bool _isReloading = false;

    public void Init()
    {
        _isReloading = false;
    }

    public void Reload()
    {
        if (_isReloading || _currentAmmo == maxAmmo)
            return;
        Debug.Log("Reload");

        _isReloading = true;
        OnReloadStarted?.Invoke(reloadSound.clip.length);
        SoundManager.Instance.CreateSoundBuilder()
        .WithRandomPitch(-0.25f, 0.25f)
        .Play(reloadSound);
    }

    public void FinishReload()
    {
        _currentAmmo = maxAmmo;
        _isReloading = false;
    }

    public void TryConsumeAmmo()
    {
        if (_isConsumable)
        {
            _currentAmmo--;
            OnAmmoConsumed?.Invoke();
            SoundManager.Instance.CreateSoundBuilder()
                .WithRandomPitch(-0.25f, 0.25f)
                .Play(ammoConsumeSound);
        }
    }
}