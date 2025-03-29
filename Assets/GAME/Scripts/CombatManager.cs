using UnityEngine;

public class CombatManager : MonoBehaviour, ICombatManager
{
    public Transform characterModelTransform;

    public float CombatSpeedModifier { get; set; }
    public float DamageModifier { get; set; }
    public float RangedAttackModifier { get; set; }
    public float AccuracyModifier { get; set; }
    public float CritModifier { get; set; }

    [SerializeField] private CharacterPromptReceiver promptReceiver;

    private ICombatBase currentCombat;
    private IHumanoidCombatPromptReceiver _promptReceiver => promptReceiver;

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

    private void FixedUpdate()
    {
        currentCombat?.FixedTick();
    }

    public void SetCombat(ICombatBase newCombat)
    {
        currentCombat?.End();
        newCombat?.Init(this, _promptReceiver);
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