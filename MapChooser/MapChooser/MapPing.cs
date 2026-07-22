
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;


namespace MapChooser
{
    public partial class MapChooser
    {
        // 定义存储映射关系的类
        public class MapPingMapInfo
        {
            public string? MapName { get; set; }
            public string? WorkshopId { get; set; }
        }


        // 构造函数，初始化并加载 MapList
        public MapChooser()
        {
            //MapPing_MapList = MapPing_LoadMapListFromJson();
        }

        // 获取地图名称并保存
        // 获取地图名称并保存或更新
        public void MapPing_GetMapName_Save()
        {
            // 获取当前的地图名称
            string mapName = GetCurrentMapName();
            if (string.IsNullOrEmpty(mapName))
                return;

            // 获取 WorkshopId
            string workshopId = FindWorkshopIdByName(mapName);
            if (string.IsNullOrEmpty(workshopId) || workshopId == "0")
            {
                return;
            }

            // 检查是否已有相同的 MapName
            var existingMap = MapPing_MapList.FirstOrDefault(m => m.MapName == mapName);

            if (existingMap != null)
            {
                // 如果找到相同的 MapName，但 WorkshopId 不同，则更新它
                if (existingMap.WorkshopId != workshopId)
                {
                    existingMap.WorkshopId = workshopId;
                    Logger.LogInformation($"MapPing Updated Map: {mapName} with new WorkshopId: {workshopId}");
                }
            }
            else
            {
                // 如果没有找到相同的 MapName，添加新的 MapName-WorkshopId 映射
                MapPing_MapList.Add(new MapPingMapInfo { MapName = mapName, WorkshopId = workshopId });
                Logger.LogInformation($"MapPing Added new Map: {mapName} with WorkshopId: {workshopId}");
            }

            // 无论是更新还是添加，都将列表保存到 JSON 文件
            MapPing_SaveMapListToJson(MapPing_MapList);
        }

        // 从 JSON 文件中加载地图列表
        public List<MapPingMapInfo> MapPing_LoadMapListFromJson()
        {
            // 如果文件不存在，返回空列表
            if (!File.Exists(_MapPingPath))
            {
                return new List<MapPingMapInfo>();
            }

            // 读取 JSON 文件
            string json = File.ReadAllText(_MapPingPath);
            return JsonConvert.DeserializeObject<List<MapPingMapInfo>>(json) ?? new List<MapPingMapInfo>();
        }

        // 将地图列表保存到 JSON 文件
        public void MapPing_SaveMapListToJson(List<MapPingMapInfo> mapList)
        {
            // 序列化列表，并覆盖写入文件
            string json = JsonConvert.SerializeObject(mapList, Formatting.Indented);
            File.WriteAllText(_MapPingPath, json);
        }
        public string? MapPing_FindMapInfo(string key, string searchType, bool partialMatch = false)
        {
            if (searchType == "MapName")
            {
                // 根据 MapName 查找 WorkshopId
                var mapInfo = partialMatch
                    ? MapPing_MapList.FirstOrDefault(m => m.MapName != null && m.MapName.Contains(key))
                    : MapPing_MapList.FirstOrDefault(m => m.MapName == key);
                return mapInfo?.WorkshopId;
            }
            else if (searchType == "WorkshopId")
            {
                // 根据 WorkshopId 查找 MapName
                var mapInfo = partialMatch
                    ? MapPing_MapList.FirstOrDefault(m => m.WorkshopId != null && m.WorkshopId.Contains(key))
                    : MapPing_MapList.FirstOrDefault(m => m.WorkshopId == key);
                return mapInfo?.MapName;
            }
            else
            {
                return null;
            }
        }


    }
}
