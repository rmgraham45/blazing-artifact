using System.Text.Json;
using System.Timers;
using Timer = System.Timers.Timer;
using Radzen;

namespace Artifacts.Components.Services
{
    public class CharacterService
    {
        private readonly string _token;
        private readonly NotificationService _notificationService;
        private readonly Dictionary<string, CharacterData> _characters = new();
        private readonly Dictionary<string, bool> _autoGatherStatus = new();
        private readonly Dictionary<string, Timer> _cooldownTimers = new();
        private readonly Dictionary<string, Timer> _progressTimers = new();
        private readonly Dictionary<string, (int TotalSeconds, int RemainingSeconds)> _cooldowns = new();
        private readonly Dictionary<string, double> _progressWidths = new();
        private readonly List<string> _characterNames = new();
        private string _expandedCharacter = string.Empty;

        public event Action? OnCharactersUpdated;
        public event Action? OnCooldownUpdated;

        public CharacterService(NotificationService notificationService)
        {
            _notificationService = notificationService;
            
            // Add some test characters for demonstration purposes
            _characterNames.Add("pancake");
            _characterNames.Add("Izzy");
            _characterNames.Add("Goober");
            
            // Initialize basic character data
            _characters["pancake"] = new CharacterData { Name = "pancake", Level = 10, X = 100, Y = 200 };
            _characters["Izzy"] = new CharacterData { Name = "Izzy", Level = 15, X = 150, Y = 250 };
            _characters["Goober"] = new CharacterData { Name = "Goober", Level = 20, X = 200, Y = 300 };
            
            // Initialize cooldown tracking for each character
            foreach (var name in _characterNames)
            {
                _cooldowns[name] = (0, 0);
                _progressWidths[name] = 0;
                _autoGatherStatus[name] = false;
                InitializeTimersForCharacter(name);
            }
        }

        public async Task InitializeAsync()
        {
            await FetchAllCharactersAsync();
        }

        public async Task FetchAllCharactersAsync()
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
                var response = await client.GetAsync("https://api.artifactsmmo.com/my/characters");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var charactersData = JsonDocument.Parse(jsonResponse);
                    var dataArray = charactersData.RootElement.GetProperty("data").EnumerateArray().ToList();
                    
                    // We're keeping the test characters instead of clearing them
                    // _characterNames.Clear();
                    
                    foreach (var data in dataArray)
                    {
                        var name = data.GetProperty("name").GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(name))
                        {
                            if (!_characterNames.Contains(name))
                            {
                                _characterNames.Add(name);
                            }
                            
                            // Initialize character data with basic info
                            if (!_characters.ContainsKey(name))
                            {
                                _characters[name] = new CharacterData
                                {
                                    Name = name,
                                    X = data.GetProperty("x").GetInt32(),
                                    Y = data.GetProperty("y").GetInt32()
                                };
                                
                                // Initialize cooldown tracking
                                _cooldowns[name] = (0, 0);
                                _progressWidths[name] = 0;
                                _autoGatherStatus[name] = false;
                                
                                // Initialize timers for this character
                                InitializeTimersForCharacter(name);
                            }
                            else
                            {
                                // Update position
                                _characters[name].X = data.GetProperty("x").GetInt32();
                                _characters[name].Y = data.GetProperty("y").GetInt32();
                            }
                        }
                    }
                    
