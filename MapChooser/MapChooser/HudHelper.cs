
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace MapChooser
{
    public partial class MapChooser
    {
        public static Timer? Hud_MapHud_timeLeft_DisplayTimer;
        private static Dictionary<CCSPlayerController, CPointWorldText> Hud_playerHudMap = new();
        private static readonly Dictionary<int, CPointOrient> Hud_playerAnchorMap = new();
        private static float Hud_timeLeft = 0.0f;
        private static bool bHud_IsDisplay = false;

        public static void HUD_OnMapEnd()
        {
            ClearHudMapHud_timeLeftDisplay();
        }

        public static void HUD_OnPlayerSpawn(CCSPlayerController player)
        {

        }

        public static void HUD_OnPlayerTeam(CCSPlayerController xPlayer)
        {
            Instance.AddTimer(2.5f, () =>
            {
                if (xPlayer == null || !xPlayer.IsValid || xPlayer.IsHLTV || xPlayer.IsBot)
                {
                    return;
                }

                if (xPlayer.PlayerPawn == null || xPlayer.PlayerPawn.Value == null)
                {
                    return;
                }

                // Only create HUD if countdown is active
                if (Hud_timeLeft > 0)
                {
                    // Remove existing HUD for the player, if any
                    if (Hud_playerHudMap.TryGetValue(xPlayer, out var existingHud) && existingHud != null && existingHud.IsValid)
                    {
                        existingHud.AddEntityIOEvent("Kill", null, null, "", 0.3f);
                        Hud_playerHudMap.Remove(xPlayer);
                    }

                    // Create new HUD for the spawning player
                    CPointWorldText? hud = CreateHud(
                        player: xPlayer,
                        text: HUD_GetMapChangeHud_timeLeft(),
                        size: 27,
                        color: Color.Aquamarine,
                        font: "Consolas",
                        shiftX: -9.0f,
                        shiftY: 1.0f
                    );

                    if (hud == null)
                        return;


                    Hud_playerHudMap[xPlayer] = hud;
                }
            }, TimerFlags.STOP_ON_MAPCHANGE);

        }


        public static void Hud_MapHud_timeLeft_Display(int sec)
        {
            if (bHud_IsDisplay)
                return;

            // Clear any existing HUDs and timer
            ClearHudMapHud_timeLeftDisplay();
            bHud_IsDisplay = true;
            Hud_timeLeft = sec;

            Instance.AddTimer(1.5f, () =>
            {
                // Create HUD for each connected player
                foreach (var xPlayer in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
                {
                    if (xPlayer == null || !xPlayer.IsValid || xPlayer.IsHLTV || xPlayer.IsBot)
                    {
                        continue;
                    }
                    if (xPlayer.PlayerPawn == null || xPlayer.PlayerPawn.Value == null)
                    {
                        continue;
                    }

                    CPointWorldText? hud = null;
                    // Try creating HUD, retry once if null
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        hud = CreateHud(
                            player: xPlayer,
                            text: HUD_GetMapChangeHud_timeLeft(),
                            size: 27,
                            color: Color.Aquamarine,
                            font: "Consolas",
                            shiftX: -9.0f,
                            shiftY: 1.0f
                        );

                        if (hud != null)
                        {
                            break; // Success, exit retry loop
                        }
                    }

                    if (hud == null)
                    {
                        xPlayer.PrintToChat("[Hud_MapHud_timeLeft_Display] CreateHud Erorr -> null");
                        continue;
                    }

                    xPlayer.PrintToChat("Hud_MapHud_timeLeft_Display");
                }

                Hud_MapHud_timeLeft_DisplayTimer = Instance.AddTimer(1.0f, HUD_UpdateHudMapHud_timeLeft, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
            }, TimerFlags.STOP_ON_MAPCHANGE);
        }

        private static void HUD_UpdateHudMapHud_timeLeft()
        {
            Hud_timeLeft -= 1.0f;

            if (Hud_timeLeft <= 0)
            {
                ClearHudMapHud_timeLeftDisplay();
                return;
            }

            // 每20秒打印一次
            if ((int)Hud_timeLeft % 20 == 0)
            {
                Server.PrintToChatAll($"{HUD_GetMapChangeHud_timeLeft(true)}");
            }

            // Update HUD text for each player
            foreach (var (player, hud) in Hud_playerHudMap)
            {
                if (player == null || !player.IsValid || player.IsBot)
                {
                    continue;
                }
                if (player.PlayerPawn == null || player.PlayerPawn.Value == null)
                {
                    continue;
                }
                if (hud == null || !hud.IsValid)
                {
                    continue;
                }

                hud.AcceptInput("SetMessage", null, null, HUD_GetMapChangeHud_timeLeft(false));

            }


        }


        private static string HUD_GetMapChangeHud_timeLeft(bool IsChatMessage = false)
        {
            int minutes = (int)(Hud_timeLeft / 60);
            int seconds = (int)(Hud_timeLeft % 60);

            if (IsChatMessage)
            {
                // 判断如果分钟数大于3，显示"地图即将投票"
                if (Hud_timeLeft > 180)
                {
                    return $" {ChatColors.Yellow}[注意]:地图投票剩余(Time until map change:) {ChatColors.Red} {minutes:D2}:{seconds:D2} 系统将在第 3 分钟发起投票";
                }
                else
                {
                    return $" {ChatColors.Yellow}[注意]:地图更换剩余(Time until map change:) {ChatColors.Red} {minutes:D2}:{seconds:D2}";
                }
            }

            // 默认返回地图更换时间
            return $"地图更换剩余\nTime until map change:\n{minutes:D2}:{seconds:D2}";
        }


        public static void ClearHudMapHud_timeLeftDisplay()
        {
            // Remove all HUDs
            foreach (var (player, hud) in Hud_playerHudMap.ToList())
            {
                if (hud != null && hud.IsValid)
                {
                    hud.AddEntityIOEvent("Kill", null, null, "", 0.3f);
                }
                Hud_playerHudMap.Remove(player);
            }
            Hud_playerHudMap.Clear();

            foreach (var (_, anchor) in Hud_playerAnchorMap.ToList())
            {
                if (anchor != null && anchor.IsValid)
                {
                    anchor.Remove();
                }
            }
            Hud_playerAnchorMap.Clear();

            // Stop the timer
            Hud_MapHud_timeLeft_DisplayTimer?.Kill();
            Hud_MapHud_timeLeft_DisplayTimer = null;
            Hud_timeLeft = 0.0f;
            bHud_IsDisplay = false;
        }

        public static CPointWorldText? CreateHud(CCSPlayerController player, string text, int size = 100, Color? color = null, string font = "", float shiftX = 0f, float shiftY = 0f)
        {
            // Check if player is null or invalid
            if (player == null || !player.IsValid)
            {
                return null;
            }


            // Check if PlayerPawn is null
            if (player.PlayerPawn == null || player.PlayerPawn.Value == null)
            {
                return null;
            }

            if (!player.PlayerPawn.Value.IsValid)
            {
                return null;
            }

            CCSPlayerPawn pawn = player.PlayerPawn.Value;

            CPointOrient? anchor = EnsureHudAnchor(player, pawn);
            if (anchor == null || !anchor.IsValid)
            {
                return null;
            }

            CPointWorldText? worldText = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
            if (worldText == null)
                return null;

            worldText.MessageText = text;
            worldText.Enabled = true;
            worldText.FontSize = size;
            worldText.Fullbright = true;
            worldText.Color = color ?? Color.Aquamarine;
            worldText.WorldUnitsPerPx = 0.01f;
            worldText.FontName = font;
            worldText.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_LEFT;
            worldText.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_TOP;
            worldText.ReorientMode = PointWorldTextReorientMode_t.POINT_WORLD_TEXT_REORIENT_NONE;

            QAngle eyeAngles = pawn.EyeAngles;
            Vector forward = new(), right = new(), up = new();
            NativeAPI.AngleVectors(eyeAngles.Handle, forward.Handle, right.Handle, up.Handle);

            Vector offset = new();
            offset += forward * 7;
            offset += right * shiftX;
            offset += up * shiftY;
            QAngle angles = new()
            {
                Y = eyeAngles.Y + 270,
                Z = 90 - eyeAngles.X,
                X = 0
            };

            worldText.DispatchSpawn();
            worldText.Teleport(pawn.AbsOrigin! + offset + new Vector(0, 0, pawn.ViewOffset.Z), angles, null);
            worldText.AcceptInput("SetParent", anchor, null, "!activator");

            return worldText;
        }

        private static CPointOrient? EnsureHudAnchor(CCSPlayerController player, CCSPlayerPawn pawn)
        {
            if (Hud_playerAnchorMap.TryGetValue(player.Slot, out CPointOrient? existingAnchor) && existingAnchor.IsValid)
            {
                return existingAnchor;
            }

            Hud_playerAnchorMap.Remove(player.Slot);

            CPointOrient? anchor = Utilities.CreateEntityByName<CPointOrient>("point_orient");
            if (anchor == null || !anchor.IsValid)
            {
                return null;
            }

            anchor.Active = true;
            anchor.GoalDirection = PointOrientGoalDirectionType_t.eEyesForward;
            anchor.DispatchSpawn();

            Vector eyePosition = pawn.AbsOrigin! + new Vector(0, 0, pawn.ViewOffset.Z);
            anchor.Teleport(eyePosition, null, null);
            anchor.AcceptInput("SetParent", pawn, null, "!activator");
            anchor.AcceptInput("SetTarget", pawn, null, "!activator");

            Hud_playerAnchorMap[player.Slot] = anchor;
            return anchor;
        }

    }

}
