using System.Collections.Generic;
using UnityEngine;

public class RifleCombat : IRifleCombat
{
    #region PUBLIC
    public IRifle Weapon => _rifle;
    #endregion

    #region PRIVATE
    private IRifle _rifle;
    private IHumanoidCombatPromptReceiver _promptReceiver;

    private IFireSystem _fireSystem;
    private IAmmoSystem _ammoSystem;
    private IAimSystem _aimSystem;
    private IProjectileSystem _projectileSystem;
    private IBulletDamageDealerSystem _damageDealerSystem;
    private IRecoilSystem _recoilSystem;

    private RifleInputHandler _inputHandler;
    private CombatManager _combatManager;
    private Transform _rifleTransform;
    private Transform _characterModelTransform;

    private bool _isHoldingFire;
    private float _fireCooldown;
    #endregion

    public RifleCombat(IRifle rifle)
    {
        _fireSystem = rifle.FireSystem;
        _ammoSystem = rifle.AmmoSystem;
        _aimSystem = rifle.AimSystem;
        _projectileSystem = rifle.ProjectileSystem;
        _damageDealerSystem = rifle.DamageDealerSystem;
        _rifleTransform = rifle.WeaponTransform;
        _recoilSystem = rifle.RecoilSystem;

        _rifle = rifle;
    }

    public void Init(CombatManager combatManager, IHumanoidCombatPromptReceiver promptReceiver)
    {
        _combatManager = combatManager;
        _characterModelTransform = combatManager.characterModelTransform;

        _promptReceiver = promptReceiver;
        _inputHandler = new RifleInputHandler(this, promptReceiver);
        _aimSystem.Init(_rifleTransform, Camera.main);
        _fireSystem.Init();
        _projectileSystem.Init();
        _recoilSystem.Init(_rifle.WeaponTransform, new List<IKickbackReceiver>() { _aimSystem });

        _inputHandler.BindInputs();
    }

    public void Tick()
    {
        if (_isHoldingFire)
        {
            handleAutomaticFire();
        }

        else
        {
            _aimSystem.RecoveryKickback();
            _recoilSystem.RecoveryCurrentRecoil();
        }

        handleRotation();
    }

    public void FixedTick()
    {
    }

    public void Fire()
    {
        _isHoldingFire = true;
    }

    public void StopFiring()
    {
        _isHoldingFire = false;
    }

    public void Reload()
    {
        _ammoSystem.Reload();
    }

    public void Aim(Vector2 aimInput)
    {
        _aimSystem.UpdateAim(aimInput);
    }

    public void End()
    {
        _inputHandler.UnbindInputs();
    }

    private void handleAutomaticFire()
    {
        if (_fireCooldown > 0)
        {
            _fireCooldown -= Time.deltaTime;
            return;
        }

        TryFire();
    }

    private void TryFire()
    {
        if (!_ammoSystem.IsReloading && _ammoSystem.HasAmmo && _fireSystem.CanFire)
        {
            IProjectile projectile = _projectileSystem.CreateProjectile();
            _fireSystem.Fire(projectile, _rifle.FirePoint.position, _rifle.FirePoint.forward);
            _fireCooldown = _fireSystem.FireRate;
        }
    }

    private void handleRotation()
    {
        Quaternion aimRotation = _aimSystem.GetAimRotation();
        bool isFlipped = _characterModelTransform.localScale.x < 0;

        if (isFlipped)
        {
            aimRotation = Quaternion.Euler(aimRotation.eulerAngles.x, aimRotation.eulerAngles.y, aimRotation.eulerAngles.z + 180f);
        }

        float dot = Vector3.Dot(_rifle.WeaponTransform.right, _characterModelTransform.right);

        _rifleTransform.localScale = new Vector3(1f, (dot < 0) ? -1f : 1f, 1f);

        _rifleTransform.rotation = aimRotation;
    }
}