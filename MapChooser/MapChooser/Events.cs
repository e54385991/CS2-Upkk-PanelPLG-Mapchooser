using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace MapChooser
{
    public partial class MapChooser
    {
        private HookResult EventOnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            if (@event.Userid == null)
                return HookResult.Continue;

            CCSPlayerController player = @event.Userid;

            if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
                return HookResult.Continue;

            RemoveSteamIDNomination(player.SteamID);
            RemoveSteamIDRTV(player.SteamID);

            AddTimer(1.0F, () =>
            {
                g_CurrentPlayers = GetOnlinePlayerCount();
                CheckRTV(g_CurrentPlayers);
            },TimerFlags.STOP_ON_MAPCHANGE);

            return HookResult.Continue;
        }

        private HookResult EventOnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            if (@event.Userid == null)
                return HookResult.Continue;

            CCSPlayerController player = @event.Userid;

            if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
                return HookResult.Continue;

            HUD_OnPlayerSpawn(player);

            return HookResult.Continue;
        }

        private HookResult EventOnPlayerTeamChanged(EventPlayerTeam @event, GameEventInfo info)
        {
            if (@event.Userid == null)
                return HookResult.Continue;

            CCSPlayerController player = @event.Userid;

            if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
                return HookResult.Continue;

            HUD_OnPlayerTeam(player);

            return HookResult.Continue;
        }

        private HookResult EventOnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            if (Instance._config.EnforceTimeLimit)
            {
                return HookResult.Continue;
            }

            if(string.IsNullOrEmpty(_nextMap))
            {
                return HookResult.Continue;
            }

            if (!Instance._config.UseGameTimeLimit)
            {
                int iTimeleft = GetTimeLeft_v2();
                int iMinutesLeft, iSecondsLeft;
                iMinutesLeft = Math.DivRem(iTimeleft, 60, out iSecondsLeft);

                if (iMinutesLeft <= 1) {
                    ExecudeChangeMap($"{_nextMap}");
                }

                return HookResult.Continue;
            }


            return HookResult.Continue;
        }

        private HookResult EventOnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            if (_timeLimitConVar == null)
            {
                _timeLimitConVar = ConVar.Find("mp_timelimit");
                Logger.LogInformation(" _timeLimitConVar mp_timelimit is init!");
            }

            ActiveRTV();
            CheckMapVoteStart();

            if (_nextMap == "")
            {
                PickRandomMap();
            }

            if (_startTime == 0.0F)
            {
                _startTime = Server.CurrentTime;
            }

            AddTimer(8.0f, () =>
            {
                PlayerUtils.PrintToChatAll($"{g_MapInfo}");
            },TimerFlags.STOP_ON_MAPCHANGE);

            return HookResult.Continue;
        }

        public HookResult OnMatchEndEvent(EventCsWinPanelMatch @event, GameEventInfo info)
        {
            if (Instance._config.EnforceTimeLimit)
            {
                return HookResult.Continue;
            }
     
            PlayerUtils.PrintToChatAll($"mapchooser.OnMapChangeMessage", _nextMap);
            PlayerUtils.PrintToChatAll($"mapchooser.OnMapChangeMessage", _nextMap);
            PlayerUtils.PrintToChatAll($"mapchooser.OnMapChangeMessage", _nextMap);


            _ActiveUSERTV = false;

            Logger.LogInformation($"[Mapchooser] MapChange To ->->-> {_nextMap}");

            Clear_EndChangeMapTimer();
            if (_EndChangeMapTimer == null)
            {
                Timer_ChangeMap();

                //if stuck repeat try it!
                _EndChangeMapTimer = AddTimer(20.5f, Timer_ChangeMap, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
            }

            return HookResult.Continue;
        }

        public void OnMapExtend(float time)
        {
            ClearHudMapHud_timeLeftDisplay();
        }
    }

}
