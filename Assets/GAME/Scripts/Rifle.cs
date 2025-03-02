using UnityEngine;

public class Rifle : MonoBehaviour, IRifle
{
    public IFireSystem FireSystem => fireSystem;
    public IRecoilSystem RecoilSystem => recoilSystem;
    public IAimSystem AimSystem => aimSystem;
    public IAmmoSystem AmmoSystem => ammoSystem;

    public Transform FirePoint => _firePoint;

    [Header("Systems")]
    [SerializeField] private FireSystemSO fireSystem;
    [SerializeField] private RecoilSystemSO recoilSystem;
    [SerializeField] private AimSystemSO aimSystem;
    [SerializeField] private AmmoSystemSO ammoSystem;

    [Header("Snapping")]
    [SerializeField] private Vector3 snapPosition;
    [SerializeField] private Quaternion snapRotation;
    [SerializeField] private Vector3 snapScale;
    [SerializeField] private Transform _firePoint;

    private ICombat<IRifle> combat;

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
        combat = new RifleCombat(this);
        CombatManager.SetCombat(combat);
    }
}