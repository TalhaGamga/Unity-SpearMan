using System.Collections;
using UnityEngine;

public class Rifle : MonoBehaviour, IRifle
{
    public IFireSystem FireSystem => _fireSystem;
    public IRecoilSystem RecoilSystem => _recoilSystem;
    public IAimSystem AimSystem => _aimSystem;
    public IAmmoSystem AmmoSystem => _ammoSystem;
    public IProjectileSystem ProjectileSystem => _projectileSystem;
    public IAnimationHandler AnimationHandler { get; }
    public IBulletDamageDealerSystem DamageDealerSystem => _damageDealerSystem;
    public IBulletTrail BulletTrail => _bulletTrail;

    public Transform FirePoint => _firePoint;
    public Transform WeaponTransform => _transform;

    [Header("Systems")]
    [SerializeField] private FireSystemSO _fireSystem;
    [SerializeField] private RecoilSystemSO _recoilSystem;
    [SerializeField] private AimSystemSO _aimSystem;
    [SerializeField] private AmmoSystemSO _ammoSystem;
    [SerializeField] private HitscanProjectileSystemSO _projectileSystem;
    [SerializeField] private BulletDamageDealerSystem _damageDealerSystem;

    [Header("Snapping Data")]
    [SerializeField] private Transform _transform;
    [SerializeField] private Vector3 snapPosition;
    [SerializeField] private Quaternion snapRotation;
    [SerializeField] private Vector3 snapScale;
    [SerializeField] private Transform _firePoint;

    [Header("Visuality Data")]
    [SerializeField] private RifleBulletTrail _bulletTrail;
    [SerializeField] private FirearmCursorDataSO _firearmCursorData;


    private ICombat<IRifle> _combat;

    private void OnEnable()
    {
        _projectileSystem.OnProjectileGatheredInfo += distributeInfoOnProjectileGathered;
        _fireSystem.OnFired += _ammoSystem.ConsumeAmmo;
        _fireSystem.OnFired += _recoilSystem.KickBack;
        _ammoSystem.OnReloadStarted += handleReloadTimer;
        CursorManager.Instance.SetFirearmCursor(_firearmCursorData);
        _recoilSystem.OnKickback += CursorManager.Instance.AnimateFiring;
    }

    private void OnDisable()
    {
        _projectileSystem.OnProjectileGatheredInfo -= distributeInfoOnProjectileGathered;
        _fireSystem.OnFired -= _ammoSystem.ConsumeAmmo;
        _fireSystem.OnFired -= _recoilSystem.KickBack;
        _ammoSystem.OnReloadStarted -= handleReloadTimer;
        _recoilSystem.OnKickback -= CursorManager.Instance.AnimateFiring;
    }

    private void Start()
    {
        SetCombat(GetComponentInParent<ICombatManager>());
    }

    public void Tick()
    {
        Debug.Log("Rifle Tick");
    }

    public void FixedTick()
    {
        Debug.Log("Rifle FixedTick");
    }

    public void Equip(IEquipmentManager equipmentManager)
    {
        Transform hand = equipmentManager.Hand;
        transform.SetParent(hand);
        transform.SetLocalPositionAndRotation(snapPosition, snapRotation);
    }

    public void Unequip(IEquipmentManager equipmentManager)
    {
    }

    public void SetCombat(ICombatManager CombatManager)
    {
        _combat = new RifleCombat(this);
        CombatManager.SetCombat(_combat);
    }

    private void distributeInfoOnProjectileGathered(ProjectileGatheredInfo info)
    {
        _bulletTrail.VisualizeFire(info.FirePoint, info.EndPoint);
    }

    private void handleReloadTimer(float reloadTime)
    {
        StartCoroutine(reloadCoroutine(reloadTime));
    }

    private IEnumerator reloadCoroutine(float reloadTime)
    {
        yield return new WaitForSeconds(reloadTime);
        _ammoSystem.FinishReload();
    }
}