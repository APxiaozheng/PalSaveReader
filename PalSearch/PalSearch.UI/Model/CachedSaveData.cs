using PalSearch.Model;
using System.Collections.Generic;

namespace PalSearch.UI.Model
{
    public class CachedSaveData
    {
        public List<PalInstance> Pals { get; set; } = new();
        public List<PlayerInstance> Players { get; set; } = new();
        public List<BaseInstance> Bases { get; set; } = new();
        public List<IPalContainer> PalContainers { get; set; } = new();
        public List<GuildInstance> Guilds { get; set; } = new();
        public List<ItemInstance> Items { get; set; } = new();
    }
}