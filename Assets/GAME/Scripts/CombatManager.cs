using UnityEngine;

public class CombatManager : MonoBehaviour, ICombatManager
{
    private ICombatBase currentCombat;

    private IHumanoidCombatPromptReceiver promptReceiver;

    public float CombatSpeedModifier { get; set; }
    public float DamageModifier { get; set; }
    public float RangedAttackModifier { get; set; }
    public float AccuracyModifier { get; set; }
    public float CritModifier { get; set; }

    private void OnEnable()
    {
        EventBus.OnItemUsed += onItemUsed;
    }

    private void OnDisable()
    {
        EventBus.OnItemUsed -= onItemUsed;
    }

    private void Update()
    {
        currentCombat?.Tick();
    }

    public void SetCombat(ICombatBase newCombat)
    {
        currentCombat?.End();
        newCombat?.Init(this);
        currentCombat = newCombat;
    }

    public void UnsettleCombat(ICombatBase combat)
    {
        if (currentCombat != null && currentCombat.Equals(combat))
        {
            currentCombat?.End();
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
}