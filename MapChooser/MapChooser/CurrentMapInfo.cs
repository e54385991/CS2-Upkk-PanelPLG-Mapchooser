using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.RegularExpressions;
using System.Text;

namespace MapChooser
{
    public partial class MapChooser
    {
        public static string g_MapInfo = string.Empty;
        public static void MapInfo_GetInfo(string WorkShopId, string MapName)
        {
            g_MapInfo = string.Empty;
            
            // 如果是官方地图 则不需要检查信息
            if (g_OfficialMapNames.Contains(MapName))
            {
                return;
            }

            if (string.IsNullOrEmpty(WorkShopId) || WorkShopId == "0")
            {
                g_MapInfo = $"{Instance.Localizer["mapchooser.prefix"]} {Instance.Localizer["mapchooser.MapInfoMessage", MapName]}";

                return;
            }
            
            string sFilePath = $"{_workshopMapPath}{WorkShopId}/publish_data.txt";
         

            if (!File.Exists(sFilePath))
            {
                //g_MapInfo = "未找到publish_data地图信息";
                Instance.Logger.LogInformation($"MapInfo_GetInfo File Not Found {sFilePath}");
                return;
            }
            try
            {
                string fileContent = File.ReadAllText(sFilePath);
                // 解析数据
                Dictionary<string, string> parsedData = ParseContent(fileContent);

                StringBuilder StringBuilder = new StringBuilder();

                foreach (var kvp in parsedData)
                {
                    string sKey = string.Empty;
                    string sValue = string.Empty;
                    if (kvp.Key == "title")
                    {
                        sKey = Instance.Localizer["mapchooser.MapInfoTitleMessage"];
                        sValue = $"{ChatColors.Red}{kvp.Value} | ";
                    }
                    else if (kvp.Key == "publish_time_readable")
                    {
                        sKey = Instance.Localizer["mapchooser.MapInfoServerFileTimeMessage"];
                        sValue = $"{ChatColors.Lime}{kvp.Value}";
                    }

                    if (!string.IsNullOrEmpty(sKey)) {
                        StringBuilder.AppendLine($"{sKey}-{sValue}");
                    }
                }
                g_MapInfo = $"{Instance.Localizer["mapchooser.prefix"]} {Instance.Localizer["mapchooser.MapInfoMessage_version", MapName, WorkShopId, StringBuilder.ToString()]}";
     
            }
            catch (Exception ex)
            {
                g_MapInfo = $"{Instance.Localizer["mapchooser.prefix"]} {Instance.Localizer["mapchooser.MapInfoExceptionMessage", ex.Message]}";

            }

        }


        private static Dictionary<string, string> ParseContent(string content)
        {
            var result = new Dictionary<string, string>();

            try
            {
                // 正则表达式匹配 "key" "value"
                var regex = new Regex(@"""([^""]+)""\s*""([^""]+)""");
                var matches = regex.Matches(content);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count == 3) // 确保匹配到的是 key 和 value
                    {
                        string key = match.Groups[1].Value.Trim();
                        string value = match.Groups[2].Value.Trim();
                        result[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MapInfo_GetInfo 解析内容时发生错误: {ex.Message}");
            }

            return result;
        }
    }
}
