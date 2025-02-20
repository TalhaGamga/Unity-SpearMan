using UnityEngine;

public class CombatManager : MonoBehaviour, ICombatManager
{
    private ICombat currentCombat;

    private void OnEnable()
    {
        EventBus.OnItemUsed += onItemUsed;
    }

    private void OnDisable()
    {
        EventBus.OnItemUsed -= onItemUsed;
    }

    public void SetCombat(ICombat newCombat)
    {
        currentCombat?.End();
        newCombat?.Init(this);
        currentCombat = newCombat;
    }

    public void UnsettleCombat(ICombat combat)
    {
        if (currentCombat != null && currentCombat.Equals(combat))
        {
            currentCombat?.End();
            currentCombat = null;
        }
    }

    private void Update()
    {
        currentCombat?.Tick();
    }
    private void onItemUsed(ItemUsedEvent itemEvent)
    {
        if (itemEvent.Item is ICombatInventoryItem inventoryItem)
        {
            inventoryItem.Use(this);
        }
    }
}