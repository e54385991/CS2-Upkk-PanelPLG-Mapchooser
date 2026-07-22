using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace MapChooser;

[MinimumApiVersion(255)]
public partial class MapChooser : BasePlugin
{

    public override string ModuleName { get; } = "Map Chooser";
    public override string ModuleVersion { get; } = "2.5.1";
    public override string ModuleDescription { get; } = "Handles map voting and map changing";
    public override string ModuleAuthor { get; } = "Retro";

    public static MapChooser Instance { get; set; } = new();

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _mapHistory = LoadMapHistoryFromFile(map_historyFile);
        LoadData();
        MapPing_MapList = MapPing_LoadMapListFromJson();

        // 设置 RTV 的 UTC 时间
        g_CanRtvUtcTime = g_startTimeUtc.AddSeconds(g_RtvWaitTimeSec);

        string crashMapRecoverData = string.Empty;
        const int maxAttempts = 4;
        int attempts = 0;

        // 尝试读取恢复尝试计数
        if (File.Exists(_AttemptFilePath))
        {
            DateTime lastWriteTime = File.GetLastWriteTime(_AttemptFilePath);
            if ((DateTime.Now - lastWriteTime).TotalMinutes > 15)
            {
                // 如果文件超过 15 分钟，则删除文件
                File.Delete(_AttemptFilePath);
            }
            else
            {
                // 安全地解析尝试计数
                if (!int.TryParse(File.ReadAllText(_AttemptFilePath), out attempts))
                {
                    attempts = 0;
                }
            }
        }

        // 尝试读取崩溃地图恢复数据
        if (File.Exists(_CrashMapRecover))
        {
            crashMapRecoverData = File.ReadAllText(_CrashMapRecover);
        }

