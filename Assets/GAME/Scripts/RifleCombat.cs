using System.Collections;
using UnityEngine;
/// <summary>
/// We will control the weapon systems here and the systems will control the weapons.
/// </summary>
/// <param name="rifle"></param>
public class RifleCombat : IRifleCombat
{
    #region PUBLIC
    public IRifle Weapon => _rifle;
    #endregion

    #region PRIVATE
    private CombatManager _combatManager;
    private IHumanoidCombatPromptReceiver _promptReceiver;

    private IFireSystem _fireSystem;
    private IAmmoSystem _ammoSystem;
    private RifleInputHandler _inputHandler;

    private IRifle _rifle;

    private bool _isHoldingFire;
    private float _fireCooldown;
    #endregion

    public RifleCombat(IRifle rifle)
    {
        _fireSystem = rifle.FireSystem;
        _ammoSystem = rifle.AmmoSystem;
        _rifle = rifle;
    }

    public void Init(CombatManager combatManager, IHumanoidCombatPromptReceiver promptReceiver)
    {
        _combatManager = combatManager;
        _promptReceiver = promptReceiver;
        _inputHandler = new RifleInputHandler(this, promptReceiver);
        _ammoSystem.OnReloadStarted += handleReloadTimer;
        _fireSystem.Init();

        _inputHandler.BindInputs();
    }

    public void Tick()
    {
        if (_isHoldingFire)
        {
            handleAutomaticFire();
        }
    }

    public void FixedTick()
    {
    }

    public void Fire()
    {
        Debug.Log("Init Fire");
        _isHoldingFire = true;
    }

    public void StopFiring()
    {
        Debug.Log("Stop Fire");

        _isHoldingFire = false;
    }

    public void Reload()
    {
        _ammoSystem.Reload();
        Debug.Log("Reload");
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
            _fireSystem.Fire(_rifle.FirePoint.position, _rifle.FirePoint.forward);
            _ammoSystem.ConsumeAmmo();
            _fireCooldown = _fireSystem.FireRate;
        }
    }

    public void End()
    {
        _inputHandler.UnbindInputs();
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
}