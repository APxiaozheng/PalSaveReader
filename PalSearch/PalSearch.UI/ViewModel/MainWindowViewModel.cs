using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalSearch.Model;
using PalSearch.SaveReader;
using PalSearch.SaveReader.SaveFile;
using PalSearch.UI.Localization;
using PalSearch.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PalSearch.UI.ViewModel
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private static ILogger logger = Log.ForContext<MainWindowViewModel>();
        private PalDB db;
        private List<ISavesLocation> savesLocations;
        private Dictionary<string, CachedSaveData> loadedSaves = new();
        private CachedSaveData currentData;
        private string CurrentLocaleKey => Translator.CurrentLocale.ToFormalName();

        [ObservableProperty] private ObservableCollection<SaveGameOption> saveOptions = new();
        [ObservableProperty] private SaveGameOption selectedSave;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string statusText;
        [ObservableProperty] private int selectedTabIndex;

        // Stats
        [ObservableProperty] private int totalItemCount;
        [ObservableProperty] private int uniqueItemCount;
        [ObservableProperty] private int totalPalCount;
        [ObservableProperty] private int uniquePalCount;

        // Item search
        [ObservableProperty] private string itemSearchText;
        [ObservableProperty] private ObservableCollection<ItemViewModel> allItems = new();
        [ObservableProperty] private ObservableCollection<ItemViewModel> filteredItems = new();

        // Pal search
        [ObservableProperty] private string palSearchText;
        [ObservableProperty] private int minIvHp;
        [ObservableProperty] private int minIvAttack;
        [ObservableProperty] private int minIvDefense;
        [ObservableProperty] private string passiveSearchText;
        [ObservableProperty] private string activeSkillSearchText;
        [ObservableProperty] private ObservableCollection<PalViewModel> allPals = new();
        [ObservableProperty] private ObservableCollection<PalViewModel> filteredPals = new();

        // Localization
        public List<TranslationLocale> AvailableLocales { get; } = Enum.GetValues<TranslationLocale>().ToList();
        [ObservableProperty] private TranslationLocale selectedLocale = TranslationLocale.zhHans;

        // Commands
        public IRelayCommand RefreshSavesCommand { get; }
        public IRelayCommand LoadSaveCommand { get; }
        public IRelayCommand SearchItemsCommand { get; }
        public IRelayCommand SearchPalsCommand { get; }
        public IRelayCommand SwitchLanguageCommand { get; }

        public MainWindowViewModel()
        {
            RefreshSavesCommand = new RelayCommand(async () => await RefreshSaves());
            LoadSaveCommand = new RelayCommand<SaveGameOption>(async (opt) => await LoadSaveData(opt));
            SearchItemsCommand = new RelayCommand(FilterItems);
            SearchPalsCommand = new RelayCommand(FilterPals);
            SwitchLanguageCommand = new RelayCommand(SwitchLanguage);
        }

        public async Task InitializeAsync()
        {
            Translator.CurrentLocale = SelectedLocale;
            db = PalDB.LoadEmbedded();
            await RefreshSaves();
        }

        private async Task RefreshSaves()
        {
            IsLoading = true;
            StatusText = Translator.Get("LC_SAVE_LOADING");
            
            await Task.Run(() =>
            {
                savesLocations = new List<ISavesLocation>();
                savesLocations.AddRange(DirectSavesLocation.AllLocal);
            });

            SaveOptions.Clear();
            foreach (var loc in savesLocations)
            {
                foreach (var save in loc.ValidSaveGames)
                {
                    try
                    {
                        var meta = save.LevelMeta.ReadGameOptions();
                        SaveOptions.Add(new SaveGameOption
                        {
                            SaveGame = save,
                            DisplayName = meta?.ToString() ?? save.GameId,
                            Location = loc
                        });
                    }
                    catch { }
                }
            }

            IsLoading = false;
            StatusText = SaveOptions.Count > 0 
                ? $"{SaveOptions.Count} saves found" 
                : Translator.Get("LC_SAVE_NO_SAVES");
        }

        private async Task LoadSaveData(SaveGameOption option)
        {
            if (option == null) return;
            SelectedSave = option;
            IsLoading = true;
            StatusText = Translator.Get("LC_SAVE_LOADING");

            try
            {
                await Task.Run(() =>
                {
                    var gameSettings = GameSettings.Defaults;
                    var levelData = option.SaveGame.Level.ReadCharacterData(db, gameSettings, option.SaveGame.Players, option.Location.GlobalPalStorage);
                    var itemData = option.SaveGame.Level.ReadItemData();

                    var cachedData = new CachedSaveData
                    {
                        Pals = levelData.Pals,
                        Players = levelData.Players,
                        Bases = levelData.Bases,
                        PalContainers = levelData.PalContainers,
                        Guilds = levelData.Guilds,
                        Items = itemData
                    };

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        loadedSaves[option.SaveGame.GameId] = cachedData;
                        currentData = cachedData;
                        PopulateData(cachedData);
                    });
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load save data");
                StatusText = $"Error: {ex.Message}";
            }

            IsLoading = false;
            if (StatusText == null || !StatusText.StartsWith("Error"))
                StatusText = $"Loaded: {option.DisplayName}";
        }

        private void PopulateData(CachedSaveData data)
        {
            try
            {
                var locale = CurrentLocaleKey;

                // Items
                AllItems.Clear();
                foreach (var item in data.Items.Where(i => i.StackCount > 0)
                    .OrderByDescending(i => i.StackCount))
                {
                    AllItems.Add(new ItemViewModel
                    {
                        ItemId = item.ItemId ?? "?",
                        Name = item.Name ?? item.ItemId ?? "?",
                        StackCount = item.StackCount,
                        ContainerName = item.Location?.ContainerName ?? "Unknown",
                        BaseName = item.Location?.BaseName ?? "-",
                        Coordinates = item.Location?.Position != null
                            ? $"({item.Location.Position.X:F0}, {item.Location.Position.Y:F0}, {item.Location.Position.Z:F0})"
                            : "-",
                        ContainerType = item.Location?.ContainerType.ToString() ?? "Unknown",
                        SlotIndex = item.Location?.SlotIndex ?? 0
                    });
                }

                TotalItemCount = data.Items.Sum(i => i.StackCount);
                UniqueItemCount = data.Items.Select(i => i.ItemId).Distinct().Count();
                FilterItems();

                // Pals
                AllPals.Clear();
                var palContainers = data.PalContainers;
                foreach (var pal in data.Pals.OrderBy(p => p.Pal?.GetLocalizedName(locale) ?? "Unknown"))
                {
                    var location = pal.Location;
                    PalDisplayCoord displayCoord = null;
                    if (location != null)
                    {
                        try { displayCoord = PalDisplayCoord.FromLocation(GameSettings.Defaults, location); }
                        catch { /* ignore invalid location */ }
                    }

                    AllPals.Add(new PalViewModel
                    {
                        InstanceId = pal.InstanceId ?? "?",
                        Name = pal.Pal?.GetLocalizedName(locale) ?? "Unknown",
                        NickName = pal.NickName ?? "",
                        Level = pal.Level,
                        Gender = pal.Gender.ToString(),
                        IV_HP = pal.IV_HP,
                        IV_Attack = pal.IV_Attack,
                        IV_Defense = pal.IV_Defense,
                        PassiveSkills = string.Join(", ", pal.PassiveSkills?.Select(p => p.GetLocalizedName(locale)) ?? []),
                        ActiveSkills = string.Join(", ", pal.ActiveSkills?.Select(s => s.GetLocalizedName(locale)) ?? []),
                        EquippedActiveSkills = string.Join(", ", pal.EquippedActiveSkills?.Select(s => s.GetLocalizedName(locale)) ?? []),
                        Rank = pal.Rank,
                        LocationType = location?.Type.ToString() ?? "Unknown",
                        LocationIndex = location?.Index ?? 0,
                        OwnerName = data.Players.FirstOrDefault(p => p.PlayerId == pal.OwnerPlayerId)?.Name ?? "Unknown",
                        ContainerId = location?.ContainerId ?? "",
                        DisplayCoord = displayCoord
                    });
                }

                TotalPalCount = data.Pals.Count;
                UniquePalCount = data.Pals.Select(p => p.Pal?.Name).Distinct().Count();
                FilterPals();

                logger.Information("Populated {itemCount} items and {palCount} pals", AllItems.Count, AllPals.Count);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to populate data");
                AllItems.Clear();
                AllPals.Clear();
                FilterItems();
                FilterPals();
            }
        }

        private void FilterItems()
        {
            FilteredItems.Clear();
            var query = ItemSearchText?.Trim().ToLower() ?? "";
            foreach (var item in AllItems)
            {
                if (string.IsNullOrEmpty(query) ||
                    MatchesFuzzy(item.Name, query) ||
                    MatchesFuzzy(item.ItemId, query) ||
                    MatchesFuzzy(item.ContainerName, query) ||
                    MatchesFuzzy(item.BaseName, query) ||
                    MatchesFuzzy(item.ContainerType, query) ||
                    MatchesFuzzy(item.SlotIndex.ToString(), query))
                {
                    FilteredItems.Add(item);
                }
            }
        }

        private void FilterPals()
        {
            FilteredPals.Clear();
            var query = PalSearchText?.Trim().ToLower() ?? "";
            var passiveQuery = PassiveSearchText?.Trim().ToLower() ?? "";
            var skillQuery = ActiveSkillSearchText?.Trim().ToLower() ?? "";

            foreach (var pal in AllPals)
            {
                bool matchesName = string.IsNullOrEmpty(query) || 
                    MatchesFuzzy(pal.Name, query) || 
                    MatchesFuzzy(pal.NickName ?? "", query) ||
                    MatchesFuzzy(pal.LocationType, query) ||
                    MatchesFuzzy(pal.OwnerName, query) ||
                    MatchesFuzzy(pal.Gender, query);
                bool matchesIv = pal.IV_HP >= MinIvHp && pal.IV_Attack >= MinIvAttack && pal.IV_Defense >= MinIvDefense;
                bool matchesPassive = string.IsNullOrEmpty(passiveQuery) || MatchesFuzzy(pal.PassiveSkills, passiveQuery);
                bool matchesSkill = string.IsNullOrEmpty(skillQuery) || 
                    MatchesFuzzy(pal.ActiveSkills, skillQuery) || 
                    MatchesFuzzy(pal.EquippedActiveSkills, skillQuery);

                if (matchesName && matchesIv && matchesPassive && matchesSkill)
                    FilteredPals.Add(pal);
            }
        }

        private static bool MatchesFuzzy(string source, string query)
        {
            if (string.IsNullOrEmpty(source)) return false;
            return source.ToLower().Contains(query);
        }

        partial void OnSelectedLocaleChanged(TranslationLocale value)
        {
            Translator.CurrentLocale = value;
            if (currentData != null)
                PopulateData(currentData);
        }

        partial void OnSelectedSaveChanged(SaveGameOption value)
        {
            if (value != null)
                _ = LoadSaveData(value);
        }

        private void SwitchLanguage()
        {
            var locales = AvailableLocales;
            var idx = locales.IndexOf(SelectedLocale);
            SelectedLocale = locales[(idx + 1) % locales.Count];
        }

        partial void OnItemSearchTextChanged(string value) => FilterItems();
        partial void OnPalSearchTextChanged(string value) => FilterPals();
        partial void OnMinIvHpChanged(int value) => FilterPals();
        partial void OnMinIvAttackChanged(int value) => FilterPals();
        partial void OnMinIvDefenseChanged(int value) => FilterPals();
        partial void OnPassiveSearchTextChanged(string value) => FilterPals();
        partial void OnActiveSkillSearchTextChanged(string value) => FilterPals();
    }

    public class SaveGameOption
    {
        public ISaveGame SaveGame { get; set; }
        public string DisplayName { get; set; }
        public ISavesLocation Location { get; set; }
        public override string ToString() => DisplayName;
    }

    public class ItemViewModel
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public int StackCount { get; set; }
        public string ContainerName { get; set; }
        public string BaseName { get; set; }
        public string Coordinates { get; set; }
        public string ContainerType { get; set; }
        public int SlotIndex { get; set; }

        public string LocationTooltip =>
            $"Container: {ContainerName}\n" +
            $"Type: {ContainerType}\n" +
            $"Slot: {SlotIndex}\n" +
            $"Base: {BaseName}\n" +
            $"Coords: {Coordinates}";
    }

    public class PalViewModel
    {
        public string InstanceId { get; set; }
        public string Name { get; set; }
        public string NickName { get; set; }
        public int Level { get; set; }
        public string Gender { get; set; }
        public int IV_HP { get; set; }
        public int IV_Attack { get; set; }
        public int IV_Defense { get; set; }
        public string PassiveSkills { get; set; }
        public string ActiveSkills { get; set; }
        public string EquippedActiveSkills { get; set; }
        public int Rank { get; set; }
        public string LocationType { get; set; }
        public int LocationIndex { get; set; }
        public string OwnerName { get; set; }
        public string ContainerId { get; set; }
        public PalDisplayCoord DisplayCoord { get; set; }

        // IV 百分比（0-100 的条形图宽度）
        public double IvHpPercent => IV_HP / 100.0;
        public double IvAttackPercent => IV_Attack / 100.0;
        public double IvDefensePercent => IV_Defense / 100.0;

        // 性别图标
        public string GenderIcon => Gender switch
        {
            "Male" => "♂",
            "Female" => "♀",
            _ => "?"
        };

        // 等级显示
        public string LevelDisplay => $"Lv.{Level}";

        // 被动技能列表（分割）
        public string[] PassiveList => string.IsNullOrEmpty(PassiveSkills) 
            ? Array.Empty<string>() 
            : PassiveSkills.Split(',').Select(s => s.Trim()).ToArray();

        public string[] ActiveSkillList => string.IsNullOrEmpty(ActiveSkills)
            ? Array.Empty<string>()
            : ActiveSkills.Split(',').Select(s => s.Trim()).ToArray();

        // 收纳盒网格数据（用于可视化）
        public int PalboxTab => DisplayCoord?.Tab ?? -1;
        public int PalboxRow => DisplayCoord?.Row ?? -1;
        public int PalboxCol => DisplayCoord?.Col ?? -1;
        public bool HasPalboxPosition => DisplayCoord != null;

        public string LocationTooltip
        {
            get
            {
                var coordStr = DisplayCoord?.ToString() ?? $"Index {LocationIndex}";
                return $"Location: {LocationType}\n" +
                       $"Owner: {OwnerName}\n" +
                       $"Position: {coordStr}\n" +
                       $"Container: {ContainerId}";
            }
        }
    }
}