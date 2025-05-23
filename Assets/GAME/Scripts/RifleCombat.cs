using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;

public class RifleCombat : IRifleCombat
{
    #region PUBLIC
    public IRifle Weapon => _rifle;
    #endregion

    #region PRIVATE
    private IRifle _rifle;

    private IFireTriggerSystem _fireTriggerSystem;
    private IRecoilSystem _recoilSystem;
    private IAimSystem _aimSystem;
    private IAmmoSystem _ammoSystem;
    private IProjectileSystem _projectileSystem;
    private IBulletTrail _bulletTrail;

    private Transform _firePoint;
    private Transform _rifleTransform;

    private RifleInputHandler _inputHandler;
    private CombatManager _combatManager;
    private Transform _characterModelTransform;

    private bool _isHoldingFire = false;

    private event Action onProjectileFired;

    private float _recoilStrength;
    private float _recoilRecoveryDelay;
    #endregion

    public RifleCombat(IRifle rifle)
    {
        _rifle = rifle;

        _fireTriggerSystem = rifle.FireSystem;
        _recoilSystem = rifle.RecoilSystem;
        _aimSystem = rifle.AimSystem;
        _ammoSystem = rifle.AmmoSystem;
        _projectileSystem = rifle.ProjectileSystem;
        _bulletTrail = rifle.BulletTrail;
        _rifleTransform = _rifle.WeaponTransform;
    }

    public void Init(IHumanoidCombatPromptReceiver promptReceiver, CombatManager combatManager)
    {
        _inputHandler = new RifleInputHandler(this, promptReceiver);
        _combatManager = combatManager;

        _projectileSystem.Init();
        _fireTriggerSystem.Init();
        _aimSystem.Init(Camera.main);
        _recoilSystem.Init();
        _ammoSystem.Init();

        _characterModelTransform = combatManager._characterModelTransform;
        _firePoint = _rifle.FirePoint;
    }

    public void Enable()
    {
        _fireTriggerSystem.OnFireAttempt += _ammoSystem.TryConsumeAmmo;
        _ammoSystem.OnAmmoConsumed += fireProjectile;
        _ammoSystem.OnReloadStarted += handleReloadTimer;
        _aimSystem.OnAiming += handleAimRotation;

        onProjectileFired += _recoilSystem.KickBack;
        _recoilSystem.OnKickback += onKickback;

        ServiceLocator.Global.Get<CursorManager>(out CursorManager cursorManager);
        cursorManager.SetFirearmCursor(_rifle.FirearmCursorData);
        _recoilSystem.OnKickback += cursorManager.AnimateFiring;


        _projectileSystem.OnProjectileGatheredInfo += distributeProjectileHitInfo;

        _inputHandler.BindInputs();
    }

    public void Disable()
    {
        _fireTriggerSystem.OnFireAttempt -= _ammoSystem.TryConsumeAmmo;
        _ammoSystem.OnAmmoConsumed -= fireProjectile;
        _ammoSystem.OnReloadStarted -= handleReloadTimer;
        _aimSystem.OnAiming -= handleAimRotation;

        onProjectileFired -= _recoilSystem.KickBack;
        _recoilSystem.OnKickback -= onKickback;

        _projectileSystem.OnProjectileGatheredInfo -= distributeProjectileHitInfo;
        _inputHandler.UnbindInputs();
    }

    public void Tick()
    {
        _aimSystem.Tick();
        _fireTriggerSystem.Tick();
        handleRecoilRecovery();
    }

    public void FixedTick()
    {
    }

    public void AttemptFire()
    {
        _fireTriggerSystem.AttemptFire();
        _isHoldingFire = true;
    }

    public void StopFiring()
    {
        _fireTriggerSystem.StopFire();
        _isHoldingFire = false;
    }

    public void Reload()
    {
        _ammoSystem.Reload();
    }

    public void TakeAim(Vector2 aimInput)
    {
        _aimSystem.TakeAimInput(aimInput, _rifleTransform);
    }

    private void fireProjectile()
    {
        IProjectile projectile = _projectileSystem.CreateProjectile();
        projectile.Fire(_firePoint.position, _firePoint.forward, 0);

        onProjectileFired.Invoke();
    }

    private void handleAimRotation(Vector2 direction)
    {
        bool isFlipped = _characterModelTransform.localScale.x < 0;
        float angle = Mathf.Atan2(direction.y + _recoilStrength, direction.x) * Mathf.Rad2Deg;

        if (isFlipped)
        {
            angle += 180f;
        }

        float dot = Vector3.Dot(_rifle.WeaponTransform.right, _characterModelTransform.right);
        _rifleTransform.rotation = Quaternion.Euler((dot > 0 ? 0f : 180f), 0f, angle * (dot < 0 ? -1 : +1));
    }

    private void handleReloadTimer(float reloadTime)
    {
        _combatManager.StartCoroutine(reloadCoroutine(reloadTime));
    }

    private IEnumerator reloadCoroutine(float reloadTime)
    {
        yield return new WaitForSeconds(reloadTime);
        _ammoSystem.FinishReload();
    }

    private void distributeProjectileHitInfo(ProjectileHitInfo info) //Make Here better
    {
        _bulletTrail.VisualizeFire(info.FirePoint, info.EndPoint);

        if (info.HitObject != null && info.HitObject.TryGetComponent<ReactiveDamageDispatcher>(out ReactiveDamageDispatcher damageDispatcher))
        {
            var source = new BulletDamageEventSource(10, 10, info.FirePoint, info.Direction, "FX");
            damageDispatcher.Apply(source, info.HitObject);
        }
    }

    private void onKickback(float recoilStrength, float recoveryDelay)
    {
        _rifleTransform.DOKill();

        Sequence recoilSequence = DOTween.Sequence();

        recoilSequence.Append(_rifleTransform.DOScale(new Vector3(0.95f,
                                                                  1.05f,
                                                                  1f), 0.05f)
            .SetEase(Ease.OutQuad));

        recoilSequence.Join(_rifleTransform.DOPunchPosition(Vector3.left * recoilStrength * 0.1f, 0.1f, 10, 1f)
            .SetEase(Ease.OutQuad));

        recoilSequence.Append(_rifleTransform.DOScale(new Vector3(1, 1, 1), recoveryDelay * 0.5f)
            .SetEase(Ease.OutElastic));

        recoilSequence.Play();

        _recoilStrength = recoilStrength;
        _recoilRecoveryDelay = recoveryDelay;
    }

    private void handleRecoilRecovery()
    {
        _recoilStrength = Mathf.Lerp(_recoilStrength, 0, _recoilRecoveryDelay * 0.05f);
    }
}