        if (attempts < maxAttempts)
        {
            // 增加尝试计数并保存
            attempts++;
            File.WriteAllText(_AttemptFilePath, attempts.ToString());
            Logger.LogInformation($"[MCE] 地图总计:{MapList.Count} 已启动 CrashMapRecover:{crashMapRecoverData}(attempts:{attempts}) Mapchooser-Ver:{ModuleVersion}");
        }
        else
        {
            // 如果超过最大尝试次数，删除崩溃恢复文件
            if (File.Exists(_CrashMapRecover))
            {
                File.Delete(_CrashMapRecover);
            }
            if (File.Exists(_AttemptFilePath))
            {
                File.Delete(_AttemptFilePath);
            }

            Logger.LogWarning($"[MCE] 已超过最大恢复尝试次数({attempts}/{maxAttempts}). Mapchooser-Ver:{ModuleVersion} CrashMapRecover:{crashMapRecoverData}");
        }
    }

    public override void Load(bool hotReload)
    {
        Instance = this;
        RegisterCVARS();
        // 基础路径部分
        var basePath = Server.GameDirectory + "/csgo/addons/counterstrikesharp/configs/plugins/MapChooser/";

        // _CrashMapRecover
        _configPath = Path.Combine(basePath, "config.json");
        _mapsData = Path.Combine(basePath, "maps.txt");
        _CrashMapRecover = Path.Combine(basePath, "map_recover.txt");
        _AttemptFilePath = Path.Combine(basePath, "CrashMapRecoverAttempts.txt");
        map_historyFile = Path.Combine(basePath, "map_history.txt");
        LastRunMapDownload = Path.Combine(basePath, "last_run_download.txt");
        _MapPingPath = Path.Combine(basePath, "MapPing.json");

        // 设置 workshop map 路径
        var workshopPathBase = "steamapps/workshop/content/730/";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _workshopMapPath = Path.Combine(Server.GameDirectory, "bin/linuxsteamrt64", workshopPathBase);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _workshopMapPath = Path.Combine(Server.GameDirectory, "bin/win64", workshopPathBase);
        }


        if (!hotReload && File.Exists(_CrashMapRecover))
        {
            _CrashMapRecover_Timer = AddTimer(36.0f, () => CrashMapRecover());
        }



        RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterEventHandler<EventRoundStart>(EventOnRoundStart);
        RegisterEventHandler<EventRoundEnd>(EventOnRoundEnd);
        RegisterEventHandler<EventCsWinPanelMatch>(OnMatchEndEvent);
        RegisterEventHandler<EventPlayerSpawn>(EventOnPlayerSpawn);
        RegisterEventHandler<EventPlayerTeam>(EventOnPlayerTeamChanged);
        RegisterEventHandler<EventPlayerDisconnect>(EventOnPlayerDisconnect);

        if (hotReload)
        {
            LoadData();
            OnMapStart(GetCurrentMapName(false));

            AddTimer(60.0F, () =>
            {
                StartCheckMapUpdate(false);
            });
        }

        AddTimer(10.0F, () =>
        {
            ShoudMapVote();
        }, TimerFlags.REPEAT);

        
    }

    public override void Unload(bool hotReload)
    {
    }


    public void LoadData()
    {
        LoadConfig();
        LoadMaps();
        TrimMapHistory();
        ActiveRTV();
        Logger.LogInformation("[MapChooser] Load Finished!");
    }


    private void OnMapStart(string mapName)
    {
        Clear_mapChangeByIdExecuteTimer();
        g_startTimeUtc = DateTime.UtcNow;
        g_CanRtvUtcTime = g_startTimeUtc.AddSeconds(g_RtvWaitTimeSec);
        _currentMapName = mapName;
        g_fMapTimelimit = cvar_css_map_timelimit.Value;

        string mapid = FindWorkshopIdByName(mapName);
        if (!Instance._config.UseGameTimeLimit)
        {
            MapTimes = g_fMapTimelimit;
        }

        AddTimer(75.0f, CheckMapStuck, TimerFlags.STOP_ON_MAPCHANGE | TimerFlags.REPEAT);
        ActiveRTV();
        LoadData();

        Clear_ExecuteTimer();
        _wasRtv = false;
        _voteActive = false;

        MapInfo_GetInfo(mapid, mapName);
        MapPing_MapList = MapPing_LoadMapListFromJson();
        MapPing_GetMapName_Save();

        AddTimer(60.0F, () =>
        {
            StartCheckMapUpdate(false);
        });

        AddTimer(5.0F, () =>
        {
            FindGameRules();
        });

    }
    private void RegisterCVARS()
    {
        g_fMapTimelimit = cvar_css_map_timelimit.Value;

        cvar_css_map_timelimit.ValueChanged += (sender, value) =>
        {
            g_fMapTimelimit = value;

            if (!Instance._config.UseGameTimeLimit)
            {
                MapTimes = g_fMapTimelimit;
            }

            if (g_fMapTimelimit <= _config.VoteStartTime)
            {
                ShoudMapVote();
            }
        };


        RegisterFakeConVars(typeof(ConVar));
    }


    private void OnMapEnd()
    {
        _ActiveUSERTV = true;
        _IsSet_mapVoteTimer = false;
        _nextMap = "";
        //Add the current map to map history


        //Clear the various lists/dictionaries
        HUD_OnMapEnd();
        g_Nominates.Clear();
        _mapsExtend.Clear();
        _playerVotes.Clear();
        _votes.Clear();
        _rtvCount.Clear();

        Clear_ExecuteTimer();
        ClearWarningTimer();
        ClearEnforceTimeLimit();

        //Set mp_timelimit convar handler to null
        _timeLimitConVar = null;

        //Reinitialize values to 0
        _totalVotes = 0;
        _extends = 0;
        ClearVoteTimer();
        Clear_EndChangeMapTimer();

        //Reinitialize values to false
        _voteActive = false;
        _IsMapVoteFinished = false;
        _wasRtv = false;
        _canRtv = true;
        MapTimes = 0.0f;
        _startTime = 0.0f;

        SaveMapHistoryToFile(map_historyFile);
    }


}



