public class ItemUsedEvent
{   
    public IInventoryItem Item { get; }

    public ItemUsedEvent(IInventoryItem item)
    {
        Item = item;
    }
}