using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/AmmoSystem")]
public class AmmoSystemSO : ScriptableObject, IAmmoSystem
{
    public event Action<bool> OnReloadStateChanged;
    public event Action<float> OnReloadStarted;
    public event Action OnAmmoConsumed;

    public bool IsReloading => _isReloading;
    public bool HasAmmo => (_currentAmmo > 0);

    [SerializeField] private int _currentAmmo = 10;
    private bool _isReloading = false;

    [SerializeField] private int maxAmmo = 30;
    [SerializeField] private float reloadTime = 1f;

    public void Reload()
    {
        if (_isReloading || _currentAmmo == maxAmmo)
            return;

        _isReloading = true;
        OnReloadStateChanged?.Invoke(true);
        OnReloadStarted?.Invoke(reloadTime);
    }

    public void FinishReload()
    {
        _currentAmmo = maxAmmo;
        _isReloading = false;
        OnReloadStateChanged?.Invoke(false);
    }

    public void ConsumeAmmo()
    {
        if (_currentAmmo > 0)
        {
            _currentAmmo--;
            OnAmmoConsumed?.Invoke();
        }
    }
}