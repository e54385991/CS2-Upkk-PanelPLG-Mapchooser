using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using SteamKit2;

namespace MapChooser
{
    public partial class MapChooser
    {
        private static readonly object MapDataFileLock = new();

        [ConsoleCommand("css_mce_add_wsmap", "Add a Workshop map to the MapChooser map pool")]
        [CommandHelper(minArgs: 2, usage: "<map_name> <workshop_id>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void AddWorkshopMapCommand(CCSPlayerController? player, CommandInfo cmd)
        {
            if (!TryNormalizeMapName(cmd.GetArg(1), out string mapName, out string validationError))
            {
                cmd.ReplyToCommand($"[MCE] 添加失败：{validationError} 用法：css_mce_add_wsmap <地图名> <WSID>");
                return;
            }

            string workshopId = cmd.GetArg(2).Trim();
            if (!ulong.TryParse(workshopId, out ulong parsedWorkshopId) || parsedWorkshopId == 0)
            {
                cmd.ReplyToCommand("[MCE] 添加失败：Workshop ID 必须是大于 0 的数字。");
                return;
            }
            workshopId = parsedWorkshopId.ToString();

            if (!TryAddMapToMapList(mapName, workshopId, out bool updatedExistingMap, out string error))
            {
                cmd.ReplyToCommand($"[MCE] 添加失败：{error}");
                return;
            }

            LoadMaps();
            Server.ExecuteCommand($"mm_download_addon {workshopId}");
            UT_SendAdminLog(player, cmd.GetCommandString);
            Logger.LogInformation("[MapChooser] {Action} Workshop map {MapName} ({WorkshopId}) in {MapFile}", updatedExistingMap ? "Updated" : "Added", mapName, workshopId, _mapsData);
            cmd.ReplyToCommand(updatedExistingMap
                ? $"[MCE] 已将地图 {mapName} 的 WSID 更新为 {workshopId}，并已请求服务器下载。当前地图数：{MapList.Count}"
                : $"[MCE] 已添加工坊地图 {mapName}（WSID: {workshopId}），并已请求服务器下载。当前地图数：{MapList.Count}");

        }

        [ConsoleCommand("css_mce_add_localmap", "Add an installed local VPK map to the MapChooser map pool")]
        [CommandHelper(minArgs: 1, usage: "<map_name>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/ban")]
        public void AddLocalMapCommand(CCSPlayerController? player, CommandInfo cmd)
        {
            if (!TryNormalizeMapName(cmd.GetArg(1), out string mapName, out string validationError))
            {
                cmd.ReplyToCommand($"[MCE] 添加失败：{validationError} 用法：css_mce_add_localmap <地图名[.vpk]>");
                return;
            }

            string mapsDirectory = Path.GetFullPath(Path.Combine(Server.GameDirectory, "csgo", "maps"));
            string vpkPath = Path.GetFullPath(Path.Combine(mapsDirectory, $"{mapName}.vpk"));
            if (!vpkPath.StartsWith(mapsDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(vpkPath))
            {
                cmd.ReplyToCommand($"[MCE] 添加失败：未找到本地地图文件 {vpkPath}");
                return;
            }

            if (!TryAddMapToMapList(mapName, "0", out bool updatedExistingMap, out string error))
            {
                cmd.ReplyToCommand($"[MCE] 添加失败：{error}");
                return;
            }

            LoadMaps();
            UT_SendAdminLog(player, cmd.GetCommandString);
            Logger.LogInformation("[MapChooser] {Action} local map {MapName} from {VpkPath} in {MapFile}", updatedExistingMap ? "Updated" : "Added", mapName, vpkPath, _mapsData);
            cmd.ReplyToCommand(updatedExistingMap
                ? $"[MCE] 已将地图 {mapName} 更新为本地地图。当前地图数：{MapList.Count}"
                : $"[MCE] 已添加本地地图 {mapName}。当前地图数：{MapList.Count}");

        }

        private static bool TryNormalizeMapName(string input, out string mapName, out string error)
        {
            mapName = input.Trim().Trim('"');
            if (mapName.EndsWith(".vpk", StringComparison.OrdinalIgnoreCase))
            {
                mapName = mapName[..^4];
            }

            if (string.IsNullOrWhiteSpace(mapName))
            {
                error = "地图名不能为空。";
                return false;
            }

            bool hasInvalidCharacter = mapName.Any(character =>
                !((character >= 'a' && character <= 'z') ||
                  (character >= 'A' && character <= 'Z') ||
                  (character >= '0' && character <= '9') ||
                  character == '_' || character == '-' || character == '.'));

            if (hasInvalidCharacter || mapName is "." or "..")
            {
                error = "地图名只能包含英文字母、数字、下划线、短横线和点，且不能包含目录路径。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryAddMapToMapList(string mapName, string workshopId, out bool updatedExistingMap, out string error)
        {
            updatedExistingMap = false;

            lock (MapDataFileLock)
            {
                if (string.IsNullOrWhiteSpace(_mapsData))
                {
                    error = "maps.txt 路径尚未初始化。";
                    return false;
                }

                var mapList = new KeyValue("Maplist");
                if (File.Exists(_mapsData) && !mapList.ReadFileAsText(_mapsData))
                {
                    error = $"无法读取 {_mapsData}。";
                    return false;
                }

                KeyValue? existingMap = mapList.Children.FirstOrDefault(map =>
                    string.Equals(map.Name, mapName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(map["filename"].Value, mapName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(map["updatedname"].Value, mapName, StringComparison.OrdinalIgnoreCase));

                if (existingMap != null)
                {
                    string existingWorkshopId = existingMap["workshop_id"].Value ?? string.Empty;
                    if (string.Equals(existingWorkshopId, workshopId, StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"地图 {mapName} 已存在于 maps.txt，WSID 未发生变化。";
                        return false;
                    }
                }

                if (workshopId != "0" && mapList.Children.Any(map =>
                        map != existingMap &&
                        string.Equals(map["workshop_id"].Value, workshopId, StringComparison.OrdinalIgnoreCase)))
                {
                    error = $"Workshop ID {workshopId} 已存在于 maps.txt。";
                    return false;
                }

                if (existingMap != null)
                {
                    KeyValue? workshopIdEntry = existingMap.Children.FirstOrDefault(child =>
                        string.Equals(child.Name, "workshop_id", StringComparison.OrdinalIgnoreCase));

                    if (workshopIdEntry == null)
                    {
                        existingMap.Children.Add(new KeyValue("workshop_id", workshopId));
                    }
                    else
                    {
                        workshopIdEntry.Value = workshopId;
                    }

                    updatedExistingMap = true;
                }
                else
                {
                    var newMap = new KeyValue(mapName);
                    newMap.Children.Add(new KeyValue("workshop_id", workshopId));
                    newMap.Children.Add(new KeyValue("enabled", "1"));
                    newMap.Children.Add(new KeyValue("filename", mapName));
                    newMap.Children.Add(new KeyValue("updatedname", mapName));
                    newMap.Children.Add(new KeyValue("OnlyNominate", "0"));
                    mapList.Children.Add(newMap);
                }

                string? directory = Path.GetDirectoryName(_mapsData);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string temporaryPath = $"{_mapsData}.tmp";
                try
                {
                    mapList.SaveToFile(temporaryPath, false);
                    File.Move(temporaryPath, _mapsData, true);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "[MapChooser] Failed to add map {MapName} to {MapFile}", mapName, _mapsData);
                    error = $"写入 maps.txt 时发生错误：{ex.Message}";
                    return false;
                }
                finally
                {
                    try
                    {
                        if (File.Exists(temporaryPath))
                        {
                            File.Delete(temporaryPath);
                        }
                    }
                    catch (Exception cleanupException)
                    {
                        Logger.LogWarning(cleanupException, "[MapChooser] Failed to clean temporary map file {TemporaryPath}", temporaryPath);
                    }
                }

                error = string.Empty;
                return true;
            }
        }
    }
}
