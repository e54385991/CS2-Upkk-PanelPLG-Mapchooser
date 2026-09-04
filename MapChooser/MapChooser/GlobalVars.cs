
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using System.Security.Cryptography;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Modules.Cvars.Validators;
using CounterStrikeSharp.API;
namespace MapChooser
{
    public partial class MapChooser
    {
        public static HashSet<string> g_OfficialMapNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "de_dust2",
            "cs_office",
            "de_inferno",
            "de_nuke",
            "de_mirage",
            "de_cache",
            "de_overpass",
            "de_train",
            "de_cbble",
            "de_vertigo",
            "de_ancient",
            "de_anubis"
        };

        public class Config
        {
            public float VoteStartTime { get; set; } = 3.0f;
            public bool AllowExtend { get; set; } = true;
            public float ExtendTimeStep { get; set; } = 10f;
            public int ExtendLimit { get; set; } = 3;
            public int ExcludeMaps { get; set; } = 0;
            public int IncludeMaps { get; set; } = 5;
            public bool IncludeCurrent { get; set; } = false;
            public bool DontChangeRtv { get; set; } = true;
            public float VoteDuration { get; set; } = 15f;
            public bool RunOfFVote { get; set; } = true;
            public float VotePercent { get; set; } = 0.6f;
            public bool IgnoreSpec { get; set; } = true;
            public bool AllowRtv { get; set; } = true;
            public float RtvPercent { get; set; } = 0.6f;
            public float RtvDelay { get; set; } = 3.0f;
            public bool EnforceTimeLimit { get; set; } = true;

            public bool AutoDownload { get; set; } = true;

            public bool UseGameTimeLimit { get; set; } = true;

            public bool ChangeMapUse_host_workshop_map { get; set; } = true;

            public bool CrashMapRecover { get; set; } = false;

            public string VoteStartSound { get; set; } = "sounds/ui/counter_beep.vsnd";

            public int DisplayHudTimeleftRemaining { get; set; } = 0;

        }

        private static ConVar? _timeLimitConVar = null;

        //private string _mapsPath = "";
        public static CCSGameRules? _gameRules;

        private string _configPath = "";
        private Config _config = new Config();

        private static List<string> _mapHistory = new();

        //private List<string> _maps = new List<string>();

        private Dictionary<string, string> _mapsExtend = new Dictionary<string, string>();

        private Dictionary<ulong, string> _playerVotes = new();

        private Dictionary<string, int> _votes = new();

        private int _totalVotes = 0;
        private bool _voteActive = false;
        private int _extends = 0;

        private int _WarningCounter = 0;
        private List<ulong> _rtvCount = new();
        private bool _wasRtv = false;
        private bool _canRtv = true;
        private bool _ActiveUSERTV = false;
        private bool _IsMapVoteFinished = false;
        private float MapTimes = 0.0f;
        private float NextVoteDelay = 0.0f;

        private Timer? _EnforceTimeLimit;
        private Timer? _mapVoteTimer;
        private bool _IsSet_mapVoteTimer = false;
        private Timer? _mapWarningTimer;
        private Timer? _mapExecuteTimer;
        private Timer? _EndChangeMapTimer;
        private Timer? _mapChangeByIdExecuteTimer;
        private Timer? _mapChangeByIdExecuteTimerByID;
        private Timer? _CrashMapRecover_Timer;
        private DateTime MapStartTime;

        private static string _workshopMapPath = string.Empty;
        private string _nextMap = "";
        private string _CrashMapRecover = "";
        private string _AttemptFilePath = "";
        private float _startTime;
        private static string _mapsData = "";
        private static string map_historyFile = string.Empty;
        public static string _MapPingPath = string.Empty;
        public static List<MapPingMapInfo> MapPing_MapList = new List<MapPingMapInfo>();

        public static int _DisplayHudTimeleftRemaining = 0;

        public static string _currentMapName = "";

        public static bool PublishedFileIds_IsDownload = false;


        public static List<MapInfo> MapList = new List<MapInfo>();

        private DateTime _lastExecutionTime = DateTime.MinValue;
        private bool _firstExecution = true;
        private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
        public static string LastRunMapDownload = "";


        public const float g_RtvWaitTimeSec = 120;
        // Define a field to store the last map change time
        private DateTime _lastMapChangeTime = DateTime.MinValue;

        public static DateTime g_startTimeUtc = DateTime.UtcNow;
        public static DateTime g_CanRtvUtcTime;

        // convars
        public FakeConVar<float> cvar_css_map_timelimit = new("css_map_timelimit", "", 20.0f, flags: ConVarFlags.FCVAR_NONE, new RangeValidator<float>(2.0f, 1440.0f));
        public static float g_fMapTimelimit = 20.0f;

        public static int g_iCacheTimeLeft;
    }
}