                    OnCharactersUpdated?.Invoke();
                }
                else
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    if (jsonResponse.Contains("Token is expired"))
                    {
                        _notificationService.Notify(NotificationSeverity.Warning, "Authentication", "Token is expired. Using demo characters.");
                    }
                    else
                    {
                        _notificationService.Notify(NotificationSeverity.Error, "Fetch Characters", $"API Error: {response.StatusCode}");
                    }
                    
                    // We're already using demo characters, so we'll just notify the user
                    OnCharactersUpdated?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _notificationService.Notify(NotificationSeverity.Error, "Fetch Characters", $"Error: {ex.Message}");
                // Still invoke the update event to show the demo characters
                OnCharactersUpdated?.Invoke();
            }
        }

        private void InitializeTimersForCharacter(string characterName)
        {
            // Create cooldown timer
            var timer = new Timer(1000);
            timer.Elapsed += (sender, e) => CharacterTimerElapsed(characterName);
            timer.Start();
            _cooldownTimers[characterName] = timer;
            
            // Create progress timer
            var progressTimer = new Timer(2000);
            progressTimer.Elapsed += (sender, e) => ProgressTimerElapsed(characterName);
            progressTimer.Start();
            _progressTimers[characterName] = progressTimer;
        }

        private void CharacterTimerElapsed(string characterName)
        {
            _ = UpdateCharacterCoordinatesAsync(characterName);
        }

        private void ProgressTimerElapsed(string characterName)
        {
            if (_cooldowns.TryGetValue(characterName, out var cooldown) && cooldown.RemainingSeconds > 0)
            {
                _cooldowns[characterName] = (cooldown.TotalSeconds, cooldown.RemainingSeconds - 1);
                _progressWidths[characterName] = ((double)(cooldown.TotalSeconds - cooldown.RemainingSeconds) / cooldown.TotalSeconds) * 100;
                OnCooldownUpdated?.Invoke();
            }
        }

        private async Task UpdateCharacterCoordinatesAsync(string characterName)
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
                var response = await client.GetAsync("https://api.artifactsmmo.com/my/characters");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var coordinatesData = JsonDocument.Parse(jsonResponse);
                    var dataArray = coordinatesData.RootElement.GetProperty("data").EnumerateArray().ToList();
                    
                    foreach (var data in dataArray)
                    {
                        var name = data.GetProperty("name").GetString();
                        if (name == characterName && _characters.ContainsKey(characterName))
                        {
                            _characters[characterName].X = data.GetProperty("x").GetInt32();
                            _characters[characterName].Y = data.GetProperty("y").GetInt32();
                            break;
                        }
                    }
                    
                    OnCharactersUpdated?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating coordinates for {characterName}: {ex.Message}");
            }
        }

        public async Task FetchCharacterDataAsync(string characterName)
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
                var url = $"https://api.artifactsmmo.com/characters/{characterName}";
                var response = await client.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var characterJson = JsonDocument.Parse(jsonResponse);
                    var character = characterJson.RootElement.GetProperty("data");
                    
                    var characterData = new CharacterData
                    {
                        Name = character.GetProperty("name").GetString() ?? string.Empty,
                        Account = character.GetProperty("account").GetString() ?? string.Empty,
                        Skin = character.GetProperty("skin").GetString() ?? string.Empty,
                        Level = character.GetProperty("level").GetInt32(),
                        Xp = character.GetProperty("xp").GetInt32(),
                        MaxXp = character.GetProperty("max_xp").GetInt32(),
                        Gold = character.GetProperty("gold").GetInt32(),
                        Speed = character.GetProperty("speed").GetInt32(),
                        MiningLevel = character.GetProperty("mining_level").GetInt32(),
                        MiningXp = character.GetProperty("mining_xp").GetInt32(),
                        MiningMaxXp = character.GetProperty("mining_max_xp").GetInt32(),
                        WoodcuttingLevel = character.GetProperty("woodcutting_level").GetInt32(),
                        WoodcuttingXp = character.GetProperty("woodcutting_xp").GetInt32(),
                        WoodcuttingMaxXp = character.GetProperty("woodcutting_max_xp").GetInt32(),
                        FishingLevel = character.GetProperty("fishing_level").GetInt32(),
                        FishingXp = character.GetProperty("fishing_xp").GetInt32(),
                        FishingMaxXp = character.GetProperty("fishing_max_xp").GetInt32(),
                        WeaponcraftingLevel = character.GetProperty("weaponcrafting_level").GetInt32(),
                        WeaponcraftingXp = character.GetProperty("weaponcrafting_xp").GetInt32(),
                        WeaponcraftingMaxXp = character.GetProperty("weaponcrafting_max_xp").GetInt32(),
                        GearcraftingLevel = character.GetProperty("gearcrafting_level").GetInt32(),
                        GearcraftingXp = character.GetProperty("gearcrafting_xp").GetInt32(),
                        GearcraftingMaxXp = character.GetProperty("gearcrafting_max_xp").GetInt32(),
                        JewelrycraftingLevel = character.GetProperty("jewelrycrafting_level").GetInt32(),
                        JewelrycraftingXp = character.GetProperty("jewelrycrafting_xp").GetInt32(),
                        JewelrycraftingMaxXp = character.GetProperty("jewelrycrafting_max_xp").GetInt32(),
                        CookingLevel = character.GetProperty("cooking_level").GetInt32(),
                        CookingXp = character.GetProperty("cooking_xp").GetInt32(),
                        CookingMaxXp = character.GetProperty("cooking_max_xp").GetInt32(),
                        AlchemyLevel = character.GetProperty("alchemy_level").GetInt32(),
                        AlchemyXp = character.GetProperty("alchemy_xp").GetInt32(),
                        AlchemyMaxXp = character.GetProperty("alchemy_max_xp").GetInt32(),
                        Hp = character.GetProperty("hp").GetInt32(),
                        MaxHp = character.GetProperty("max_hp").GetInt32(),
                        Haste = character.GetProperty("haste").GetInt32(),
                        CriticalStrike = character.GetProperty("critical_strike").GetInt32(),
                        Wisdom = character.GetProperty("wisdom").GetInt32(),
                        Prospecting = character.GetProperty("prospecting").GetInt32(),
                        AttackFire = character.GetProperty("attack_fire").GetInt32(),
                        AttackEarth = character.GetProperty("attack_earth").GetInt32(),
                        AttackWater = character.GetProperty("attack_water").GetInt32(),
                        AttackAir = character.GetProperty("attack_air").GetInt32(),
                        Dmg = character.GetProperty("dmg").GetInt32(),
                        DmgFire = character.GetProperty("dmg_fire").GetInt32(),
                        DmgEarth = character.GetProperty("dmg_earth").GetInt32(),
                        DmgWater = character.GetProperty("dmg_water").GetInt32(),
                        DmgAir = character.GetProperty("dmg_air").GetInt32(),
                        ResFire = character.GetProperty("res_fire").GetInt32(),
                        ResEarth = character.GetProperty("res_earth").GetInt32(),
                        ResWater = character.GetProperty("res_water").GetInt32(),
                        ResAir = character.GetProperty("res_air").GetInt32(),
                        X = character.GetProperty("x").GetInt32(),
                        Y = character.GetProperty("y").GetInt32(),
                        Cooldown = character.GetProperty("cooldown").GetInt32(),
                        CooldownExpiration = character.GetProperty("cooldown_expiration").GetDateTime(),
                        WeaponSlot = character.GetProperty("weapon_slot").GetString() ?? string.Empty,
                        RuneSlot = character.GetProperty("rune_slot").GetString() ?? string.Empty,
                        ShieldSlot = character.GetProperty("shield_slot").GetString() ?? string.Empty,
                        HelmetSlot = character.GetProperty("helmet_slot").GetString() ?? string.Empty,
                        BodyArmorSlot = character.GetProperty("body_armor_slot").GetString() ?? string.Empty,
                        LegArmorSlot = character.GetProperty("leg_armor_slot").GetString() ?? string.Empty,
                        BootsSlot = character.GetProperty("boots_slot").GetString() ?? string.Empty,
                        Ring1Slot = character.GetProperty("ring1_slot").GetString() ?? string.Empty,
                        Ring2Slot = character.GetProperty("ring2_slot").GetString() ?? string.Empty,
                        AmuletSlot = character.GetProperty("amulet_slot").GetString() ?? string.Empty,
                        Artifact1Slot = character.GetProperty("artifact1_slot").GetString() ?? string.Empty,
                        Artifact2Slot = character.GetProperty("artifact2_slot").GetString() ?? string.Empty,
                        Artifact3Slot = character.GetProperty("artifact3_slot").GetString() ?? string.Empty,
                        Utility1Slot = character.GetProperty("utility1_slot").GetString() ?? string.Empty,
                        Utility1SlotQuantity = character.GetProperty("utility1_slot_quantity").GetInt32(),
                        Utility2Slot = character.GetProperty("utility2_slot").GetString() ?? string.Empty,
                        Utility2SlotQuantity = character.GetProperty("utility2_slot_quantity").GetInt32(),
                        BagSlot = character.GetProperty("bag_slot").GetString() ?? string.Empty,
                        Task = character.GetProperty("task").GetString() ?? string.Empty,
                        TaskType = character.GetProperty("task_type").GetString() ?? string.Empty,
                        TaskProgress = character.GetProperty("task_progress").GetInt32(),
                        TaskTotal = character.GetProperty("task_total").GetInt32(),
                        InventoryMaxItems = character.GetProperty("inventory_max_items").GetInt32(),
                        Inventory = character.GetProperty("inventory").EnumerateArray().Select(item => new InventoryItem
                        {
                            Slot = item.GetProperty("slot").GetInt32(),
                            Code = item.GetProperty("code").GetString() ?? string.Empty,
                            Quantity = item.GetProperty("quantity").GetInt32()
                        }).Where(x => x.Quantity > 0).ToList()
                    };
                    
                    _characters[characterName] = characterData;
                    OnCharactersUpdated?.Invoke();
                }
                else
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    if (jsonResponse.Contains("Token is expired"))
                    {
                        _notificationService.Notify(NotificationSeverity.Warning, "Authentication", "Token is expired. Using demo character data.");
                        
                        // Generate demo character data
                        PopulateDemoCharacterData(characterName);
                    }
                    else
                    {
                        _notificationService.Notify(NotificationSeverity.Error, "Fetch Character Data", $"Failed: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                _notificationService.Notify(NotificationSeverity.Error, "Fetch Character Data", $"Error: {ex.Message}");
                
                // Generate demo character data on error
                PopulateDemoCharacterData(characterName);
            }
            
            // Ensure we invoke the update event
            OnCharactersUpdated?.Invoke();
        }
        
        private void PopulateDemoCharacterData(string characterName)
        {
            // Get the existing character data or create a new one
            var character = _characters.TryGetValue(characterName, out var existingData) 
                ? existingData 
                : new CharacterData { Name = characterName };
            
            // Generate demo data based on character name
            var random = new Random(characterName.GetHashCode());
            
            // Basic info
            character.Account = "demo_account";
            character.Skin = "default";
            character.Level = random.Next(1, 50);
            character.Xp = random.Next(1000, 5000);
            character.MaxXp = 10000;
            character.Gold = random.Next(100, 10000);
            character.Speed = random.Next(5, 20);
            
            // Skills
            character.MiningLevel = random.Next(1, 30);
            character.MiningXp = random.Next(100, 1000);
            character.MiningMaxXp = 1000;
            character.WoodcuttingLevel = random.Next(1, 30);
            character.WoodcuttingXp = random.Next(100, 1000);
            character.WoodcuttingMaxXp = 1000;
            character.FishingLevel = random.Next(1, 30);
            character.FishingXp = random.Next(100, 1000);
            character.FishingMaxXp = 1000;
            character.WeaponcraftingLevel = random.Next(1, 30);
            character.WeaponcraftingXp = random.Next(100, 1000);
            character.WeaponcraftingMaxXp = 1000;
            character.GearcraftingLevel = random.Next(1, 30);
            character.GearcraftingXp = random.Next(100, 1000);
            character.GearcraftingMaxXp = 1000;
            character.JewelrycraftingLevel = random.Next(1, 30);
            character.JewelrycraftingXp = random.Next(100, 1000);
            character.JewelrycraftingMaxXp = 1000;
            character.CookingLevel = random.Next(1, 30);
            character.CookingXp = random.Next(100, 1000);
            character.CookingMaxXp = 1000;
            character.AlchemyLevel = random.Next(1, 30);
            character.AlchemyXp = random.Next(100, 1000);
            character.AlchemyMaxXp = 1000;
            
            // Stats
            character.Hp = random.Next(50, 200);
            character.MaxHp = 200;
            character.Haste = random.Next(1, 20);
            character.CriticalStrike = random.Next(1, 20);
            character.Wisdom = random.Next(1, 20);
            character.Prospecting = random.Next(1, 20);
            character.AttackFire = random.Next(1, 20);
            character.AttackEarth = random.Next(1, 20);
            character.AttackWater = random.Next(1, 20);
            character.AttackAir = random.Next(1, 20);
            character.Dmg = random.Next(10, 50);
            character.DmgFire = random.Next(1, 20);
            character.DmgEarth = random.Next(1, 20);
            character.DmgWater = random.Next(1, 20);
            character.DmgAir = random.Next(1, 20);
            character.ResFire = random.Next(1, 20);
            character.ResEarth = random.Next(1, 20);
            character.ResWater = random.Next(1, 20);
            character.ResAir = random.Next(1, 20);
            
            // Equipment
            character.WeaponSlot = "Demo Weapon";
            character.RuneSlot = "Demo Rune";
            character.ShieldSlot = "Demo Shield";
            character.HelmetSlot = "Demo Helmet";
            character.BodyArmorSlot = "Demo Body Armor";
            character.LegArmorSlot = "Demo Leg Armor";
            character.BootsSlot = "Demo Boots";
            character.Ring1Slot = "Demo Ring 1";
            character.Ring2Slot = "Demo Ring 2";
            character.AmuletSlot = "Demo Amulet";
            character.Artifact1Slot = "Demo Artifact 1";
            character.Artifact2Slot = "Demo Artifact 2";
            character.Artifact3Slot = "Demo Artifact 3";
            character.Utility1Slot = "Demo Utility 1";
            character.Utility1SlotQuantity = random.Next(1, 10);
            character.Utility2Slot = "Demo Utility 2";
            character.Utility2SlotQuantity = random.Next(1, 10);
            character.BagSlot = "Demo Bag";
            
            // Task
            character.Task = "Demo Task";
            character.TaskType = "Gathering";
            character.TaskProgress = random.Next(1, 10);
            character.TaskTotal = 10;
            
            // Inventory
            character.InventoryMaxItems = 20;
            character.Inventory = new List<InventoryItem>
            {
                new InventoryItem { Slot = 1, Code = "wood", Quantity = random.Next(1, 50) },
                new InventoryItem { Slot = 2, Code = "stone", Quantity = random.Next(1, 50) },
                new InventoryItem { Slot = 3, Code = "iron_ore", Quantity = random.Next(1, 30) },
                new InventoryItem { Slot = 4, Code = "gold_ore", Quantity = random.Next(1, 20) },
                new InventoryItem { Slot = 5, Code = "fish", Quantity = random.Next(1, 15) },
                new InventoryItem { Slot = 6, Code = "potion", Quantity = random.Next(1, 5) }
            };
            
            // Update the character data
            _characters[characterName] = character;
        }

    public async Task MoveActionAsync(string characterName, int x, int y)
    {
        try
        {
            Console.WriteLine("Here");
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(new { x, y }),
                System.Text.Encoding.UTF8,
                "application/json");
                
            var response = await client.PostAsync($"https://api.artifactsmmo.com/my/{characterName}/action/move", content);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var jsonDocument = JsonDocument.Parse(jsonResponse);
                var totalCooldownSeconds = jsonDocument.RootElement.GetProperty("data").GetProperty("cooldown").GetProperty("remaining_seconds").GetInt32();
                _cooldowns[characterName] = (totalCooldownSeconds, totalCooldownSeconds);
                _progressWidths[characterName] = 0;
                
                _notificationService.Notify(NotificationSeverity.Success, "Move Action", $"{characterName} move success");
                OnCooldownUpdated?.Invoke();
            }
            else
            {
                _notificationService.Notify(NotificationSeverity.Error, "Move Action", $"{characterName}: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _notificationService.Notify(NotificationSeverity.Error, "Move Action", $"Error: {ex.Message}");
        }
    }

        public async Task GatherActionAsync(string characterName)
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
                
                var response = await client.PostAsync($"https://api.artifactsmmo.com/my/{characterName}/action/gathering", new StringContent("", System.Text.Encoding.UTF8, "application/json"));
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var jsonDocument = JsonDocument.Parse(jsonResponse);
                    var totalCooldownSeconds = jsonDocument.RootElement.GetProperty("data").GetProperty("cooldown").GetProperty("remaining_seconds").GetInt32();
                    _cooldowns[characterName] = (totalCooldownSeconds, totalCooldownSeconds);
                    _progressWidths[characterName] = 0;
                    
                    _notificationService.Notify(NotificationSeverity.Success, "Gather Action", $"{characterName}: {totalCooldownSeconds}s");
                    OnCooldownUpdated?.Invoke();
                }
                else
                {
                    if ((int)response.StatusCode == 497)
                    {
                        _autoGatherStatus[characterName] = false;
                    }
                    _notificationService.Notify(NotificationSeverity.Error, "Gather Action", $"{characterName}: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _notificationService.Notify(NotificationSeverity.Error, "Gather Action", $"Error: {ex.Message}");
            }
        }

        public async Task CraftItemActionAsync(string characterName, string itemCode, int quantity)
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
                
                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new { code = itemCode, quantity }),
                    System.Text.Encoding.UTF8,
                    "application/json");
                    
                var response = await client.PostAsync($"https://api.artifactsmmo.com/my/{characterName}/action/crafting", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var jsonDocument = JsonDocument.Parse(jsonResponse);
                    var totalCooldownSeconds = jsonDocument.RootElement.GetProperty("data").GetProperty("cooldown").GetProperty("remaining_seconds").GetInt32();
                    _cooldowns[characterName] = (totalCooldownSeconds, totalCooldownSeconds);
                    _progressWidths[characterName] = 0;
                    
                    _notificationService.Notify(NotificationSeverity.Success, "Craft Action", $"{characterName} craft success");
                    OnCooldownUpdated?.Invoke();
                }
                else
                {
                    _notificationService.Notify(NotificationSeverity.Error, "Craft Action", $"{characterName}: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _notificationService.Notify(NotificationSeverity.Error, "Craft Action", $"Error: {ex.Message}");
            }
        }

        public async Task ToggleAutoGatherAsync(string characterName)
        {
            _autoGatherStatus[characterName] = !_autoGatherStatus[characterName];
            
            while (_autoGatherStatus[characterName])
            {
                await GatherActionAsync(characterName);
                
                // Wait for cooldown to complete
                if (_cooldowns.TryGetValue(characterName, out var cooldown) && cooldown.RemainingSeconds > 0)
                {
                    await Task.Delay(cooldown.RemainingSeconds * 1000);
                }
                else
                {
                    await Task.Delay(1000); // Prevent tight loop if cooldown info is missing
                }
            }
        }

        public List<string> GetCharacterNames() => _characterNames;
        
        public CharacterData? GetCharacterData(string characterName)
        {
            return _characters.TryGetValue(characterName, out var data) ? data : null;
        }
        
        public double GetProgressWidth(string characterName)
        {
            return _progressWidths.TryGetValue(characterName, out var width) ? width : 0;
        }
        
        public bool IsActionButtonsDisabled(string characterName)
        {
            return _cooldowns.TryGetValue(characterName, out var cooldown) && cooldown.RemainingSeconds > 0;
        }
        
        public bool IsAutoGatherEnabled(string characterName)
        {
            return _autoGatherStatus.TryGetValue(characterName, out var status) && status;
        }
        
        public string GetExpandedCharacter() => _expandedCharacter;
        
        public void SetExpandedCharacter(string characterName)
        {
            _expandedCharacter = characterName;
            OnCharactersUpdated?.Invoke();
        }
        
        public void Dispose()
        {
            foreach (var timer in _cooldownTimers.Values)
            {
                timer.Stop();
                timer.Dispose();
            }
            
            foreach (var timer in _progressTimers.Values)
            {
                timer.Stop();
                timer.Dispose();
            }
        }
    }

    public class CharacterData
    {
        public required string Name { get; set; }
        public string Account { get; set; } = string.Empty;
        public string Skin { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Xp { get; set; }
        public int MaxXp { get; set; }
        public int Gold { get; set; }
        public int Speed { get; set; }
        public int MiningLevel { get; set; }
        public int MiningXp { get; set; }
        public int MiningMaxXp { get; set; }
        public int WoodcuttingLevel { get; set; }
        public int WoodcuttingXp { get; set; }
        public int WoodcuttingMaxXp { get; set; }
        public int FishingLevel { get; set; }
        public int FishingXp { get; set; }
        public int FishingMaxXp { get; set; }
        public int WeaponcraftingLevel { get; set; }
        public int WeaponcraftingXp { get; set; }
        public int WeaponcraftingMaxXp { get; set; }
        public int GearcraftingLevel { get; set; }
        public int GearcraftingXp { get; set; }
        public int GearcraftingMaxXp { get; set; }
        public int JewelrycraftingLevel { get; set; }
        public int JewelrycraftingXp { get; set; }
        public int JewelrycraftingMaxXp { get; set; }
        public int CookingLevel { get; set; }
        public int CookingXp { get; set; }
        public int CookingMaxXp { get; set; }
        public int AlchemyLevel { get; set; }
        public int AlchemyXp { get; set; }
        public int AlchemyMaxXp { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int Haste { get; set; }
        public int CriticalStrike { get; set; }
        public int Wisdom { get; set; }
        public int Prospecting { get; set; }
        public int AttackFire { get; set; }
        public int AttackEarth { get; set; }
        public int AttackWater { get; set; }
        public int AttackAir { get; set; }
        public int Dmg { get; set; }
        public int DmgFire { get; set; }
        public int DmgEarth { get; set; }
        public int DmgWater { get; set; }
        public int DmgAir { get; set; }
        public int ResFire { get; set; }
        public int ResEarth { get; set; }
        public int ResWater { get; set; }
        public int ResAir { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Cooldown { get; set; }
        public DateTime CooldownExpiration { get; set; }
        public string WeaponSlot { get; set; } = string.Empty;
        public string RuneSlot { get; set; } = string.Empty;
        public string ShieldSlot { get; set; } = string.Empty;
        public string HelmetSlot { get; set; } = string.Empty;
        public string BodyArmorSlot { get; set; } = string.Empty;
        public string LegArmorSlot { get; set; } = string.Empty;
        public string BootsSlot { get; set; } = string.Empty;
        public string Ring1Slot { get; set; } = string.Empty;
        public string Ring2Slot { get; set; } = string.Empty;
        public string AmuletSlot { get; set; } = string.Empty;
        public string Artifact1Slot { get; set; } = string.Empty;
        public string Artifact2Slot { get; set; } = string.Empty;
        public string Artifact3Slot { get; set; } = string.Empty;
        public string Utility1Slot { get; set; } = string.Empty;
        public int Utility1SlotQuantity { get; set; }
        public string Utility2Slot { get; set; } = string.Empty;
        public int Utility2SlotQuantity { get; set; }
        public string BagSlot { get; set; } = string.Empty;
        public string Task { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public int TaskProgress { get; set; }
        public int TaskTotal { get; set; }
        public int InventoryMaxItems { get; set; }
        public List<InventoryItem> Inventory { get; set; } = new();
    }

    public class InventoryItem
    {
        public int Slot { get; set; }
        public required string Code { get; set; }
        public int Quantity { get; set; }
    }
}
