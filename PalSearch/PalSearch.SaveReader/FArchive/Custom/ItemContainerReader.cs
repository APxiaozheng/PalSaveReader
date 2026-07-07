using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalSearch.SaveReader.FArchive.Custom
{
    public class ItemContainerDataPropertyMeta : BasicPropertyMeta
    {
        public Guid ContainerId { get; set; }
    }

    public class ItemContainerDataProperty : IProperty
    {
        public IPropertyMeta Meta { get; set; }
        public ItemContainerDataPropertyMeta TypedMeta { get; set; }
        public List<ItemSlotData> Slots { get; set; }

        public void Traverse(Action<IProperty> action) { }
    }

    public class ItemSlotData
    {
        public int SlotIndex { get; set; }
        public string ItemId { get; set; }
        public int StackCount { get; set; }
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

            var slots = new List<ItemSlotData>();

            // Try old binary format first (ContainerId + slotCount + slots)
            // If the remaining data is too small, try reading properties until end
            var remaining = subReader.BaseStream.Length - subReader.BaseStream.Position;
            if (remaining >= 4)
            {
                int slotCount;
                try
                {
                    slotCount = subReader.ReadInt32();
                }
                catch (EndOfStreamException)
                {
                    logger.Warning("ItemContainerReader: failed to read slotCount (end of stream), returning empty container");
                    return BuildResult(meta, slots, path, visitors);
                }

                for (int i = 0; i < slotCount; i++)
                {
                    try
                    {
                        slots.Add(new ItemSlotData
                        {
                            SlotIndex = subReader.ReadInt32(),
                            ItemId = subReader.ReadString(),
                            StackCount = subReader.ReadInt32()
                        });
                    }
                    catch (Exception ex) when (ex is EndOfStreamException || ex is ArgumentOutOfRangeException)
                    {
                        logger.Warning(ex, "ItemContainerReader: failed to read slot {i}, stopping iteration", i);
                        break;
                    }
                }
            }

            return BuildResult(meta, slots, path, visitors);
        }

        private IProperty BuildResult(ItemContainerDataPropertyMeta meta, List<ItemSlotData> slots, string path, IEnumerable<IVisitor> visitors)
        {
            var result = new ItemContainerDataProperty
            {
                TypedMeta = meta,
                Slots = slots
            };

            foreach (var v in visitors.Where(v => v.Matches(path)))
                v.VisitItemContainerProperty(path, result);

            logger.Verbose("done (slots: {count})", slots.Count);
            return result;
        }
    }
}