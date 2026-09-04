using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using CounterStrikeSharp.API.Modules.Utils;
using static MapChooser.MapChooser;
using CounterStrikeSharp.API.Core.Translations;
using System.Text;
using CounterStrikeSharp.API.Modules.Memory;
using System.Drawing;

namespace MapChooser
{
    public partial class MapChooser
    {
        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    Logger.LogError($"[MapChooser] Config file not found at path: {_configPath}. Loading default configuration.");
                    _config = new Config(); // 加载默认配置
                    return;
                }

                var json = File.ReadAllText(_configPath);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    // 使用 Newtonsoft.Json 进行反序列化
                    var config = JsonConvert.DeserializeObject<Config>(json);

                    if (config != null)
                    {
                        _config = config;
                        _DisplayHudTimeleftRemaining = _config.DisplayHudTimeleftRemaining;
                        Logger.LogInformation($"[MapChooser] Configuration loaded successfully from {_configPath} _DisplayHudTimeleftRemaining: {_DisplayHudTimeleftRemaining}.");
                    }
                    else
                    {
                        Logger.LogError($"[MapChooser] Failed to deserialize config, loading default configuration.");
                        _config = new Config(); // 加载默认配置
                    }
                }
                else
                {
                    Logger.LogWarning($"[MapChooser] Config file is empty, loading default configuration.");
                    _config = new Config(); // 加载默认配置
                }
            }
            catch (JsonException jsonEx)
            {
                Logger.LogError($"[MapChooser] JSON format error in config file: {jsonEx.Message}. Loading default configuration.");
                _config = new Config(); // 加载默认配置
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MapChooser] Error loading config: {ex.Message}. Loading default configuration.");
                _config = new Config(); // 加载默认配置
            }
        }

        private void TrimMapHistory()
        {
            while (_mapHistory.Count > _config.ExcludeMaps)
            {
                _mapHistory.RemoveAt(0);
            }
        }

        public static string GetTimeLeftFormat()
        {
            int iTimeleft = GetTimeLeft_v2();
            if (iTimeleft <= 0.0F)
                return "未知";

            int iMinutesLeft, iSecondsLeft;
            iMinutesLeft = Math.DivRem(iTimeleft, 60, out iSecondsLeft);

  

            return ($"{iMinutesLeft}:{iSecondsLeft}");
        }

        private int GetRandomIndex(int max)
        {
            byte[] randomNumber = new byte[4];
            int value;

            do
            {
                _rng.GetBytes(randomNumber);
                value = BitConverter.ToInt32(randomNumber, 0) & int.MaxValue;
            } while (value >= int.MaxValue - (int.MaxValue % max));  // 拒绝采样，保证均匀性

            return value % max;
        }


        public static string FindWorkshopIdByName(string mapName)
        {
            // Using LINQ to search for the first map that matches the given name
            var mapInfo = MapList.FirstOrDefault(map =>
                map.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(map.Name, mapName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(map.UpdatedName, mapName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(map.FileName, mapName, StringComparison.OrdinalIgnoreCase));

            if (mapInfo == null) return string.Empty;


            // If a map is found, return its WorkshopId, otherwise return null or an appropriate default value
            return mapInfo.WorkshopId;
        }
        public static string FindMapNameByWSId(string wsId)
        {
            // Using LINQ to search for the first map that matches the given name
            var mapInfo = MapList.FirstOrDefault(map => map.WorkshopId.Equals(wsId, StringComparison.OrdinalIgnoreCase));

            if (mapInfo == null) return string.Empty;


            // If a map is found, return its WorkshopId, otherwise return null or an appropriate default value
            return mapInfo.FileName;
        }


        public void ChangeRandomMap()
        {
            PickRandomMap();
            ExecudeChangeMap($"{_nextMap}");
        }

        private bool CheckRTV(int playerCount)
        {
            if (playerCount < 1 || _rtvCount.Count < 1) return false;

            var required = (int)Math.Ceiling(playerCount * _config.RtvPercent);
            if (_rtvCount.Count < required) return false;

            _wasRtv = true;
            StartMapVote();
            PlayerUtils.PrintToChatAll($"mapchooser.rtv_vote_starting");


            return true;
        }


        private void PickRandomMap()
        {
            // Check if there are no maps loaded or all maps are disabled
            if (MapList.Count == 0 || !MapList.Any(m => m.Enabled))
            {
                LoadMaps(); // You might need to ensure this method reloads or rechecks the maps correctly
            }

            // Filter only enabled maps
            var enabledMaps = MapList.Where(m => m.Enabled).ToList();
            if (enabledMaps.Count > 0)
            {
                int index = GetRandomIndex(enabledMaps.Count); // Generate a random index for the enabled maps
                MapInfo randomMapInfo = enabledMaps[index]; // Pick a random map from the filtered list

                string FinalMapName = !string.IsNullOrEmpty(randomMapInfo.UpdatedName) && randomMapInfo.UpdatedName.Length >= 2 ? randomMapInfo.UpdatedName : randomMapInfo.FileName;
                _nextMap = FinalMapName;  // Assuming you want to use WorkshopId to change levels

                Server.ExecuteCommand($"nextlevel {_nextMap}");
                Logger.LogInformation($"[Mapchooser] 设置随机地图-> {_nextMap} ");
            }
            else
            {
                Logger.LogWarning("[Mapchooser] No enabled maps available to pick from.");
            }
        }

        public static int GetTimeLeft_v2(bool bReturnCache = false)
        {
            // 配置中 使用 mp_timelimit 的总时间
            if (Instance._config.UseGameTimeLimit)
            {
                if (bReturnCache) return g_iCacheTimeLeft;

                if (_timeLimitConVar != null && _gameRules != null)
                {
                    float TimeLimitValue = _timeLimitConVar.GetPrimitiveValue<float>();

                    if (TimeLimitValue <= 0.0F)
                    {
                        return -1;
                    }

                    int iTimeleft = (int)((_gameRules.GameStartTime + TimeLimitValue * 60.0f) - Server.CurrentTime);
                    if (iTimeleft < 0)
                    {
                        //last round
                        return 0;
                    }

                    g_iCacheTimeLeft = iTimeleft;

                    return iTimeleft;
                }
            }
            // 使用自定义变量时间方案
            else
            {
                // Total time for the map round in minutes // 地图总时间分钟数
                float TotalTimeMinutes = g_fMapTimelimit;

                // Calculate the total time in seconds
                int TotalTimeSeconds = (int)(TotalTimeMinutes * 60);

                // Calculate the time left in seconds
                int iTimeleft = TotalTimeSeconds - (int)(DateTime.UtcNow - g_startTimeUtc).TotalSeconds;

                if (iTimeleft < 0)
                {
                    //last round
                    // If the time left is less than 0, it's the last round
                    return 0;
                }

                g_iCacheTimeLeft = iTimeleft;

                return iTimeleft;

                /*
                //Instance.MapStartTime = 地图开始的时间 datetime.now
                // 地图默认时间
                float TotalTime = g_fMapTimelimit;
                int iTimeleft = (int)((_gameRules.GameStartTime + g_fMapTimelimit * 60.0f) - Server.CurrentTime);
                if (iTimeleft < 0)
                {
                    //last round
                    return 0;
                }

                g_iCacheTimeLeft = iTimeleft;
                return iTimeleft;
                */
            }

            return -1;
        }

        public void UpdateMapTimeVar()
        {
            if (Instance._config.UseGameTimeLimit)
            {
                if (_timeLimitConVar != null)
                    MapTimes = _timeLimitConVar.GetPrimitiveValue<float>();
            }
            else MapTimes = g_fMapTimelimit;
        }
        
        public void ExtendMapTime()
        {
            if (Instance._config.UseGameTimeLimit)
            {
                if (_timeLimitConVar != null)
                    _timeLimitConVar.SetValue(_timeLimitConVar.GetPrimitiveValue<float>() + _config.ExtendTimeStep);
            }
            else g_fMapTimelimit += _config.ExtendTimeStep;

            OnMapExtend(_config.ExtendTimeStep);
        }

        public static bool IsValidPlayer(CCSPlayerController player)
        {
            if (!player.IsValid
                || player.IsBot
                || player.IsHLTV
                || player.UserId == null
                || player.Connected != PlayerConnectedState.Connected
                || player.PlayerPawn == null
                || !player.PlayerPawn.IsValid
                || player.PlayerPawn.Value == null
                || !player.PlayerPawn.Value.IsValid)
            {
                return false;
            }

            return true;
        }
        public static void FindGameRules()
        {
            try
            {
                _gameRules = GetGameRules();
            }
            catch (Exception)
            {

                Instance?.Logger.LogError("Couldn't find `CCSGameRules`");
            }
        }


        private static void SaveMapHistoryToFile(string filePath)
        {
            try
            {
                string json = JsonConvert.SerializeObject(_mapHistory, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("保存文件时发生错误: " + ex.Message);
            }
        }
        private static List<string> LoadMapHistoryFromFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var result = JsonConvert.DeserializeObject<List<string>>(json);
                    return result ?? new List<string>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("读取文件时发生错误: " + ex.Message);
            }

            return new List<string>();
        }
        private void SetupTimeLimitCountDown()
        {
            CheckMapVoteStart();
            AddTimer(60.0f, () =>
            {
                _canRtv = true;
                CheckMapVoteStart();
                Logger.LogInformation($"[Mapchooser] RTV Is Active");
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }
        private void CheckMapVoteStart()
        {
            if (MapTimes > 5.0)
                return;

            MapStartTime = DateTime.Now;

            if (Instance._config.UseGameTimeLimit) 
            { 
                if (_timeLimitConVar == null)
                {
                    _timeLimitConVar = ConVar.Find("mp_timelimit");
                }

                // 先检查 _timeLimitConVar 是否为 null
                if (_timeLimitConVar != null)
                {
                    MapTimes = _timeLimitConVar.GetPrimitiveValue<float>();
                }
                else
                {
                    MapTimes = 23.0f;  // 假设默认地图时间为 23 分钟
                }

                if (MapTimes > 5.0 && !_IsSet_mapVoteTimer)
                {
                    _IsSet_mapVoteTimer = true;

                    NextVoteDelay = (MapTimes * 60f) - (_config.VoteStartTime * 60f);

                    ClearVoteTimer();
                }
            }
            else
            {
                MapTimes = g_fMapTimelimit;

                if (MapTimes > 5.0 && !_IsSet_mapVoteTimer)
                {
                    _IsSet_mapVoteTimer = true;

                    NextVoteDelay = (MapTimes * 60f) - (_config.VoteStartTime * 60f);

                    ClearVoteTimer();
                }
            }

            Logger.LogInformation($"NextMapVote Delay: {MapTimes} VoteDelay: {NextVoteDelay} UseGameTimeLimit: {(Instance._config.UseGameTimeLimit ? "true" : "false")}");
        }


        private void CrashMapRecover()
        {
            if (!_config.CrashMapRecover)
                return;

            if (!File.Exists(_CrashMapRecover))
                return;

            string sMapName = File.ReadAllText(_CrashMapRecover).Trim();
            _CrashMapRecover_Timer = null;
            if (string.IsNullOrEmpty(sMapName))
            {
                return;
            }

            Logger.LogInformation($"CrashMapRecover {sMapName}");
            ExecudeChangeMap(sMapName);
        }

        private void SaveCrashMapRecover(string mapName)
        {
            if (!_config.CrashMapRecover || string.IsNullOrWhiteSpace(mapName))
                return;

            File.WriteAllText(_CrashMapRecover, mapName);
        }

        private void StartMapVote_Action()
        {
            _voteActive = true;
            _totalVotes = 0;
            _votes.Clear();
            _rtvCount.Clear();
            ClearVoteTimer();

            var menu = new CenterHtmlMenu(Localizer["mapchooser.vote_header"], plugin: this);
            menu.ExitButton = false;

            var now = DateTime.Now.TimeOfDay;
            g_CurrentPlayers = GetOnlinePlayerCount();
            // 获取已启用的地图列表，并按顺序反转地图名称
            var voteMaps = GetFilteredVoteMaps();

            // 准备要投票的地图列表
            var nextMap = g_Nominates.Values.Select(nomination => nomination.MapName).ToList();

            while (nextMap.Count < _config.IncludeMaps && voteMaps.Any())
            {
                int index = GetRandomIndex(voteMaps.Count);
                nextMap.Add(voteMaps[index]);
                voteMaps.RemoveAt(index);
            }


            var number = 1;
            if (_wasRtv)
            {
                number++;
                menu.AddMenuOption(Localizer["mapchooser.option_dont_change"], (controller, option) =>
                {
                    if (!_voteActive) return;

                    // 检查玩家是否已经投过票

                    if (_playerVotes.TryGetValue(controller.SteamID, out var previousVote))
                    {
                        if (previousVote == option.Text) return; // 如果投票相同，不提示

                        // 如果已经投过票，减少原来的票数
                        _votes[previousVote]--;
                    }

                    if (_playerVotes.TryGetValue(controller.SteamID, out var vote))
                        _votes[vote]--;
                    if (_votes.TryGetValue(option.Text, out var count))
                        _votes[option.Text] = count + 1;
                    else
                        _votes[option.Text] = 1;

                    if (!_playerVotes.ContainsKey(controller.SteamID))
                        _totalVotes++;
                    _playerVotes[controller.SteamID] = option.Text;
                    MenuManager.CloseActiveMenu(controller);

                    Server.PrintToChatAll(
                        $"{Localizer["mapchooser.prefix"]} {Localizer["mapchooser.voted_for", controller.PlayerName, option.Text]}");
                });
            }


            // Add voting options for each map
            foreach (var map in nextMap)
            {
                menu.AddMenuOption(map, (controller, option) =>
                {
                    if (!_voteActive) return; // Ensure voting is active

                    // 检查玩家是否已经投过票
                    if (_playerVotes.ContainsKey(controller.SteamID))
                    {
                        var previousVote = _playerVotes[controller.SteamID];
                        if (previousVote == option.Text) return; // 如果投票相同，不提示

                        // 如果已经投过票，减少原来的票数
                        _votes[previousVote]--;

                    }

                    _votes.TryGetValue(option.Text, out var currentCount);
                    _votes[option.Text] = currentCount + 1;
                    _playerVotes[controller.SteamID] = option.Text;
                    _totalVotes++;
                    MenuManager.CloseActiveMenu(controller);

                    Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} {Localizer["mapchooser.voted_for", controller.PlayerName, option.Text]} {map} 当前票数: {_votes[option.Text]}");
                });
            }

            // Optionally add an option to extend the current map session
            if (!_wasRtv && _config.AllowExtend && _extends < _config.ExtendLimit)
            {
                menu.AddMenuOption("Extend", (controller, option) =>
                {
                    if (!_voteActive) return; // Ensure voting is active

                    // 检查玩家是否已经投过票
                    if (_playerVotes.TryGetValue(controller.SteamID, out var previousVote))
                    {
                        // 如果投票相同，不提示
                        if (previousVote == option.Text) return;

                        // 如果已经投过票，减少原来的票数
                        _votes[previousVote]--;
                    }

                    _votes.TryGetValue(option.Text, out var currentCount);
                    _votes[option.Text] = currentCount + 1;
                    _playerVotes[controller.SteamID] = option.Text;
                    _totalVotes++;
                    MenuManager.CloseActiveMenu(controller);
                    Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} {Localizer["mapchooser.voted_for_extend", controller.PlayerName]}");
                });
            }

            // Open the voting menu for all players
            foreach (var player in Utilities.GetPlayers())
            {
                MenuManager.OpenCenterHtmlMenu(this, player, menu);
                player.ExecuteClientCommand("play sounds/xnet/tips/noa_enabled.vsnd");
            }

            // Set a timer to end the voting process
            AddTimer(Math.Min(_config.VoteDuration, 60f), OnVoteFinished, TimerFlags.STOP_ON_MAPCHANGE);
        }

        private int GetOnlinePlayerCount(bool countSpec = false)
        {
            var players = Utilities.GetPlayers().Where((player) => player is { IsValid: true, Connected: PlayerConnectedState.Connected, IsBot: false, IsHLTV: false });
            if (!countSpec) players = players.Where((player) => player.TeamNum > 1);
            return players.Count();
        }
        private void StartCheckMapUpdate(bool IsForce = false)
        {
            DateTime currentRun = DateTime.Now;

            if (!IsForce)
            {
                LastRunMapDownload = Server.GameDirectory + "/csgo/addons/counterstrikesharp/configs/plugins/MapChooser/last_run_download.txt";
                DateTime lastRun;
                // 检查文件是否存在
                if (File.Exists(LastRunMapDownload))
                {
                    // 读取文件内容并解析为日期时间
                    string lastRunStr = File.ReadAllText(LastRunMapDownload);
                    lastRun = DateTime.Parse(lastRunStr);
                }
                else
                {
                    // 如果文件不存在，初始化上次运行时间为较早时间
                    lastRun = DateTime.MinValue;
                }

                // 比较当前时间和上次运行时间
                if ((currentRun - lastRun).TotalDays < 1)
                {
                    Logger.LogInformation("地图下载不运行 因为距离上次小于1天.");
                    return;
                }
            }


            AddTimer(1.0F, () =>
            {
                // 检查上次执行时间是否超过4小时，或者是初次启动
                if (_firstExecution || (DateTime.Now - _lastExecutionTime >= TimeSpan.FromHours(4)))
                {
                    _lastExecutionTime = DateTime.Now; // 更新上次执行时间
                    _firstExecution = false; // 标记为非初次启动
                    if (!_config.AutoDownload) return;
                    if (_config.ChangeMapUse_host_workshop_map) return;

                    DownloadMapByLists();
                }

                else
                {
                    Logger.LogInformation("StartCheckMapUpdate method was recently executed. Skipping execution.");
                }
            });



        }

        private void DownloadMapByLists()
        {
            DateTime currentRun = DateTime.Now;

            // 1. 安全写入时间戳
            try
            {
                var dir = Path.GetDirectoryName(LastRunMapDownload);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(LastRunMapDownload, currentRun.ToString());
            }
            catch (IOException ex)
            {
                Logger.LogWarning(ex, "Failed to write last run timestamp to {Path}", LastRunMapDownload);
            }

            // 2. 筛选出所有 Enabled 的 WorkshopId（已自动排除 null/空值）
            var workshopIds = MapList
                .Where(mapInfo => mapInfo != null
                                  && mapInfo.Enabled
                                  && !string.IsNullOrEmpty(mapInfo.WorkshopId)
                                  && mapInfo.WorkshopId != "0")
                .Select(mapInfo => mapInfo.WorkshopId)
                .Reverse()          // 保持原来的倒序逻辑
                .ToList();

            if (workshopIds.Count == 0)
            {
                Logger.LogInformation("No file IDs are stored.");
                return;
            }

            if (PublishedFileIds_IsDownload)
            {
                Logger.LogInformation("Download already in progress, skipping.");
                return;
            }

            // 3. 设置标志位（提前设置，防止并发重入）
            PublishedFileIds_IsDownload = true;

            // 4. 按顺序下载，每个间隔 15 秒
            int totalCount = workshopIds.Count;
            for (int i = 0; i < totalCount; i++)
            {
                var fileId = workshopIds[i];    // 循环体内声明，闭包安全
                var index = i;                  // 捕获当前索引用于日志
                double delay = 30.0d * i;       // 使用 double 避免 float 精度丢失

                AddTimer((float)delay, () =>
                {
                    try
                    {

                        Logger.LogInformation(
                            "Downloading addon {FileId} ({Index}/{Total})",
                            fileId, index + 1, totalCount);

                        Server.ExecuteCommand($"mm_download_addon {fileId}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex,
                            "Failed to download addon {FileId} ({Index}/{Total})",
                            fileId, index + 1, totalCount);
                    }
                    finally
                    {
                        // 最后一个下载完成后重置标志位，允许后续再次触发
                        if (index == totalCount - 1)
                        {
                            PublishedFileIds_IsDownload = false;
                            Logger.LogInformation("All {Total} addon downloads completed.", totalCount);
                        }
                    }
                });
            }
        }

        private void LoadMaps()
        {
            //Logger.LogInformation($"[MapChooser]Loading {_mapsPath}");
            if (File.Exists(_mapsData))
            {
                MapList.Clear();
                ParseKVFile(_mapsData);
            }
            else
            {
                Logger.LogInformation($"[MapChooser] can't find file {_mapsData}");
            }

            Logger.LogInformation($"[MapChooser] Total maps loaded: {MapList.Count}");
        }

        private bool IsInRestrictedTime(List<TimePeriod> restrictedTimes)
        {
            var now = DateTime.Now.TimeOfDay;
            return restrictedTimes.Any(period => now >= period.Start && now <= period.End);
        }

        private void ActiveRTV()
        {
            if (!_ActiveUSERTV)
            {
                SetupTimeLimitCountDown();
                _ActiveUSERTV = true;
                Logger.LogInformation($"[Mapchooser] 60sec Active RTV");
            }
        }

        private void PlayerRTV(CCSPlayerController player)
        {
            if (player == null)
                return;

            if (!IsValidPlayer(player))
                return;

            if (!_config.AllowRtv)
                return;

            DateTime currentTimeUtc = DateTime.UtcNow;
            if (g_CanRtvUtcTime >= currentTimeUtc)
            {
                double secondsLeft = (g_CanRtvUtcTime - currentTimeUtc).TotalSeconds;

                player.PrintToChatMessage($"mapchooser.RTVUnavailableMessage", secondsLeft);
                
                return;
            }


            if (_gServerTime.TryGetValue(player.Slot, out var cooldownTime) && DateTime.UtcNow < cooldownTime)
            {
                var remainingTime = (cooldownTime - DateTime.UtcNow).TotalSeconds;
                player.PrintToChatMessage($"mapchooser.CommandCooldownMessage", remainingTime);

                return;
            }

            if (!_canRtv)
            {
                player.PrintToChatMessage($"mapchooser.rtv_not_available");

                return;
            }

            g_CurrentPlayers = GetOnlinePlayerCount();
            var required = (int)Math.Ceiling(g_CurrentPlayers * _config.RtvPercent);


            if (_rtvCount.Contains(player.SteamID) || _voteActive)
            {
                player.PrintToChatMessage($"mapchooser.RTVVoteMessage", _rtvCount.Count, required);
                return;
            }

            _rtvCount.Add(player.SteamID);
            _gServerTime[player.Slot] = DateTime.UtcNow.AddSeconds(RtvUnRTV_WaitTimeSeconds);

            player.PrintToChatMessage($"mapchooser.RTVPercentMessage", _config.RtvPercent);

            //TODO: Add message saying player has voted to rtv
            PlayerUtils.PrintToChatAll($"mapchooser.rtv", player.PlayerName, _rtvCount.Count, required);

            CheckRTV(g_CurrentPlayers);
        }

        private void StartMapVote()
        {
            if (_mapWarningTimer != null)
            {
                Server.PrintToChatAll($"{ChatColors.DarkRed} 地图投票触发暂时无法发起(已在倒计时)");
                return;
            }

            if (_voteActive)
            {
                Server.PrintToChatAll($"{ChatColors.DarkRed} 地图投票触发暂时无法发起(投票已激活)");
                return;
            }


            // 如果地图投票已完成.选出了地图.
            if (_IsMapVoteFinished)
            {
                _totalVotes = 0;
                _votes.Clear();
                _rtvCount.Clear();
                ClearVoteTimer();
                PlayerUtils.PrintToChatAll($"mapchooser.VoteAlreadyCastMessage", _nextMap);

                if (_nextMap != "")
                {
                    ExecudeChangeMap($"{_nextMap}");
                }

                return;
            }



            _voteActive = true;
            _WarningCounter = 0;
            ClearWarningTimer();
            _mapWarningTimer = AddTimer(1.0F, StartMapVote_WarningEnd, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        }

        private void ExecudeChangeMap(string MapName)
        {
            if ((DateTime.UtcNow - _lastMapChangeTime).TotalSeconds < 5)
            {
                Logger.LogInformation("ExecudeChangeMap 变更地图操作过于频繁 < 5 秒");
                return;
            }

            if (string.IsNullOrEmpty(MapName))
            {
                return;
            }

            var mapInfo = MapList.FirstOrDefault(map =>
                string.Equals(map.FileName, MapName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(map.Name, MapName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(map.UpdatedName, MapName, StringComparison.OrdinalIgnoreCase));

            string sMapid = string.Empty;
            if (mapInfo == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(mapInfo.WorkshopId) && mapInfo.WorkshopId != "0")
            {
                sMapid = mapInfo.WorkshopId;

                // 查找 ServerMapName
                string? ServerMapName = MapPing_FindMapInfo(mapInfo.WorkshopId, "WorkshopId", false);

                // 更新 FinalMapName 的逻辑
                string FinalMapName = !string.IsNullOrEmpty(ServerMapName) && ServerMapName.Length >= 2
                    ? ServerMapName
                    : (!string.IsNullOrEmpty(mapInfo.UpdatedName) && mapInfo.UpdatedName.Length >= 2
                        ? mapInfo.UpdatedName
                        : mapInfo.FileName);


                PlayerUtils.PrintToChatAll($"mapchooser.ExecudeChangeMap", FinalMapName, mapInfo.WorkshopId, ServerMapName ?? "UN");


                // 防止计时器重复

                if (_mapChangeByIdExecuteTimer == null)
                {
                    _mapChangeByIdExecuteTimer = AddTimer(6.5f, () =>
                    {
                        if (!Instance._config.ChangeMapUse_host_workshop_map)
                        {
                            Server.ExecuteCommand($"ds_workshop_changelevel {FinalMapName}");
                        }
                        else Server.ExecuteCommand($"host_workshop_map {mapInfo.WorkshopId}");

                        _mapChangeByIdExecuteTimer = null;
                    });
                }


                if (_mapChangeByIdExecuteTimerByID == null) 
                { 
                    _mapChangeByIdExecuteTimerByID = AddTimer(23.5f, () =>
                    {
                        // Assume the first change failed, try with the workshop ID
                        AddTimer(2.5f, () =>
                        {
                            Server.ExecuteCommand($"host_workshop_map {mapInfo.WorkshopId}");
                        });

                        //Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} 服务器使用 ds_workshop_changelevel {FinalMapName} 更换地图 疑似失败 正在自动重试中... host_workshop_map {mapInfo.WorkshopId}");
                        //Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} {string.Format(Localizer["mapchooser.MapChangeRetryMessage"], FinalMapName, mapInfo.WorkshopId)}");
                        PlayerUtils.PrintToChatAll($"mapchooser.MapChangeRetryMessage", FinalMapName, mapInfo.WorkshopId);

                        Logger.LogWarning($"[Map Change] Command ds_workshop_changelevel {FinalMapName} likely failed, retrying with WsCommand:host_workshop_map {mapInfo.WorkshopId}");

                    }, TimerFlags.REPEAT);
                }

            }
            else
            {
                Server.ExecuteCommand($"changelevel {mapInfo.FileName}");
            }

            _lastMapChangeTime = DateTime.UtcNow;

        }

        private void StartMapVote_WarningEnd()
        {
            _voteActive = true;
            if (_WarningCounter <= 8)
            {
                _WarningCounter++;

                Server.PrintToChatAll($"Map voting is about to begin(地图投票即将开始) {ChatColors.Green}{10 - _WarningCounter}{ChatColors.Yellow}s{ChatColors.Default}");

                foreach (var player in Utilities.GetPlayers())
                {
                    player.ExecuteClientCommand("play sounds/xnet/tips/seatbelt_front_chime.vsnd");
                    player.PrintToCenter($"{GetAllNominationsString()}");
                }

            }
            else
            {
                ClearWarningTimer();
                StartMapVote_Action();
            }
        }





        private static CCSGameRules GetGameRules()
        {
            return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
        }

        private void OnVoteFinished()
        {
            _voteActive = false;
            if (_totalVotes == 0)
            {
                ChooseRandomNextMap();

                ClearEnforceTimeLimit();
                if (_config.EnforceTimeLimit && _EnforceTimeLimit == null)
                {
                    ClearVoteTimer();
                    float fTimer = (_config.VoteStartTime * 60f) - Math.Min(_config.VoteDuration, 60f);
                    
                    PlayerUtils.PrintToChatAll($"mapchooser.MapChangeTimerMessage", fTimer, _nextMap);

                    _EnforceTimeLimit = AddTimer(fTimer, () =>
                    {
                        ExecudeChangeMap($"{_nextMap}");
                    }, TimerFlags.STOP_ON_MAPCHANGE);

                }
                return;
            }

            var winner = "";
            var winnerVotes = 0;
            foreach (var (map, count) in _votes)
            {
                if (winner == "")
                {
                    winner = map;
                    winnerVotes = count;
                }
                else if (count > winnerVotes)
                {
                    winner = map;
                    winnerVotes = count;
                }
            }
            string mapid = FindWorkshopIdByName(winner);


            PlayerUtils.PrintToChatAll($"mapchooser.map_won", winner);
            Logger.LogInformation($"{Localizer["mapchooser.prefix"]} {Localizer["mapchooser.map_won", winner]} ID->{mapid}");

            foreach (var player in Utilities.GetPlayers())
            {
                if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
                    continue;

                MenuManager.CloseActiveMenu(player);
                player.ExecuteClientCommand("play sounds/xnet/tips/noa_disabled.vsnd");
            }


            if (_wasRtv)
            {
                _wasRtv = false;

                if (!winner.Equals(Localizer["mapchooser.option_dont_change"]))
                {
                    SaveCrashMapRecover(winner);
                    _mapHistory.Add(winner);
                    ExecudeChangeMap(winner);

                    PlayerUtils.PrintToChatAll($"mapchooser.RTVVoteResultMessage", winner,mapid);
                }
                else
                {
                    _canRtv = true;
                    
                    PlayerUtils.PrintToChatAll($"mapchooser.RTVVoteNoChangeMessage");
                    AddTimer(_config.RtvDelay * 60f, () =>
                    {
                        PlayerUtils.PrintToChatAll($"mapchooser.rtv_enabled");
                        _canRtv = true;
                    }, TimerFlags.STOP_ON_MAPCHANGE);
                }

            }
            else
            {
                if (winner.Equals("Extend"))
                {
                    if (_timeLimitConVar != null)
                    {
                        _nextMap = "";
                        ExtendMapTime();
                        _extends++;
                        Logger.LogInformation($"Setting next map vote time to {(_config.ExtendTimeStep * 60f) + (60 - Math.Min(_config.VoteDuration, 60f))}");
                        ClearVoteTimer();

                        NextVoteDelay = (_config.ExtendTimeStep * 60f) + (60 - Math.Min(_config.VoteDuration, 60f)) - 10.0F;


                        //_mapVoteTimer = AddTimer(NextVoteDelay, StartMapVote,TimerFlags.STOP_ON_MAPCHANGE);

                        _canRtv = false;
                        AddTimer(60.0F, () =>
                        {
                            PlayerUtils.PrintToChatAll($"mapchooser.rtv_enabled");
                            _canRtv = true;
                        }, TimerFlags.STOP_ON_MAPCHANGE);
                        
                        PlayerUtils.PrintToChatAll($"mapchooser.NextVoteMessage", NextVoteDelay, _extends, _config.ExtendLimit, _config.ExtendLimit);

                        Logger.LogInformation($"{Localizer["mapchooser.prefix"]} 下一次地图投票将于 {NextVoteDelay} 秒后 已延长次数/最大 {_extends} / {_config.ExtendLimit}");

                        UpdateMapTimeVar();
                        CheckMapStuck();

                    }
                }
                else
                {

                    SetNextMap(winner);

                    Logger.LogInformation($"[Mapchooser][OnMapVoteWin] -> {_nextMap} ID:{mapid} Write file to record!");
                    ClearEnforceTimeLimit();
                    if (_config.EnforceTimeLimit && _EnforceTimeLimit == null)
                    {
                        ClearVoteTimer();
                        float fTimer = (_config.VoteStartTime * 60f) - Math.Min(_config.VoteDuration, 60f);

                        PlayerUtils.PrintToChatAll($"mapchooser.MapChangeTimerMessage", fTimer, winner);


                        _EnforceTimeLimit = AddTimer(fTimer, () =>
                        {
                            ExecudeChangeMap($"{winner}");
                        }, TimerFlags.STOP_ON_MAPCHANGE);

                    }

                    SaveCrashMapRecover(winner);

                }
            }

            _playerVotes.Clear();
            _votes.Clear();
            g_Nominates.Clear();
            _totalVotes = 0;
        }

        private void SetNextMap(string NextMap)
        {
            _nextMap = NextMap;
            _mapHistory.Add(NextMap);
            Server.ExecuteCommand($"nextlevel {_nextMap}");
            _IsMapVoteFinished = true;

            _playerVotes.Clear();
            _votes.Clear();
            g_Nominates.Clear();
            _totalVotes = 0;
        }

        private void ChooseRandomNextMap()
        {
            // Filter maps that can be chosen based on the configuration and nominations
            // 过滤可以根据配置和提名被选择的地图
            var eligibleMaps = MapList.Where(mapInfo =>
                mapInfo.Enabled &&
                (!_config.IncludeCurrent || mapInfo.FileName != GetCurrentMapName(false)) &&
                (_config.ExcludeMaps <= 0 || !_mapHistory.Contains(mapInfo.WorkshopId) &&
                !g_Nominates.Values.Any(nomination =>
                    nomination.MapWorkShopId.Equals(mapInfo.WorkshopId, StringComparison.OrdinalIgnoreCase) ||
                    nomination.MapName.Equals(mapInfo.FileName, StringComparison.OrdinalIgnoreCase)
                ))
            ).ToList();

            if (eligibleMaps.Count == 0)
            {
                Logger.LogWarning("[Mapchooser] No eligible maps available to pick from.");
                return; // Exit if no maps are available
            }

            Random random = new Random();
            // Select a random map from eligible maps
            MapInfo selectedMap = eligibleMaps[random.Next(eligibleMaps.Count)];
            string FinalMapName = !string.IsNullOrEmpty(selectedMap.UpdatedName) && selectedMap.UpdatedName.Length >= 2 ? selectedMap.UpdatedName : selectedMap.FileName;
            _nextMap = FinalMapName;  // Assuming you want to use WorkshopId to change levels


            PlayerUtils.PrintToChatAll($"mapchooser.map_won", _nextMap);

            if (_wasRtv)
            {
                _wasRtv = false;
                ExecudeChangeMap(selectedMap.FileName);
                return;
            }



            Server.ExecuteCommand($"nextlevel \"{_nextMap}\"");
            PlayerUtils.PrintToChatAll($"mapchooser.NoVotesMessage", _nextMap);


            _IsMapVoteFinished = true;
            //File.WriteAllText(_CrashMapRecover, _nextMap);

            foreach (var player in Utilities.GetPlayers())
            {
                if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
                    continue;

                MenuManager.CloseActiveMenu(player);
                player.ExecuteClientCommand("play sounds/xnet/tips/tacc_enabled.vsnd");
            }

            Logger.LogInformation($"[Mapchooser][ChooseRandomNextMap] -> {_nextMap}");
        }
        private void CheckMapStuck()
        {
            if (MapTimes > 5.0)
            {
                int iTimeleft = GetTimeLeft_v2();
                int iMinutesLeft, iSecondsLeft;
                iMinutesLeft = Math.DivRem(iTimeleft, 60, out iSecondsLeft);
                DateTime currentTime = DateTime.Now;

                //无人后防止卡结算界面
                if (GetOnlinePlayerCount(true) <= 0 && iMinutesLeft <= 5)
                {
                    Server.ExecuteCommand("mp_restartgame 1");
                }

                string Message = $"[Mapchooser] 地图参数时长:{MapTimes}分 延:{_extends}/{_config.ExtendLimit} 剩:{ChatColors.Orange}{iMinutesLeft}.{iSecondsLeft}{ChatColors.Default}分 将进行地图投票 (开始于 {ChatColors.Red}{MapStartTime:HH:mm}) {ChatColors.Default}服务器时间[CST]:{ChatColors.Purple}{currentTime:HH:mm} {ChatColors.Green} {ChatColors.Green} 输入!rtv申请换图 {ChatColors.Gold}!yd <map name> {ChatColors.LightYellow}地图关键词可以预定 {ChatColors.Red}VIP用户可以输入 {ChatColors.Green}!pve{ChatColors.LightYellow} 延长 ";
                string Message_En = $"[Mapchooser] Map duration: {MapTimes} Exts:{_extends}/{_config.ExtendLimit} minutes Remaining: {ChatColors.Orange}{iMinutesLeft}.{iSecondsLeft}{ChatColors.Default} minutes (Started at {ChatColors.Red}{MapStartTime:HH:mm}) {ChatColors.Default}Server time[CST]: {ChatColors.Purple}{currentTime:HH:mm} {ChatColors.Green} {ChatColors.Green} A map vote will take place or type !rtv to start it early {ChatColors.Gold}!yd <map name>{ChatColors.LightYellow}You can nominate a map.{ChatColors.Red}VIP users can Type {ChatColors.Green}!pve{ChatColors.LightYellow} to extend";

                foreach (var player in Utilities.GetPlayers())
                {
                    if (!IsValidPlayer(player))
                        continue;

                    if (player.GetLanguage().TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
                        player.PrintToChat(Message);
                    else
                        player.PrintToChat(Message_En);
                }

            }
        }

        public void ShoudMapVote()
        {
            if (_voteActive || _IsMapVoteFinished)
            {
                return;
            }

            int iTimeLeft = -1;
            iTimeLeft = GetTimeLeft_v2();

            if (_DisplayHudTimeleftRemaining > 0 && _DisplayHudTimeleftRemaining >= iTimeLeft)
            {
                Hud_MapHud_timeLeft_Display(iTimeLeft);
            }

            if (iTimeLeft > 60 && iTimeLeft <= 180)
            {
                StartMapVote();
            }
        }

        private void Timer_ChangeMap()
        {
            if (_nextMap == "" || _nextMap.Length <= 3) PickRandomMap();

            PlayerUtils.PrintToChatAll($"mapchooser.OnMapChangeMessage", _nextMap);

            _nextMap = _nextMap.Replace("ws:", "");

            ExecudeChangeMap(_nextMap);
            // Server.ExecuteCommand($"ds_workshop_changelevel {_nextMap}");
        }
        
        public List<string> GetFilteredVoteMaps()
        {
            // 获取当前时间
            var now = DateTime.Now.TimeOfDay;

            // 获取当前在线玩家数量
            g_CurrentPlayers = GetOnlinePlayerCount();

            // 获取已启用的地图列表，并按顺序反转地图名称
            var voteMaps = MapList
                .Where(map => map.Enabled &&
                              !(map.RestrictedTimes?.Any(period => now >= period.Start && now <= period.End) ?? false) 
                              && g_CurrentPlayers > ParseMapListMinPlayers(map.MinPlayers) 
                              && (!map.OnlyNominate || IsMapNominatedWorkshopId_OR_MapName(map.WorkshopId,map.FileName))) // 排除 OnlyNominate 为 true 且未被提名的地图
                .Select(map => map.FileName)
                .Reverse() // 反转地图名称的顺序
                .ToList();


            // 如果配置不包括当前地图，则从投票中移除当前地图
            if (!_config.IncludeCurrent)
            {
                voteMaps.Remove(GetCurrentMapName(false));
            }

            // 忽略已玩过的地图,查找已预定的地图 加入投票
            voteMaps = voteMaps.Where(map =>
                !_mapHistory.Contains(map) &&
                !g_Nominates.Values.Any(nomination =>
                    nomination.MapName.Equals(map, StringComparison.OrdinalIgnoreCase) ||
                    nomination.MapWorkShopId.Equals(map, StringComparison.OrdinalIgnoreCase)
                )
            ).Distinct().ToList();

            return voteMaps;
        }

        // Manually parse time strings and ensure they are within valid ranges
        private static bool TryParseTime(string timeString, out TimeSpan time)
        {
            time = TimeSpan.Zero;  // Initialize time as zero in case of failure
            var parts = timeString.Split(':');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int hours) &&
                int.TryParse(parts[1], out int minutes))
            {
                // Ensure hours are between 0 and 23 and minutes are between 0 and 59
                if (hours >= 0 && hours < 24 && minutes >= 0 && minutes < 60)
                {
                    time = new TimeSpan(hours, minutes, 0);
                    return true;
                }
            }
            return false;  // Return false if parsing or validation fails
        }

        private static void UT_SendAdminLog(CCSPlayerController? player, string message)
        {
            if (player != null && IsValidPlayer(player))
            {
                Instance.Logger.LogInformation($"[Admin] {player.PlayerName} ({player.SteamID}): {message}");
            }
        }

        private int ParseMapListMinPlayers(string minPlayersString)
        {
            if (string.IsNullOrEmpty(minPlayersString))
                return 0;

            if (int.TryParse(minPlayersString, out var minPlayers))
            {
                return minPlayers;
            }
            return 0;
        }


        public bool RemoveSteamIDRTV(ulong steamID)
        {
            if (_rtvCount.Contains(steamID))
            {
                _rtvCount.Remove(steamID);
            }
            return false;
        }

        private void ClearEnforceTimeLimit()
        {
            if (_EnforceTimeLimit != null)
            {
                _EnforceTimeLimit?.Kill();
                _EnforceTimeLimit = null;
            }
        }
        private void Clear_mapChangeByIdExecuteTimer()
        {
            if (_mapChangeByIdExecuteTimer != null)
            {
                _mapChangeByIdExecuteTimer?.Kill();
                _mapChangeByIdExecuteTimer = null;
            }
            if (_mapChangeByIdExecuteTimerByID != null)
            {
                _mapChangeByIdExecuteTimerByID?.Kill();
                _mapChangeByIdExecuteTimerByID = null;
            }

        }

        private void ClearVoteTimer()
        {
            if (_mapVoteTimer != null)
            {
                _mapVoteTimer?.Kill();
                _mapVoteTimer = null;
            }
        }
        private void ClearWarningTimer()
        {
            if (_mapWarningTimer != null)
            {
                _mapWarningTimer?.Kill();
                _mapWarningTimer = null;
            }
        }
        private void Clear_ExecuteTimer()
        {
            if (_mapExecuteTimer != null)
            {
                _mapExecuteTimer?.Kill();
                _mapExecuteTimer = null;
            }
        }
        private void Clear_EndChangeMapTimer()
        {
            if (_EndChangeMapTimer != null)
            {
                _EndChangeMapTimer?.Kill();
                _EndChangeMapTimer = null;
            }
        }
        public static string GetCurrentMapName(bool ToLower = false)
        {
            string szMapName = Server.MapName;
            if (ToLower) {
                szMapName = szMapName.ToLower();
            }

            if (!string.IsNullOrEmpty(szMapName))
                return szMapName;

            return string.Empty;
        }
    }

}
public static class PlayerUtils
{
    static public void PrintToChatMessage(this CCSPlayerController player, string message, params object[] args)
    {
        if (player == null
    || !player.IsValid
    || player.IsBot
    || player.IsHLTV)
            {
                return;
            }

        using (new WithTemporaryCulture(player.GetLanguage()))
        {
            StringBuilder builder = new("[MCE]");
            builder.AppendFormat(Instance.Localizer[message], args);
            player.PrintToChat(builder.ToString());
        }
    }


    static public void PrintToChatAll(string message, params object[] args)
    {
        // Get all valid players (excluding bots, HLTV, and disconnected players)
        Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false, Connected: PlayerConnectedState.Connected })
            .ToList()
            .ForEach(player =>
            {
                // Send the message to each player
                player.PrintToChatMessage(message, args);
            });
    }

 
}
