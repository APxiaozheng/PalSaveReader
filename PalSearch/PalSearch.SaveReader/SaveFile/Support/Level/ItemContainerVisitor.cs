using PalSearch.SaveReader.FArchive;
using PalSearch.SaveReader.FArchive.Custom;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalSearch.SaveReader.SaveFile.Support.Level
{
    public class RawItemSlot
    {
        public int SlotIndex { get; set; }
        public string ItemId { get; set; }
        public int StackCount { get; set; }
    }

    public class RawItemContainer
    {
        public string ContainerId { get; set; }
        public List<RawItemSlot> Slots { get; set; } = new();
    }

    public class ItemContainerCollectorVisitor : IVisitor
    {
        private static ILogger logger = Log.ForContext<ItemContainerCollectorVisitor>();

        public List<RawItemContainer> CollectedContainers { get; } = new();

        RawItemContainer workingContainer;

        public ItemContainerCollectorVisitor() : base(".worldSaveData.ItemContainerSaveData")
        {
        }

        class SlotCollectingVisitor : IVisitor
        {
            public SlotCollectingVisitor(string path) : base(path, "Value.RawData")
            {
            }

            public Action<List<RawItemSlot>> OnSlotData;

            public override bool Matches(string path) => path.StartsWith(MatchedPath);

            public override void VisitItemContainerProperty(string path, ItemContainerDataProperty property)
            {
                var slots = property.Slots.Select(s => new RawItemSlot
                {
                    SlotIndex = s.SlotIndex,
                    ItemId = s.ItemId,
                    StackCount = s.StackCount
                }).ToList();

                OnSlotData?.Invoke(slots);
            }
        }

        public override IEnumerable<IVisitor> VisitMapEntryBegin(string path, int index, MapPropertyMeta meta)
        {
            logger.Verbose("map entry begin");

            workingContainer = new RawItemContainer();

            var keyCollector = new ValueCollectingVisitor(this, isCaseSensitive: true, ".Key.ID");
            keyCollector.OnExit += v =>
            {
                workingContainer.ContainerId = v[".Key.ID"].ToString();
            };

            var slotCollector = new SlotCollectingVisitor(path);
            slotCollector.OnSlotData += slots =>
            {
                workingContainer.Slots = slots;
            };

            yield return keyCollector;
            yield return slotCollector;
        }

        public override void VisitMapEntryEnd(string path, int index, MapPropertyMeta meta)
        {
            logger.Verbose("map entry end");

            CollectedContainers.Add(workingContainer);
            workingContainer = null;
        }
    }
}