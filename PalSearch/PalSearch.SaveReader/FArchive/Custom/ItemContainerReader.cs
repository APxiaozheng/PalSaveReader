using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalSearch.SaveReader.FArchive.Custom
{
    public class ItemSlotData
    {
        public int SlotIndex { get; set; }
        public string ItemId { get; set; }
        public int StackCount { get; set; }
    }

    public class ItemContainerDataPropertyMeta : BasicPropertyMeta
    {
        public Guid ContainerId { get; set; }
    }

    public class ItemContainerDataProperty : ICustomProperty
    {
        public IPropertyMeta Meta => TypedMeta;
        public ItemContainerDataPropertyMeta TypedMeta { get; set; }

        public List<ItemSlotData> Slots { get; set; }

        public void Traverse(Action<IProperty> action) { }
    }

    public class ItemContainerReader : ICustomByteArrayReader
    {
        private static ILogger logger = Log.ForContext<ItemContainerReader>();

        public override string MatchedPath => ".worldSaveData.ItemContainerSaveData.Value.RawData";

        protected override IProperty Decode(FArchiveReader subReader, string path, IEnumerable<IVisitor> visitors)
        {
            logger.Verbose("decoding");

            var meta = new ItemContainerDataPropertyMeta
            {
                Path = path,
                ContainerId = subReader.ReadGuid(),
            };

            var slotCount = subReader.ReadInt32();
            var slots = new List<ItemSlotData>();
            for (int i = 0; i < slotCount; i++)
            {
                slots.Add(new ItemSlotData
                {
                    SlotIndex = subReader.ReadInt32(),
                    ItemId = subReader.ReadString(),
                    StackCount = subReader.ReadInt32()
                });
            }

            var result = new ItemContainerDataProperty
            {
                TypedMeta = meta,
                Slots = slots
            };

            foreach (var v in visitors.Where(v => v.Matches(path)))
                v.VisitItemContainerProperty(path, result);

            logger.Verbose("done");
            return result;
        }
    }
}