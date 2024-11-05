using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;
using CS2GamingAPIShared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VipCoreApi;
using static CounterStrikeSharp.API.Core.Listeners;

namespace VipRestriction
{
    public class Plugin : BasePlugin, IPluginConfig<Configs>
    {
        public override string ModuleName => "Vip Restriction";
        public override string ModuleVersion => "1.0";

        private ICS2GamingAPIShared? _cs2gamingAPI { get; set; }
        private IVipCoreApi? _vipAPI { get; set; } 
        public static PluginCapability<ICS2GamingAPIShared> _capability { get; } = new("cs2gamingAPI");
        public static PluginCapability<IVipCoreApi> _vipCap { get; } = new("vipcore:core");
        public Configs Config { get; set; } = new Configs();
        public Dictionary<CCSPlayerController, PlayerData> _playerVipClaimed { get; set; } = new();
        public string? filePath { get; set; }
        public readonly ILogger<Plugin> _logger;

        public override void Load(bool hotReload)
        {
            RegisterListener<OnClientDisconnect>(OnClientDisconnect);
            InitializeData();
        }

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            _cs2gamingAPI = _capability.Get();
            _vipAPI = _vipCap.Get();

            if (_vipAPI == null)
                return;

            _vipAPI.OnPlayerUseFeature += OnPlayerUseFeature;
        }

        public Plugin(ILogger<Plugin> logger)
        {
            _logger = logger;
        }

        public void OnConfigParsed(Configs config)
        {
            Config = config;
        }

        public void InitializeData()
        {
            filePath = Path.Combine(ModuleDirectory, "playerdata.json");

            if (!File.Exists(filePath))
            {
                var empty = "{}";

                File.WriteAllText(filePath, empty);
                _logger.LogInformation("Data file is not found creating a new one.");
            }

            _logger.LogInformation("Found Data file at {0}.", filePath);
        }

        [GameEventHandler]
        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var client = @event.Userid;

            if (!IsValidPlayer(client))
                return HookResult.Continue;

            var steamID = client!.AuthorizedSteamID!.SteamId64;

            var data = GetPlayerData(steamID);

            _playerVipClaimed.Add(client!, new());

            if (data == null)
            {
                //_playerVipClaimed.Add(client!, new(DateTime.Now.ToString(), DateTime.Now.AddDays(7.0f).ToString(), false));
                if (Config.RestrictList == null)
                    return HookResult.Continue;

                foreach (var item in Config.RestrictList)
                {
                    _playerVipClaimed[client].AllFeatureData!.Add(item, new(DateTime.Now.ToString(), DateTime.Now.AddDays(7.0f).ToString(), false));
                }

                SaveClientData(steamID, false, "", true);
            }

            else
            {
                if (data!.AllFeatureData == null)
                    return HookResult.Continue;

                if (Config.RestrictList == null)
                    return HookResult.Continue;

                foreach (var item in Config.RestrictList)
                {
                    bool claimed = false;

                    if (data.AllFeatureData.ContainsKey(item))
                    {
                        claimed = data.AllFeatureData[item].Claimed;
                        var timeReset = DateTime.ParseExact(data.AllFeatureData[item].TimeReset, "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
                        if (timeReset <= DateTime.Now)
                        {
                            claimed = false;
                        }
                    }

                    _playerVipClaimed[client].AllFeatureData!.Add(item, new(DateTime.Now.ToString(), DateTime.Now.AddDays(7.0f).ToString(), claimed));
                }
            }

            return HookResult.Continue;
        }

        public void OnClientDisconnect(int playerslot)
        {
            var client = Utilities.GetPlayerFromSlot(playerslot);

            if (!IsValidPlayer(client))
                return;

            var steamID = client!.AuthorizedSteamID!.SteamId64;
            var theList = _playerVipClaimed[client].AllFeatureData;

            if (theList == null)
            {
                _logger.LogError("{0} data is null, this will not be saved!", client.PlayerName);
                return;
            }

            _logger.LogInformation("Disconnect here.");
            SaveClientData(steamID, false, string.Empty, false, _playerVipClaimed[client!]);

            _playerVipClaimed.Remove(client!);
        }

