
namespace MapChooser
{
    public partial class MapChooser
    {
        public static void ParseKVFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("File does not exist: " + filePath);
                return;
            }

            string[] lines = File.ReadAllLines(filePath);
            MapInfo? currentMap = null;
            string key = "", value = "";

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("\""))
                {
                    int quoteIndex = trimmedLine.IndexOf('\"', 1);
                    if (quoteIndex > 0)
                    {
                        key = trimmedLine.Substring(1, quoteIndex - 1);
                        int valueStart = trimmedLine.IndexOf('\"', quoteIndex + 1) + 1;
                        int valueEnd = trimmedLine.IndexOf('\"', valueStart);
                        if (valueEnd > valueStart)
                        {
                            value = trimmedLine.Substring(valueStart, valueEnd - valueStart);
                        }
                    }

                    switch (key)
                    {
                        case "Maplist":
                            break;  // Root key, do nothing
                        case "workshop_id":
                            if (currentMap != null)
                                currentMap.WorkshopId = value;
                            break;
                        case "filename":
                            if (currentMap != null)
                            {
                                currentMap.FileName = value;
                            }
                            break;
                        case "enabled":
                            if (currentMap != null)
                                currentMap.Enabled = value == "1";
                            break;
                        case "updatedname":
                            if (currentMap != null)
                                currentMap.UpdatedName = value;
                            break;
                        case "search":
                            if (currentMap != null)
                                currentMap.Search = value;
                            break;
                        case "MinPlayers":
                            if (currentMap != null)
                                currentMap.MinPlayers = value;
                            break;
                        case "OnlyNominate":
                            if (currentMap != null)
                                currentMap.OnlyNominate = value == "1";
                            break;
                        case "RestrictedTimes":
                            if (currentMap != null)
                            {
                                if (!string.IsNullOrEmpty(value))
                                {
                                    var periods = value.Split(';');  // Assume periods are separated by ';'
                                    foreach (var period in periods)
                                    {
                                        var times = period.Split('-');  // Assume each period is in 'HH:mm-HH:mm' format
                                        if (times.Length == 2)
                                        {
                                            if (TryParseTime(times[0], out TimeSpan start) && TryParseTime(times[1], out TimeSpan end))
                                            {
                                                currentMap.RestrictedTimes.Add(new TimePeriod { Start = start, End = end });
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                        case "DisableServerInPerfectWorld":
                            // Legacy server policy setting; ignored for map-list compatibility.
                            break;
                        default:
                            if (currentMap != null)
                                MapList.Add(currentMap);
                            currentMap = new MapInfo { Name = key };
                            break;
                    }

                }
            }

            if (currentMap != null && !MapList.Contains(currentMap) && !String.IsNullOrEmpty(currentMap.WorkshopId))
            {
                MapList.Add(currentMap);
            }
        }
        public class TimePeriod
        {
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }
        }

        public class MapInfo
        {
            public string Name { get; set; }
            public string FileName { get; set; }
            public string WorkshopId { get; set; }
            public bool Enabled { get; set; }

            public string CN_Name { get; set; }
            public string UpdatedName { get; set; }
            public string Search { get; set; }

            public List<TimePeriod> RestrictedTimes { get; set; } = new List<TimePeriod>();

            public string MinPlayers { get; set; }
            public bool OnlyNominate { get; set; } = false;

            public MapInfo()
            {
                Name = string.Empty;
                WorkshopId = string.Empty;
                Enabled = true;
                CN_Name = string.Empty;
                FileName = string.Empty;
                UpdatedName = string.Empty;
                Search = string.Empty;
                MinPlayers = string.Empty;
                RestrictedTimes = new List<TimePeriod>();
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Name, WorkshopId);
            }
        }
    }
}
