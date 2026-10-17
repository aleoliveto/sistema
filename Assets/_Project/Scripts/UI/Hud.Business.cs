using System;
using OSistema.Crime;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.UI
{
    /// <summary>The business panel (B): treasury, crew roles and wages, supplier, piazze and pizzo.</summary>
    public sealed partial class Hud
    {
        bool showBusiness;
        Vector2 businessScroll;

        static Rect BusinessPanel => new Rect(Width - 520, TopBarHeight + 8, 512, Height - TopBarHeight - BottomBarHeight - 16);

        static string RoleLabel(CrewRole role) => role switch
        {
            CrewRole.Pusher => "Pusher",
            CrewRole.Lookout => "Lookout (vedetta)",
            CrewRole.Collector => "Collector (pizzo)",
            _ => "General",
        };

        static void CycleRole(Pawn p)
        {
            var roles = (CrewRole[])Enum.GetValues(typeof(CrewRole));
            p.Role = roles[(Array.IndexOf(roles, p.Role) + 1) % roles.Length];
        }

        void DrawBusinessPanel()
        {
            var r = BusinessPanel;
            GUI.Box(r, GUIContent.none);
            GUILayout.BeginArea(new Rect(r.x + 8, r.y + 6, r.width - 16, r.height - 12));
            businessScroll = GUILayout.BeginScrollView(businessScroll);

            DrawTreasury();
            GUILayout.Space(8);
            DrawCrew();
            GUILayout.Space(8);
            DrawSupplier();
            GUILayout.Space(8);
            DrawSquares();
            GUILayout.Space(8);
            DrawBusinesses();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawTreasury()
        {
            var org = game.Org;
            GUILayout.Label("'O Sistema  ·  the business", headerStyle);
            GUILayout.Label($"Treasury (bacinella): <b>€{game.Stock.Cash}</b>    Respect: {org.Respect:0}/100", textStyle);
            GUILayout.Label($"Weekly mesata and family support: €{org.WeeklyWageBill(game)} · paid Sunday at 12:00", smallStyle);
        }

        void DrawCrew()
        {
            GUILayout.Label("Crew", headerStyle);
            long now = game.Ticks.TicksGame;
            foreach (var p in game.Pawns.All)
            {
                if (p.Dead) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{p.Nickname}</b>", textStyle, GUILayout.Width(120));
                if (GUILayout.Button(RoleLabel(p.Role), smallButtonStyle, GUILayout.Width(120))) CycleRole(p);
                if (GUILayout.Button("-", smallButtonStyle, GUILayout.Width(20))) p.Wage = Mathf.Max(0, p.Wage - 10);
                GUILayout.Label($"€{p.Wage}", smallStyle, GUILayout.Width(38));
                if (GUILayout.Button("+", smallButtonStyle, GUILayout.Width(20))) p.Wage = Mathf.Min(1000, p.Wage + 10);
                GUILayout.Label($"Loyal {p.Loyalty:0}", smallStyle, GUILayout.Width(56));
                GUILayout.Label(CrewStatus(p, now), smallStyle);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Recruit a guaglione (€{Prices.RecruitBonus})", GUILayout.Width(220)))
            {
                if (game.Org.Recruit(game, out var error) == null) game.Log.Add(error, OSistema.Core.MessageKind.Bad);
            }
            GUILayout.EndHorizontal();

            bool hasCollector = false;
            foreach (var p in game.Pawns.All) if (p.IsPresent && p.Role == CrewRole.Collector) hasCollector = true;
            if (!hasCollector) GUILayout.Label("<color=#ff7a70>Nobody is collecting pizzo. Give someone the Collector role.</color>", smallStyle);
        }

        string CrewStatus(Pawn p, long now)
        {
            if (p.InJail) return $"<color=#ff7a70>In Poggioreale ({(p.JailedUntil - now) / OSistema.Core.GameClock.TicksPerDay + 1}d)</color>";
            if (p.Health.Downed) return "<color=#ff7a70>Downed</color>";
            if (p.InMentalBreak(now)) return "<color=#ff7a70>Breakdown</color>";
            if (p.IsLyingLow(now)) return "Lying low";
            return p.CurrentJob != null ? p.CurrentJob.Label : "";
        }

        void DrawSupplier()
        {
            var org = game.Org;
            GUILayout.Label("Supplier", headerStyle);
            GUILayout.Label($"Product at the basso: <b>{game.Stock.Product}</b> units · wholesale €{Prices.ProductWholesale}, street €{Prices.ProductStreet}", smallStyle);
            if (org.PendingOrderUnits > 0)
            {
                GUILayout.Label($"Order of {org.PendingOrderUnits} units arriving day {org.PendingOrderDay} at 08:00.", smallStyle);
                return;
            }
            GUILayout.BeginHorizontal();
            foreach (int units in new[] { 20, 50, 100 })
            {
                if (GUILayout.Button($"Order {units} (€{units * Prices.ProductWholesale})", GUILayout.Width(140)) &&
                    !org.OrderProduct(units, game, out var error))
                    game.Log.Add(error, OSistema.Core.MessageKind.Bad);
            }
            GUILayout.EndHorizontal();
        }

        void DrawSquares()
        {
            var org = game.Org;
            GUILayout.Label("Piazze di spaccio", headerStyle);
            if (org.Squares.Count == 0)
            {
                GUILayout.Label("None yet. Use the Piazza tool on the bottom bar and click a sidewalk.", smallStyle);
                return;
            }
            foreach (var sq in org.Squares)
            {
                var zone = game.Territory.Zones[sq.ZoneId];
                int pushers = 0, lookouts = 0;
                foreach (var p in game.Pawns.All)
                {
                    if (!p.IsPresent) continue;
                    if (p.Role == CrewRole.Pusher) pushers++;
                    if (p.Role == CrewRole.Lookout) lookouts++;
                }
                string raid = game.Police.IsRaiding(sq) ? "  <color=#ff7a70>RAID</color>" : "";
                GUILayout.Label($"<b>{zone.Name}</b>{raid}  ·  Heat {zone.Heat:0}  ·  Demand {zone.Demand:0.0}", textStyle);
                GUILayout.Label($"Today: {sq.SalesToday} sold (€{sq.RevenueToday}), {sq.LostToday} customers lost  ·  open {DrugSquare.OpenHour:00}:00–{DrugSquare.CloseHour:00}:00", smallStyle);
                if (pushers == 0) GUILayout.Label("<color=#ff7a70>No pushers assigned.</color>", smallStyle);
                if (lookouts == 0) GUILayout.Label("<color=#ffb070>No lookouts: the police will walk right up to the spot.</color>", smallStyle);
            }
        }

        void DrawBusinesses()
        {
            var org = game.Org;
            GUILayout.Label("Pizzo", headerStyle);
            int weekly = 0;
            foreach (var b in org.Businesses) if (b.Status == BusinessStatus.Paying) weekly += b.WeeklyPizzo;
            GUILayout.Label($"Collected weekly from paying businesses: €{weekly}", smallStyle);

            foreach (var b in org.Businesses)
            {
                var zone = game.Territory.ZoneAt(b.Cell);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{b.Name}</b> <color=#aaaaaa>{zone?.Name}</color>", smallStyle, GUILayout.Width(250));
                GUILayout.Label(b.StatusLabel, smallStyle, GUILayout.Width(130));
                if ((b.Status == BusinessStatus.Free || b.Status == BusinessStatus.Refusing) &&
                    GUILayout.Button(b.Status == BusinessStatus.Free ? $"Impose €{b.WeeklyPizzo}" : "Try again", smallButtonStyle, GUILayout.Width(90)))
                    org.Target(b);
                GUILayout.EndHorizontal();
            }
        }

        // ---------- World labels for the crime layer ----------

        void DrawSquareLabels()
        {
            float unit = GuiUnitsPerWorldUnit;
            foreach (var sq in game.Org.Squares)
            {
                if (!WorldToGui(new Vector2(sq.Cell.x + 0.5f, sq.Cell.y + 0.5f), out var gui)) continue;
                var zone = game.Territory.Zones[sq.ZoneId];
                shopStyle.normal.textColor = new Color(0.9f, 0.5f, 1f);
                ShadowLabel(new Rect(gui.x - 90, gui.y + unit * 0.5f + 14, 180, 18), $"Piazza · Heat {zone.Heat:0}", shopStyle);
            }
            shopStyle.normal.textColor = Gold;
        }

        void DrawNpcLabels()
        {
            if (cam.orthographicSize > 30f) return;
            float unit = GuiUnitsPerWorldUnit;
            foreach (var npc in game.Npcs.All)
            {
                if (npc.Kind != NpcKind.Police || !WorldToGui(npc.Mover.Position, out var gui)) continue;
                nameStyle.normal.textColor = new Color(0.55f, 0.7f, 1f);
                ShadowLabel(new Rect(gui.x - 60, gui.y + unit * 0.45f, 120, 18), "Police", nameStyle);
            }
        }
    }
}