        public HookResult? OnPlayerUseFeature(CCSPlayerController client, string feature, IVipCoreApi.FeatureState state, IVipCoreApi.FeatureType type)
        {
            //Server.PrintToChatAll("Start here");
            if (Config == null)
                return HookResult.Continue;

            if (Config.RestrictList == null || Config.RestrictList.Count == 0)
                return HookResult.Continue;

            if (!IsValidPlayer(client))
                return HookResult.Continue;

            if (!_playerVipClaimed.ContainsKey(client!))
                return HookResult.Continue;

            //Server.PrintToChatAll("They all passed");

            // Server.PrintToChatAll($"{client.PlayerName} is using {feature} state: {state} type: {type}");

            if (type == IVipCoreApi.FeatureType.Selectable)
            {
                if (!Config.RestrictList.Contains(feature))
                    return HookResult.Continue;

                // Server.PrintToChatAll($"{feature} is included");

                if (_playerVipClaimed[client].AllFeatureData![feature].Claimed)
                {
                    var now = DateTime.Now;
                    var available = DateTime.ParseExact(_playerVipClaimed[client].AllFeatureData![feature].TimeReset, "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);

                    var timeleft = available - now;

                    client.PrintToChat($" {Localizer.ForPlayer(client, "Prefix")} {Localizer.ForPlayer(client, "Cooldown", timeleft.Days, timeleft.Hours, timeleft.Minutes)}");

                    return HookResult.Handled;
                }

                AfterSelect(client!, feature);
            }

            return HookResult.Continue;
        }

        public void AfterSelect(CCSPlayerController client, string feature)
        {
            if (!IsValidPlayer(client))
                return;

            if (!_playerVipClaimed.ContainsKey(client!))
                return;

            if (_playerVipClaimed[client].AllFeatureData![feature].Claimed)
                return;

            var steamid = client.AuthorizedSteamID?.SteamId64;
            Task.Run(async () => await SelectComplete(client!, (ulong)steamid!, feature));
        }

        public async Task SelectComplete(CCSPlayerController client, ulong steamid, string feature)
        {
            if (_playerVipClaimed[client].AllFeatureData![feature].Claimed)
                return;

            _playerVipClaimed[client].AllFeatureData![feature].Claimed = true;

            var response = await _cs2gamingAPI?.RequestSteamID(steamid!)!;
            if (response != null)
            {
                if (response.Status != 200)
                    return;

                Server.NextFrame(() =>
                {
                    var language = client.GetLanguage();
                    string message = "";

                    if (language.TwoLetterISOLanguageName == "ru_RU")
                        message = response.Message_RU!;

                    else
                        message = response.Message!;

                    client.PrintToChat($" {ChatColors.Green}[VIP]{ChatColors.White} {message}");
                });

                SaveClientData(steamid!, true, feature, true);
            }
        }

        public void SaveClientData(ulong steamid, bool claimed, string featureName = "", bool settime = false, PlayerData data = null!)
        {
            // Set time for update shit.
            var finishTime = DateTime.Now.ToString();
            var resetTime = DateTime.Now.AddDays(7.0).ToString();
            var steamKey = steamid.ToString();

            // new data for new player.
            if(data == null)
                data = new PlayerData();

            // get json file
            var jsonObject = ParseFileToJsonObject();

            if (jsonObject == null)
            {
                _logger.LogError("Json is null!");
                return;
            }

            // if contain steamkey then let's go and update
            if (jsonObject.ContainsKey(steamKey))
            {
                _logger.LogInformation("Found {0}", steamKey);

                // if feature name is in param
                if (!string.IsNullOrEmpty(featureName) || !string.IsNullOrWhiteSpace(featureName))
                {
                    // if feature name not found in json.
                    if (!jsonObject[steamKey].AllFeatureData!.ContainsKey(featureName))
                    {
                        // added it.
                        jsonObject[steamKey].AllFeatureData!.Add(featureName, new(finishTime, resetTime, claimed));
                    }

                    // if they're just update.
                    else
                    {
                        // settime will get trigger, but mostly for non claimed stuff.
                        if (settime)
                        {
                            jsonObject[steamKey].AllFeatureData![featureName].TimeAcheived = finishTime;
                            jsonObject[steamKey].AllFeatureData![featureName].TimeReset = resetTime;
                        }

                        // claimed it or not.
                        jsonObject[steamKey].AllFeatureData![featureName].Claimed = claimed;
                    }
                }

                // if they're not set feature name in param

                else
                {
                    // loop for List
                    foreach (var item in Config.RestrictList!)
                    {
                        // if feature name not found in json.
                        if (!jsonObject[steamKey].AllFeatureData!.ContainsKey(item))
                        {
                            // add it as false
                            jsonObject[steamKey].AllFeatureData!.Add(item, new(finishTime, resetTime, false));
                        }

                        // found it
                        else
                        {
                            // this time we check if it not claimed then we still need to updated time, if claimed then let them be.
                            if (!jsonObject[steamKey].AllFeatureData![item].Claimed)
                            {
                                jsonObject[steamKey].AllFeatureData![item].TimeAcheived = finishTime;
                                jsonObject[steamKey].AllFeatureData![item].TimeReset = resetTime;
                            }

                            // claimed it or not this is no need it intend for saving time data.
                            // jsonObject[steamKey].AllFeatureData![featureName].Claimed = claimed;
                        }
                    }
                }

                // save it.
                var updated = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                //_logger.LogInformation("{0}", updated);
                File.WriteAllTextAsync(filePath!, updated);
            }

            // just added a new one.
            else
            {
                _logger.LogInformation("There is no key for {0}", steamKey);
                jsonObject.Add(steamKey, data);
                var updated = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
               // _logger.LogInformation("Added {0}", updated);
                File.WriteAllTextAsync(filePath!, updated);
            }
        }

        private PlayerData? GetPlayerData(ulong steamid)
        {
            var jsonObject = ParseFileToJsonObject();

            if (jsonObject == null)
                return null;

            var steamKey = steamid.ToString();

            if (jsonObject.ContainsKey(steamKey))
                return jsonObject[steamKey];

            return null;
        }

        private Dictionary<string, PlayerData>? ParseFileToJsonObject()
        {
            if (!File.Exists(filePath))
                return null;

            return JsonConvert.DeserializeObject<Dictionary<string, PlayerData>>(File.ReadAllText(filePath));
        }

        public bool IsValidPlayer(CCSPlayerController? client)
        {
            return client != null && client.IsValid && !client.IsBot;
        }
    }
}
