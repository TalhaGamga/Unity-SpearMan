using R3;
using UnityEngine;

public class CombatManager : MonoBehaviour, ICombatManager, IReactiveCapabilityProvider
{
    public Transform _characterModelTransform;
    public Observable<CombatSnapshot> Stream => _stream;
    private readonly Subject<CombatSnapshot> _stream = new();

    public float CombatSpeedModifier { get; set; }
    public float DamageModifier { get; set; }
    public float RangedAttackModifier { get; set; }
    public float AccuracyModifier { get; set; }
    public float CritModifier { get; set; }

    private ICombatBase currentCombat;

    private void OnEnable()
    {
        EventBus.OnItemUsed += onItemUsed;
    }

    private void OnDisable()
    {
        EventBus.OnItemUsed -= onItemUsed;
        currentCombat?.Disable();
    }

    private void Update()
    {
        currentCombat?.Tick();
    }

    private void FixedUpdate()
    {
        currentCombat?.FixedTick();
    }

    public void SetCombat(ICombatBase newCombat)
    {
        currentCombat?.Disable();

        newCombat?.Enable();
        currentCombat = newCombat;
    }

    public void UnsettleCombat(ICombatBase combat)
    {
        if (currentCombat != null && currentCombat.Equals(combat))
        {
            currentCombat?.Disable();
            currentCombat = null;
        }
    }

    private void onItemUsed(ItemUsedEvent itemEvent)
    {
        if (itemEvent.Item is ICombatInventoryItem inventoryItem)
        {
            inventoryItem.Use(this);
        }
    }

    public Observable<(bool Allowed, string Reason)> ObserveCapability(Capability capability)
    {
        return null;
    }
}