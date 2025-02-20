using System;

public static class EventBus
{
    public static event Action<ItemUsedEvent> OnItemUsed;
    public static void RaiseItemUsed(ItemUsedEvent itemUsedEvent)
    {
        OnItemUsed?.Invoke(itemUsedEvent);
    }
}