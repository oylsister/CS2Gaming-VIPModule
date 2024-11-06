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
using static VipCoreApi.IVipCoreApi;

namespace VipItem
{
    public class Plugin : BasePlugin
    {
        public override string ModuleName => "Vip Item";
        public override string ModuleVersion => "1.1";

        private ICS2GamingAPIShared? _cs2gamingAPI { get; set; }
        private IVipCoreApi? _vipAPI { get; set; }
        private CS2GamingItem? _itemFeature;
        public static PluginCapability<ICS2GamingAPIShared> _capability { get; } = new("cs2gamingAPI");
        public static PluginCapability<IVipCoreApi> _vipCap { get; } = new("vipcore:core");
        public Dictionary<CCSPlayerController, PlayerData> _playerVipClaimed { get; set; } = new();
        public string? filePath { get; set; }
        public readonly ILogger<Plugin> _logger;

        public override void Load(bool hotReload)
        {
            RegisterListener<OnClientDisconnect>(OnClientDisconnect);
            InitializeData();
        }

        public override void Unload(bool hotReload)
        {
            if (_vipAPI != null && _itemFeature != null)
            {
                _vipAPI?.UnRegisterFeature(_itemFeature);
            }
        }

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            _cs2gamingAPI = _capability.Get();
            _vipAPI = _vipCap.Get();

            if (_vipAPI == null)
                return;

            _itemFeature = new CS2GamingItem(this, _vipAPI);
            _vipAPI.RegisterFeature(_itemFeature, FeatureType.Selectable);
        }

        public Plugin(ILogger<Plugin> logger)
        {
            _logger = logger;
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

            if (data == null)
            {
                _playerVipClaimed.Add(client!, new(DateTime.Now.ToString(), DateTime.Now.AddDays(7.0f).ToString(), false));
            }
            else
            {
                var claimed = data.Claimed;
                var timeReset = DateTime.ParseExact(data.TimeReset, "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);

                if (timeReset <= DateTime.Now)
                {
                    claimed = false;
                    data.TimeAcheived = DateTime.Now.ToString();
                    data.TimeReset = DateTime.Now.AddDays(7.0f).ToString();
                    Task.Run(async () => await SaveClientData(steamID, claimed, true));
                }

                _playerVipClaimed.Add(client, new(data.TimeAcheived, data.TimeReset, claimed));
            }

            return HookResult.Continue;
        }

        public void OnClientDisconnect(int playerslot)
        {
            var client = Utilities.GetPlayerFromSlot(playerslot);

            if (!IsValidPlayer(client))
                return;

            var steamID = client!.AuthorizedSteamID!.SteamId64;
            var claimed = _playerVipClaimed[client].Claimed;

            Task.Run(async () => await SaveClientData(steamID, claimed, !claimed));

            _playerVipClaimed.Remove(client!);
        }

        public void AfterSelect(CCSPlayerController client)
        {
            if (!IsValidPlayer(client))
                return;

            if (!_playerVipClaimed.ContainsKey(client!))
                return;

            if (_playerVipClaimed[client].Claimed)
            {
                var now = DateTime.Now;
                var available = DateTime.ParseExact(_playerVipClaimed[client].TimeReset, "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);

                var timeleft = available - now;

                client.PrintToChat($" {Localizer.ForPlayer(client, "Prefix")} {Localizer.ForPlayer(client, "Cooldown", timeleft.Days, timeleft.Hours, timeleft.Minutes)}");
                return;
            }

            var steamid = client.AuthorizedSteamID?.SteamId64;
            Task.Run(async () => await SelectComplete(client!, (ulong)steamid!));
        }

        public async Task SelectComplete(CCSPlayerController client, ulong steamid)
        {
            if (_playerVipClaimed[client].Claimed)
                return;

            _playerVipClaimed[client].Claimed = true;

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

                await SaveClientData(steamid!, true, true);
            }
        }

        private async Task SaveClientData(ulong steamid, bool claimed, bool settime)
        {
            var finishTime = DateTime.Now.ToString();
            var resetTime = DateTime.Now.AddDays(7.0).ToString();
            var steamKey = steamid.ToString();

            var data = new PlayerData(finishTime, resetTime, claimed);

            var jsonObject = ParseFileToJsonObject();

            if (jsonObject == null)
                return;

            if (jsonObject.ContainsKey(steamKey))
            {
                jsonObject[steamKey].Claimed = claimed;

                if (settime)
                {
                    jsonObject[steamKey].TimeAcheived = finishTime;
                    jsonObject[steamKey].TimeReset = resetTime;
                }

                var updated = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                await File.WriteAllTextAsync(filePath!, updated);
            }

            else
            {
                jsonObject.Add(steamKey, data);
                var updated = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                await File.WriteAllTextAsync(filePath!, updated);
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

    public class CS2GamingItem : VipFeatureBase
    {
        public override string Feature => "CS2GamingItem";

        private readonly Plugin _plugin;

        public CS2GamingItem(Plugin plugin, IVipCoreApi api) : base(api)
        {
            _plugin = plugin;
        }

        public override void OnSelectItem(CCSPlayerController player, FeatureState state)
        {
            _plugin.AfterSelect(player);
        }
    }
}
