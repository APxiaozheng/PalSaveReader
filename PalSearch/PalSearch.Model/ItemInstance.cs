namespace PalSearch.Model
{
    public class ItemInstance
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public int StackCount { get; set; }
        public string ContainerId { get; set; }
        public ItemLocation Location { get; set; }
    }

    public class ItemLocation
    {
        public string ContainerId { get; set; }
        public ItemContainerType ContainerType { get; set; }
        public string ContainerName { get; set; }
        public string BaseId { get; set; }
        public string BaseName { get; set; }
        public WorldCoord Position { get; set; }
        public int SlotIndex { get; set; }
    }

    public enum ItemContainerType
    {
        PlayerInventory,
        BaseStorage,
        WorldContainer,
        Unknown
    }
}