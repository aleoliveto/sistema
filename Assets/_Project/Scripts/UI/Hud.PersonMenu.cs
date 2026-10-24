using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// Click one of your people (yourself included) and everything you can have them do comes up
    /// next to them: eat, sleep, see to their wounds, come over, change job, choose where they live...
    /// </summary>
    public sealed partial class Hud
    {
        VisualElement personMenu, menuAnchor;

        public void ShowPersonMenu(Pawn p, Vector2 screenPos)
        {
            if (personMenu == null)
            {
                personMenu = Ui.Box("layer").Into(modalLayer);
                personMenu.RegisterCallback<PointerDownEvent>(e => { if (e.target == personMenu) personMenu.Show(false); });
            }
            personMenu.Clear();
            personMenu.Show(true);
            personMenu.pickingMode = PickingMode.Position;
            var list = Ui.Box("panel", "role-menu", "person-menu").Into(personMenu);
            var at = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            list.style.left = at.x + 14;
            list.style.top = at.y - 10;
            // Where the job and house dropdowns open from, once this menu has closed.
            if (menuAnchor == null)
            {
                menuAnchor = Ui.Box().Into(modalLayer);
                menuAnchor.style.position = Position.Absolute;
                menuAnchor.style.width = 1;
                menuAnchor.style.height = 1;
                menuAnchor.pickingMode = PickingMode.Ignore;
            }
            menuAnchor.style.left = at.x + 14;
            menuAnchor.style.top = at.y;
            Ui.Text(p.Controlled ? "You" : p.DisplayName, list, "bold", "gold");
            int n = 0;
            foreach (var (label, act, ok, hint) in ActionsFor(p))
            {
                var b = Ui.Btn(label, () => { personMenu.Show(false); act(); RefreshAll(); }, list, "btn--small", "role-menu__item");
                b.SetEnabled(ok);
                if (!string.IsNullOrEmpty(hint)) Tip(b, () => hint);
                n++;
            }
            if (n == 0) Ui.Text("Nothing to do here.", list, "small", "muted");
        }

        void ToggleFollow(Pawn p)
        {
            p.Following = !p.Following;
            p.EndCurrentJob(game);
            game.Log.Add(p.Following ? $"{p.DisplayName} falls in behind you." : $"{p.DisplayName} goes back to work.", MessageKind.Info);
        }

        /// <summary>The nearest of the family who can carry someone: not the boss, not someone down himself.</summary>
        Pawn Carrier(Pawn patient)
        {
            Pawn best = null;
            float bestD = float.MaxValue;
            foreach (var c in game.Pawns.Crew)
            {
                if (c == patient || c.Controlled || !c.CanAct || c.CurrentJob is CarryHomeJob) continue;
                float d = (c.Position - patient.Position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        List<(string, System.Action, bool, string)> ActionsFor(Pawn p)
        {
            var list = new List<(string, System.Action, bool, string)>();
            var s = game.Stock;
            bool me = p.Controlled;
            bool canAct = p.CanAct;

            // Their own house's pantry and beds first; the beds are whichever nobody is lying in.
            Vector2Int? pantry = JobGiver.FindHome(p, game, BuildingDefs.Pantry, out var ownPantry) ? ownPantry : (Vector2Int?)null;
            list.Add(($"Eat ({s.Meals} meals at home)", () => { p.LastInputTick = -1000000; p.ForceJob(new EatJob(p, pantry.Value, atShop: false), game); },
                canAct && s.Meals > 0 && pantry.HasValue && p.Needs.Food < 0.95f, s.Meals <= 0 ? "No meals at home: cook groceries, or buy a pizza." : null));
            var bed = Housing.FreeBed(p, game);
            list.Add(("Sleep", () => { p.LastInputTick = -1000000; p.ForceJob(new SleepJob(p, bed, null), game); },
                canAct && bed.HasValue && p.Needs.Rest < 0.95f,
                bed.HasValue ? null : Housing.BedsIn(Housing.HomeOf(p, game), game).Count > 0 ? "Every bed at home is taken." : "No bed at home: buy one at the furniture shop."));
            if (!p.Health.Healthy)
            {
                // Wounds are seen to at home: carried there first if they're down in the street.
                bool atHome = game.PlayerBase.Contains(p.Cell) || game.Properties.IsOwnedHome(p.Cell);
                bool tooBad = Healing.TooHurtForHome(p);
                list.Add(($"Treat the wounds ({s.Medicine} medicine)", () => Healing.WithMedicine(game, p),
                    s.Medicine > 0 && Healing.NeedsCare(p) && atHome && !tooBad,
                    tooBad ? "Too badly hurt for home: only the hospital (118) can fix it." : !atHome ? "Get them home first." : s.Medicine > 0 ? null : "No medicine at home: the farmacia sells it."));
                if (p.Health.Downed && !atHome)
                {
                    var carrier = Carrier(p);
                    list.Add(("Carry them home", () => { carrier.LastInputTick = -1000000; carrier.ForceJob(new CarryHomeJob(carrier, p), game); },
                        carrier != null, carrier != null ? $"{carrier.DisplayName} picks them up and takes them home, out of sight." : "Nobody from the family is free to carry them."));
                }
                if (p.Health.Downed && !game.Hospital.IsCalled(p))
                    list.Add(("Call 118", () => game.Hospital.Call(p, game, byPasserby: false), true,
                        "The ambulance takes them to the Ospedale. Doctors report gunshots: 6 times in 10 the police arrest them there."));
            }
            var home = Housing.Rooms(Housing.HomeOf(p, game), game);
            list.Add(("Go home", () => p.ForceJob(new GotoJob(p, Vector2Int.RoundToInt(home.center), "Going home"), game), canAct, null));

            if (me)
            {
                if (p.Armed && p.Ammo < p.MagSize)
                    list.Add(("Reload", () => { p.ReloadUntil = Time.unscaledTime + 1f; }, true, null));
                list.Add((p.Autopilot ? "Automatic movement: on (Z)" : "Automatic movement: off (Z)", ToggleAutoMovement, true, "When you leave him be, he looks after himself."));
            }
            else
            {
                var boss = game.Player;
                list.Add(("Call over", () => { if (boss != null) p.ForceJob(new GotoJob(p, boss.Cell, "Coming over"), game); }, canAct && boss != null, null));
                list.Add((p.Following ? "Stop following" : "Follow me", () => ToggleFollow(p), canAct && boss != null,
                    p.Following ? "Back to their job." : "They come along behind you and keep their job for later."));
                list.Add(($"Job: {RoleLabel(p.Role)} ▸", () => ShowRoleMenu(p, menuAnchor), canAct, null));
                list.Add(($"{HomeLabel(p).Replace(" ▾", "")} ▸", () => ShowHouseMenu(p, menuAnchor), true, null));
                list.Add((p.Armed ? $"Take the {p.GunStats.Name.ToLower()} back" : $"Give a gun ({s.AllGuns} in the stash)", () => ToggleArmed(p), p.Armed || s.AllGuns > 0, null));
            }
            return list;
        }
    }
}
