using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalSearch.Model;
using PalSearch.SaveReader;
using PalSearch.SaveReader.SaveFile;
using PalSearch.UI.Localization;
using PalSearch.UI.Model;
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

        // Map
        [ObservableProperty] private ObservableCollection<MapMarkerViewModel> mapMarkers = new();
        [ObservableProperty] private MapMarkerViewModel hoveredMarker;

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

            IsLoading = false;
            StatusText = $"Loaded: {option.DisplayName}";
        }

        private void PopulateData(CachedSaveData data)
        {
            // Items
            AllItems.Clear();
            var itemGroups = data.Items
                .Where(i => i.StackCount > 0)
                .GroupBy(i => i.ItemId)
                .OrderByDescending(g => g.Sum(i => i.StackCount));

            foreach (var group in itemGroups)
            {
                foreach (var item in group.OrderByDescending(i => i.StackCount))
                {
                    AllItems.Add(new ItemViewModel
                    {
                        ItemId = item.ItemId,
                        Name = item.Name ?? item.ItemId,
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
            }

            TotalItemCount = data.Items.Sum(i => i.StackCount);
            UniqueItemCount = data.Items.Select(i => i.ItemId).Distinct().Count();
            FilterItems();

            // Pals
            AllPals.Clear();
            var locale = CurrentLocaleKey;
            foreach (var pal in data.Pals.OrderBy(p => p.Pal?.GetLocalizedName(locale) ?? "Unknown"))
            {
                AllPals.Add(new PalViewModel
                {
                    InstanceId = pal.InstanceId,
                    Name = pal.Pal?.GetLocalizedName(locale) ?? "Unknown",
                    NickName = pal.NickName,
                    Level = pal.Level,
                    Gender = pal.Gender.ToString(),
                    IV_HP = pal.IV_HP,
                    IV_Attack = pal.IV_Attack,
                    IV_Defense = pal.IV_Defense,
                    PassiveSkills = string.Join(", ", pal.PassiveSkills?.Select(p => p.GetLocalizedName(locale)) ?? []),
                    ActiveSkills = string.Join(", ", pal.ActiveSkills?.Select(s => s.GetLocalizedName(locale)) ?? []),
                    EquippedActiveSkills = string.Join(", ", pal.EquippedActiveSkills?.Select(s => s.GetLocalizedName(locale)) ?? []),
                    Rank = pal.Rank,
                    LocationType = pal.Location?.Type.ToString() ?? "Unknown",
                    LocationIndex = pal.Location?.Index ?? 0,
                    OwnerName = data.Players.FirstOrDefault(p => p.PlayerId == pal.OwnerPlayerId)?.Name ?? "Unknown",
                    ContainerId = pal.Location?.ContainerId ?? ""
                });
            }

            TotalPalCount = data.Pals.Count;
            UniquePalCount = data.Pals.Select(p => p.Pal?.Name).Distinct().Count();
            FilterPals();

            // Map markers
            MapMarkers.Clear();
            foreach (var baseInst in data.Bases)
            {
                if (baseInst.Position != null)
                {
                    MapMarkers.Add(new MapMarkerViewModel
                    {
                        Name = $"Base {baseInst.Id}",
                        X = baseInst.Position.X,
                        Y = baseInst.Position.Y,
                        Z = baseInst.Position.Z,
                        MarkerType = "Base"
                    });
                }
            }
        }

        private void FilterItems()
        {
            FilteredItems.Clear();
            var query = ItemSearchText?.ToLower() ?? "";
            foreach (var item in AllItems)
            {
                if (string.IsNullOrEmpty(query) ||
                    item.Name.ToLower().Contains(query) ||
                    item.ItemId.ToLower().Contains(query) ||
                    item.ContainerName.ToLower().Contains(query) ||
                    item.BaseName.ToLower().Contains(query))
                {
                    FilteredItems.Add(item);
                }
            }
        }

        private void FilterPals()
        {
            FilteredPals.Clear();
            var query = PalSearchText?.ToLower() ?? "";
            var passiveQuery = PassiveSearchText?.ToLower() ?? "";
            var skillQuery = ActiveSkillSearchText?.ToLower() ?? "";

            foreach (var pal in AllPals)
            {
                bool matchesName = string.IsNullOrEmpty(query) || pal.Name.ToLower().Contains(query) || (pal.NickName?.ToLower().Contains(query) ?? false);
                bool matchesIv = pal.IV_HP >= MinIvHp && pal.IV_Attack >= MinIvAttack && pal.IV_Defense >= MinIvDefense;
                bool matchesPassive = string.IsNullOrEmpty(passiveQuery) || pal.PassiveSkills.ToLower().Contains(passiveQuery);
                bool matchesSkill = string.IsNullOrEmpty(skillQuery) || pal.ActiveSkills.ToLower().Contains(skillQuery) || pal.EquippedActiveSkills.ToLower().Contains(skillQuery);

                if (matchesName && matchesIv && matchesPassive && matchesSkill)
                    FilteredPals.Add(pal);
            }
        }

        partial void OnSelectedLocaleChanged(TranslationLocale value)
        {
            Translator.CurrentLocale = value;
            if (currentData != null)
                PopulateData(currentData);
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
    }

    public class MapMarkerViewModel
    {
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string MarkerType { get; set; }
    }
}