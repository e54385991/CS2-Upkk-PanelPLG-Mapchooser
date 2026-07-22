using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;



namespace MapChooser
{
    public partial class MapChooser
    {
        private readonly Dictionary<int, DateTime> _gServerTime = new();
        private const int RtvUnRTV_WaitTimeSeconds = 25;
        private static int g_CurrentPlayers = 0;


        [ConsoleCommand("css_rtv", "Rocks the vote")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnRtVCommand(CCSPlayerController? player, CommandInfo cmd)
        {
            if (player == null)
                return;

            PlayerRTV(player);
        }

        [ConsoleCommand("css_timeleft", "timeleft mce")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnTimeLeftCommand(CCSPlayerController? player, CommandInfo cmd)
        {
            if (player == null || !player.IsValid)
                return;

            int iTimeleft = GetTimeLeft_v2(true);
            int iMinutesLeft, iSecondsLeft;
            iMinutesLeft = Math.DivRem(iTimeleft, 60, out iSecondsLeft);
           
            string Message = $"[Mapchooser] 地图参数时长:{MapTimes}分 剩:{ChatColors.Orange}{iMinutesLeft}.{iSecondsLeft} ";

            player.PrintToChat(Message);
        }


        [ConsoleCommand("css_unrtv", "No Rocks the vote")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnUnRtVCommand(CCSPlayerController? player, CommandInfo cmd)
        {
            if (player == null)
                return;

            if (!IsValidPlayer(player))
                return;

            if (!_rtvCount.Contains(player.SteamID)) return;

            if (_gServerTime.TryGetValue(player.Slot, out var cooldownTime) && DateTime.UtcNow < cooldownTime)
            {
                var remainingTime = (cooldownTime - DateTime.UtcNow).TotalSeconds;
                player.PrintToChat($"{Localizer["mapchooser.prefix"]} {Localizer["mapchooser.CommandCooldownMessage", remainingTime]}");
                return;
            }


            _rtvCount.Remove(player.SteamID);
            Server.PrintToChatAll(
                $"{Localizer["mapchooser.prefix"]} {Localizer["mapchooser.unrtv", player.PlayerName, _rtvCount.Count, Utilities.GetPlayers().Count(player => player.TeamNum > 1)]}");
            _gServerTime[player.Slot] = DateTime.UtcNow.AddSeconds(RtvUnRTV_WaitTimeSeconds);
        }

        [ConsoleCommand("css_nominate", "Puts up a map to be in the next vote")]
        [ConsoleCommand("css_yd", "Puts up a map to be in the next vote")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]

        public void OnNominateCommand(CCSPlayerController? player, CommandInfo cmd)
        {
            if (player == null)
                return;

            if (!IsValidPlayer(player))
                return;


            if (_voteActive) return;

            var searchMap = cmd.GetCommandString;
            var menu = new ChatMenu(Localizer["mapchooser.nominate_header"]);

            if (_gServerTime.TryGetValue(player.Slot, out var cooldownTime) && DateTime.UtcNow < cooldownTime)
            {
                var remainingTime = (cooldownTime - DateTime.UtcNow).TotalSeconds;
                player.PrintToChat($"{Localizer["mapchooser.prefix"]} {Localizer["mapchooser.CommandCooldownMessage", remainingTime]}");
                return;
            }

            if (!string.IsNullOrWhiteSpace(searchMap) && searchMap.Length >= 2)
            {
                searchMap = searchMap.ToLower().Replace("css_yd", "").Replace("css_nominate", "").Replace(" ", "").Trim();

                if (searchMap.Length >= 1)
                {
                    cmd.ReplyToCommand($"{Localizer["mapchooser.prefix"]} {Localizer["mapchooser.SearchMapMessage", searchMap]}");
                }
                else
                {
                    searchMap = string.Empty;
                }
            }

            var filteredMaps = string.IsNullOrEmpty(searchMap)
                ? MapList.Where(mapInfo => mapInfo != null && mapInfo.Enabled)
                : MapList.Where(mapInfo => mapInfo != null && mapInfo.Enabled &&
                                            (mapInfo.FileName != null && mapInfo.FileName.IndexOf(searchMap, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             mapInfo.WorkshopId != null && mapInfo.WorkshopId.IndexOf(searchMap, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             mapInfo.Search != null && mapInfo.Search.IndexOf(searchMap, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            filteredMaps = filteredMaps.AsEnumerable().Reverse().ToList();

            // 打印反转后的列表
            player.PrintToConsole("OnNominateCommand-VoteFilteredMaps:");
            foreach (var mapInfo in filteredMaps)
            {
                player.PrintToConsole(mapInfo.FileName);
            }

            player.PrintToChat($"{Localizer["mapchooser.prefix"]} {Localizer["mapchooser.MapCountMessage", MapList.Count]}");

            g_CurrentPlayers = GetOnlinePlayerCount();
            foreach (var mapInfo in filteredMaps)
            {
                var now = DateTime.Now.TimeOfDay;
                var restrictedPeriod = mapInfo.RestrictedTimes?.FirstOrDefault(period => now >= period.Start && now <= period.End);

                var restrictionMessages = new List<string>();

                if (restrictedPeriod != null)
                {
                    // 如果在受限时间段内，添加受限时间段的提示信息
                    //restrictionMessages.Add($"不可用1: {restrictedPeriod.Start:hh\\:mm} - {restrictedPeriod.End:hh\\:mm}");
                    restrictionMessages.Add($"{Localizer["mapchooser.RestrictedPeriodMessage", restrictedPeriod.Start, restrictedPeriod.End]}");

                }

                int MinPlayers = ParseMapListMinPlayers(mapInfo.MinPlayers);
                if (MinPlayers > 0 && g_CurrentPlayers < MinPlayers)
                {
                    // 如果玩家数量不足，添加玩家数量的提示信息
                    //restrictionMessages.Add($"不可用2: 玩家数量需要大于 {MinPlayers} 位");
                    restrictionMessages.Add($"{Localizer["mapchooser.MinPlayersMessage", MinPlayers]}");

                }

                if (restrictionMessages.Count > 0)
                {
                    // 拼接所有的受限信息，并添加到菜单选项中
                    string restrictedMessage = $"{mapInfo.FileName} ({string.Join(" | ", restrictionMessages)})";
                    menu.AddMenuOption(restrictedMessage, (_, _) => { }, true);
                    continue; // 跳过此地图的进一步处理
                }

                string displayText = mapInfo.FileName;

                if (mapInfo.FileName == GetCurrentMapName(false))
                {
                    menu.AddMenuOption(Localizer["mapchooser.nominate_current_map", displayText], (_, _) => { }, true);
                }
                else if (_mapHistory.Contains(mapInfo.FileName))
                {
                    int index = _mapHistory.IndexOf(mapInfo.FileName);  // 获取元素的索引
                    string displayTextWithIndex = $"{displayText} {Localizer["mapchooser.nominate_req_play_more_maps", index + 1]}"; 
                    menu.AddMenuOption(Localizer["mapchooser.nominate_recent", displayTextWithIndex], (_, _) => { }, true);
                }
                else if (IsMapNominatedWorkshopId_OR_MapName(mapInfo.WorkshopId, mapInfo.FileName))
                {
                    menu.AddMenuOption(Localizer["mapchooser.nominate_nominated", displayText], (_, _) => { }, true);
                }
                else
                {
                    menu.AddMenuOption(displayText, (player, option) =>
                    {
                        if (!IsMapNominatedWorkshopId_OR_MapName(mapInfo.WorkshopId, mapInfo.FileName))
                        {
                            AddOrUpdateNomination(player.SteamID, player.PlayerName, mapInfo.FileName, mapInfo.WorkshopId);
                            Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} {Localizer["mapchooser.nominate", player.PlayerName, option.Text]} {displayText} WorkShopId:{mapInfo.WorkshopId}");
                        }
                        else
                        {
                            player.PrintToChat($"{Localizer["mapchooser.prefix"]} {Localizer["mapchooser.hasnominated", mapInfo.FileName]}");
                        }

                        _gServerTime[player.Slot] = DateTime.UtcNow.AddSeconds(RtvUnRTV_WaitTimeSeconds);

                        MenuManager.CloseActiveMenu(player);
                    });
                }
            }

            MenuManager.OpenChatMenu(player, menu);
        }




        [ConsoleCommand("css_force_mapvote", "css_force_mapvote")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void CSS_force_mapvote(CCSPlayerController? player, CommandInfo cmd)
        {
            StartMapVote();

            UT_SendAdminLog(player!, cmd.GetCommandString);

        }


        [ConsoleCommand("css_debug_listmaps", "css_debug_listmaps")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void Css_debug_listmaps(CCSPlayerController? player, CommandInfo cmd)
        {
            var filteredMaps = MapList.Where(mapInfo => mapInfo != null && mapInfo.Enabled);
 
            filteredMaps = filteredMaps.AsEnumerable().Reverse().ToList();

            // 输出所有元素
            foreach (var map in filteredMaps)
            {
                // 调用 MapPing_FindMapInfo 获取 ServerMapName
                string? ServerMapName = MapPing_FindMapInfo(map.WorkshopId, "WorkshopId", false);

                // 如果 ServerMapName 为 null，则显示为 "null"
                string displayServerMapName = ServerMapName ?? "null";

                // 输出包含文件名、WorkshopId 和 ServerMapName 的信息
                cmd.ReplyToCommand($"FileName:{map.FileName} wsid:{map.WorkshopId} ServerMapName:{displayServerMapName}");

            }

        }

        [ConsoleCommand("css_debug_autochangemap", "css_debug_autochangemap")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void Css_debug_autochangemap(CCSPlayerController? player, CommandInfo cmd)
        {
            var filteredMaps = MapList.Where(mapInfo => mapInfo != null && mapInfo.Enabled);

            // 如果 cmd.GetArg(1) == "1"，进一步过滤 WorkshopId 不为 null 的地图
            if (cmd.GetArg(1) == "1")
            {
                // 过滤掉那些 WorkshopId 存在于 MapPing_MapList 中的地图
                filteredMaps = filteredMaps.Where(mapInfo =>
                    !MapPing_MapList.Any(pingInfo => pingInfo.WorkshopId == mapInfo.WorkshopId));
                Logger.LogInformation($"Css_debug_autochangemap GetArg1 == 1 Count:{filteredMaps.Count()}");
            }


            float delay = 0.0F;
            int total = filteredMaps.Count();
            int Currentcount = 0;
            foreach (var map in filteredMaps)
            {
                delay += 13.0F; // 每次增加 20 秒的延迟
                cmd.ReplyToCommand($"FileName:{map.FileName} not found");

                AddTimer(delay, () =>
                {
                    Currentcount++;
                    Logger.LogInformation($"Css_debug_autochangemap {map.FileName} {Currentcount} / {total}");
                    ExecudeChangeMap(map.FileName);
                });
            }

            // 检查 player 是否为 null，避免空引用异常
            if (player != null)
            {
                UT_SendAdminLog(player, cmd.GetCommandString);
            }
            else
            {
                Logger.LogError("Player is null, unable to send admin log.");
            }
        }


        [ConsoleCommand("css_ydlist", "css_ydlist")]
        [ConsoleCommand("css_nominatelist", "css_nominatelist")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void Css_nominatelist(CCSPlayerController? player, CommandInfo cmd)
        {
            if (player == null)
                return;

            if (!IsValidPlayer(player))
                return;

            GetAllNominateStringIncludeSteamId(player, false);
        }

        [ConsoleCommand("css_nextmap", "css_nextmap")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void Css_NextMap(CCSPlayerController? player, CommandInfo cmd)
        {
            if (player == null)
                return;

            if (!IsValidPlayer(player))
                return;

            if(!_IsMapVoteFinished)
            {
                player.PrintToChatMessage($"mapchooser.NoVoteMessage");

                return;
            }
            player.PrintToChatMessage($"mapchooser.NextMapMessage", _nextMap);

        }


        [ConsoleCommand("css_debug_votemaplist", "css_debug_votemaplist")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void Css_debug_votemaplist(CCSPlayerController? player, CommandInfo cmd)
        {
            if (player == null)
                return;

            if (!IsValidPlayer(player))
                return;

            var voteMaps = GetFilteredVoteMaps();
            // 输出所有元素
            foreach (var map in voteMaps)
            {
                player.PrintToConsole(map);
            }

            // 准备要投票的地图列表
            var nextMap = g_Nominates.Values.Select(nomination => nomination.MapName).ToList();

            while (nextMap.Count < _config.IncludeMaps && voteMaps.Any())
            {
                int index = GetRandomIndex(voteMaps.Count);
                nextMap.Add(voteMaps[index]);
                voteMaps.RemoveAt(index);
            }
            player.PrintToConsole("VOTE===================");
            player.PrintToConsole("VOTE===================");
            player.PrintToConsole("VOTE===================");

            // 输出所有元素
            foreach (var sNextMap in nextMap)
            {
                player.PrintToConsole(sNextMap);
            }

            UT_SendAdminLog(player!, cmd.GetCommandString);

        }

        [ConsoleCommand("css_setnextmapwsid", "CSS_SetNexMaptWsId")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void CSS_SetNexMaptWsId(CCSPlayerController? player, CommandInfo cmd)
        {
            string wsid =  cmd.GetArg(1).Trim();
            var MapName = FindMapNameByWSId(wsid);
            if (string.IsNullOrEmpty(MapName))
            {
                cmd.ReplyToCommand($" wsid:{wsid} maps.txt 没有找到关联地图.");
                return;
            }

            UT_SendAdminLog(player!, cmd.GetCommandString);

            SetNextMap(MapName);
            cmd.ReplyToCommand($" 设置下一张地图 {MapName }成功.");

            Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} 管理员已设置下一张地图 {MapName} -> {wsid}");
        }

        [ConsoleCommand("css_mce_wsmap", "css_mce_wsmap")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void Css_mce_wsmap(CCSPlayerController? player, CommandInfo cmd)
        {
            string Input = cmd.GetArg(1).Trim();
            var FindMapName = FindMapNameByWSId(Input);
            if (string.IsNullOrEmpty(FindMapName))
            {
                var issuedCommand = long.TryParse(Input, out var mapId) ? $"host_workshop_map {mapId}" : $"ds_workshop_changelevel {Input}";
                cmd.ReplyToCommand($" {Input} maps.txt 没有找到关联地图. 尝试使用 {issuedCommand} ");

                AddTimer(4.5F, () =>
                {
                    Server.ExecuteCommand(issuedCommand);
                });

            }
            else { 
                ExecudeChangeMap(FindMapName);
            }
            UT_SendAdminLog(player!, cmd.GetCommandString);

            cmd.ReplyToCommand($" 更换地图处理中");
            Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} 管理员 更换地图处理中 {Input}");

        }


        [ConsoleCommand("css_mce_download_maplist", "CSS_DownloadMapList")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void CSS_DownloadMapList(CCSPlayerController? player, CommandInfo cmd)
        {
            DownloadMapByLists();
            cmd.ReplyToCommand($" 服务器 下载/更新 地图启动.");
            Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} 服务器批量 下载/更新 地图启动");
        }



        [ConsoleCommand("css_setnextmap", "CSS_SetNextMap")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void CSS_SetNextMap(CCSPlayerController? player, CommandInfo cmd)
        {
            string MapName = cmd.GetArg(1).Trim();
            string WsId = FindWorkshopIdByName(MapName);
            if (string.IsNullOrEmpty(WsId))
            {
                cmd.ReplyToCommand($" MapName:{MapName} maps.txt没有找到关联地图的ID.");
                return;
            }
            UT_SendAdminLog(player!, cmd.GetCommandString);

            SetNextMap(MapName);
            cmd.ReplyToCommand($" 设置下一张地图 {MapName}成功.");
            Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} 管理员已设置下一张地图 {MapName} -> {WsId}");
        }


        [ConsoleCommand("css_force_rtv", "css_force_rtv")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void CSS_force_rtv(CCSPlayerController? player, CommandInfo cmd)
        {
            _wasRtv = true;
            StartMapVote();
            Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} 管理已强制启动RTV");
            UT_SendAdminLog(player!, cmd.GetCommandString);
        }

        [ConsoleCommand("css_random_map", "css_random_map")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]

        public void CSS_random_map(CCSPlayerController? player, CommandInfo cmd)
        {
            ChangeRandomMap();
            Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} 管理员随机更换地图");
            UT_SendAdminLog(player!, cmd.GetCommandString);
        }

        [ConsoleCommand("css_update_wsmapid", "UpdateWorkShopMapId")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void UpdateWorkShopMapId(CCSPlayerController? player, CommandInfo cmd)
        {
            string wsid = cmd.GetArg(1).Trim();
            var MapName = FindMapNameByWSId(wsid);
            if (string.IsNullOrEmpty(MapName))
            {
                cmd.ReplyToCommand($" wsid:{wsid} maps.txt 没有找到关联地图无法进行此操作..");
                return;
            }
            Server.ExecuteCommand($"mm_download_addon {wsid}");

            Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} 管理员已更新地图. 已通知服务器下载 -> mm_download_addon {wsid} 建议稍等5分钟左右再换图.");

            UT_SendAdminLog(player!, cmd.GetCommandString);
        }

        //更新订阅
        [ConsoleCommand("css_update_ws_collection", "UpdateWorkShopMapCollection")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void UpdateWorkShopMapCollection(CCSPlayerController? player, CommandInfo cmd)
        {
            _firstExecution = true;
            AddTimer(1.0F, () =>
            {
                StartCheckMapUpdate(true);
            });

            Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} 管理员已更新地图订阅..");

            UT_SendAdminLog(player!, cmd.GetCommandString);
        }

        [ConsoleCommand("css_reload_maplist", "css_reload_maplist")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void Reload_maplist(CCSPlayerController? player, CommandInfo cmd)
        {
            OnMapStart(GetCurrentMapName(false));
            Server.PrintToChatAll($"{Localizer["mapchooser.prefix"]} 管理员重新加载地图.. 当前解析数量:{MapList.Count} 请注意 所有预定都将被清空");
            g_Nominates.Clear();

            UT_SendAdminLog(player!, cmd.GetCommandString);
        }


        [ConsoleCommand("css_ext_maptime", "Css_ext_maptime")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void Css_ext_maptime(CCSPlayerController? player, CommandInfo cmd)
        {
            // 当前存在强制换图情况的处理
            if (_config.EnforceTimeLimit && _EnforceTimeLimit != null && !string.IsNullOrEmpty(_nextMap))
            {
                ClearEnforceTimeLimit();

                // 需要以秒为单位 - 分钟数 * 60 后换图
                _EnforceTimeLimit = AddTimer(_config.ExtendTimeStep * 60.0f, () =>
                {
                    ExecudeChangeMap($"{_nextMap}");
                }, TimerFlags.STOP_ON_MAPCHANGE);


                PlayerUtils.PrintToChatAll($"mapchooser.MapChangeTimerMessage", _config.ExtendTimeStep, _nextMap);
            }


            ExtendMapTime();

            PlayerUtils.PrintToChatAll($"mapchooser.ExtendTimeStepMessage", _config.ExtendTimeStep);

            UT_SendAdminLog(player!, cmd.GetCommandString);
        }


        [ConsoleCommand("css_force_display_maptimeleft", "Css_ForceDisplayMapChangeHud")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void Css_ForceDisplayMapChangeHud(CCSPlayerController? player, CommandInfo cmd)
        {
            _DisplayHudTimeleftRemaining = 9999991;
  
            UT_SendAdminLog(player!, cmd.GetCommandString);
        }


        [ConsoleCommand("css_clean_crashmaprecover", "clean crashmaprecover file")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void Clean_crashmaprecover(CCSPlayerController? player, CommandInfo cmd)
        {
            if (File.Exists(_CrashMapRecover))
            {
                File.WriteAllText(_CrashMapRecover, string.Empty);
            }

            UT_SendAdminLog(player!,cmd.GetCommandString);
        }

        [ConsoleCommand("css_mapinfo", "display map info")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnMapInfo(CCSPlayerController? player, CommandInfo cmd)
        {
            if (player is null) return;

            player.PrintToChat($"{g_MapInfo}");
        }

    }

}
