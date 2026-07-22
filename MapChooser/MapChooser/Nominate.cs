
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Reflection;

namespace MapChooser
{
    public partial class MapChooser
    {
        public class NominationInfo
        {
            public ulong SteamID { get; set; }
            public string PlayerName { get; set; }
            public string MapName { get; set; }

            public string MapWorkShopId { get; set; }

            public NominationInfo(ulong steamID, string playerName, string mapName, string mapWSId)
            {
                SteamID = steamID;
                PlayerName = playerName;
                MapName = mapName;
                MapWorkShopId = mapWSId;
            }

        }

        // 创建字典
        private Dictionary<ulong, NominationInfo> g_Nominates = new();

        // 添加或更新条目
        public void AddOrUpdateNomination(ulong steamID, string playerName, string mapName, string mapWorksghopId)
        {
            if (g_Nominates.ContainsKey(steamID))
            {
                // 如果玩家ID已存在，更新地图名
                g_Nominates[steamID].MapName = mapName;
            }
            else
            {
                // 如果玩家ID不存在，添加新的条目
                var nomination = new NominationInfo(steamID, playerName, mapName, mapWorksghopId);
                g_Nominates[steamID] = nomination;
            }
        }

        // 获取条目
        public NominationInfo? GetNominationBySteamID(ulong steamID)
        {
            if (g_Nominates.TryGetValue(steamID, out var nomination))
            {
                return nomination;
            }
            return null;
        }

        public bool IsMapNominated(string mapName)
        {
            return g_Nominates.Values.Any(nomination => nomination.MapName.Equals(mapName, StringComparison.OrdinalIgnoreCase));
        }

        // 根据 WorkshopID 查询某个地图是否已经被预定
        public bool IsMapNominatedByWorkshopId(string workshopId)
        {
            if (string.IsNullOrWhiteSpace(workshopId))
            {
                return false; // 如果 workshopId 是 null 或空字符串，直接返回 false
            }
            return g_Nominates.Values.Any(nomination => nomination.MapWorkShopId.Equals(workshopId, StringComparison.OrdinalIgnoreCase));
        }

        // 双重查找：根据 WorkshopID 或 MapName 查询地图是否已被预定
        public bool IsMapNominatedWorkshopId_OR_MapName(string? workshopId, string? mapName)
        {
            // 如果两个参数都是 null 或空白，则返回 false
            if (string.IsNullOrWhiteSpace(workshopId) && string.IsNullOrWhiteSpace(mapName))
            {
                return false;
            }

            // 进行查找，只要其中一个参数不是空的，就会进行匹配
            return g_Nominates.Values.Any(nomination =>
                (!string.IsNullOrWhiteSpace(workshopId) && nomination.MapWorkShopId.Equals(workshopId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(mapName) && nomination.MapName.Equals(mapName, StringComparison.OrdinalIgnoreCase))
            );
        }

        public bool RemoveSteamIDNomination(ulong steamID)
        {
            // 如果字典中存在该玩家的提名，删除并返回 true
            if (g_Nominates.ContainsKey(steamID))
            {
                return g_Nominates.Remove(steamID);
            }
            // 如果字典中不存在该玩家的提名，返回 false
            return false;
        }

        public List<NominationInfo> GetAllNominations()
        {
            return g_Nominates.Values.ToList();
        }
        public string GetAllNominationsString()
        {
            string data = string.Empty;
            var nominations = GetAllNominations();

            if (nominations != null)
            {
                // 拼接提名信息
                foreach (var nomination in nominations)
                {
                    data += $"{nomination.PlayerName} -: {nomination.MapName}\n";
                }
            }

            return data;
        }

        public string GetAllNominateStringIncludeSteamId(CCSPlayerController? player, bool PrintChatAll)
        {
            string data = string.Empty;
            var nominations = GetAllNominations();
            HashSet<string> processedMapNames = new HashSet<string>();

            if (nominations != null)
            {

                if (nominations.Count <= 0)
                {
                    PlayerUtils.PrintToChatAll($"mapchooser.NoReservationMessage");
                }

                // 拼接提名信息
                foreach (var nomination in nominations)
                {
                    data += $"{nomination.PlayerName}({nomination.SteamID}), Nonminate: {nomination.MapName}, MapWS: {nomination.MapWorkShopId}\n";
                    if (!processedMapNames.Contains(nomination.MapWorkShopId))
                    {
             
                        //data += $"{nomination.PlayerName}({nomination.SteamID}), 预定: {nomination.MapName}, MapWS: {nomination.MapWorkShopId}\n";
                        if (PrintChatAll)
                        {
                            PlayerUtils.PrintToChatAll($" {ChatColors.Green}{nomination.PlayerName}({nomination.SteamID}),-: {ChatColors.Red}{nomination.MapName}, MapWS: {ChatColors.Grey}{nomination.MapWorkShopId}");
                        }
                        if (player != null)
                        {
                            player.PrintToChatMessage($" {ChatColors.Green}{nomination.PlayerName}({nomination.SteamID}), -: {ChatColors.Red}{nomination.MapName}, MapWS: {ChatColors.Grey}{nomination.MapWorkShopId}");
                        }

                        // Add the map name to the processed set
                        processedMapNames.Add(nomination.MapWorkShopId);
                    }
                }
            }

            return data;
        }



    }

}
