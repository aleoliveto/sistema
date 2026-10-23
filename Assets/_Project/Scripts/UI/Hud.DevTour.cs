using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using OSistema.Core;
using OSistema.Crime;
using OSistema.World;
using UnityEngine;

namespace OSistema.UI
{
    /// <summary>Developer switch "uitour" in dev_autoplay: opens every screen in turn and saves a screenshot of each
    /// (persistentDataPath/uitour), to check the layouts without clicking through them by hand.</summary>
    public sealed partial class Hud
    {
        void StartUiTour() => StartCoroutine(UiTour());

        void StartInteriorTour() => StartCoroutine(InteriorTour());

        void StartWalkShots() => StartCoroutine(WalkShots());

        /// <summary>Developer switch "walkshots": close-up frames of someone walking (persistentDataPath/walk), to check the arms.</summary>
        IEnumerator WalkShots()
        {
            string dir = Path.Combine(Application.persistentDataPath, "walk");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            Application.runInBackground = true;
            var fog = FindAnyObjectByType<OSistema.Views.FogOfWar>();
            if (fog != null) fog.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(3f);
            var cam = Camera.main;
            for (int tries = 0; tries < 60; tries++)
            {
                // Whoever moves furthest in a moment is walking.
                var before = new Dictionary<OSistema.Pawns.Pawn, Vector2>();
                foreach (var p in game.Pawns.Crew) if (p.IsPresent) before[p] = p.Position;
                yield return new WaitForSecondsRealtime(0.3f);
                OSistema.Pawns.Pawn walker = null;
                float best = 0.25f;
                foreach (var kv in before)
                {
                    float d = (kv.Key.Position - kv.Value).magnitude;
                    if (d > best && !kv.Key.Riding) { best = d; walker = kv.Key; }
                }
                if (walker != null)
                {
                    controller.StopFollowing();
                    cam.orthographicSize = 2.2f;
                    for (int i = 0; i < 8; i++)
                    {
                        cam.transform.position = new Vector3(walker.Position.x, walker.Position.y, cam.transform.position.z);
                        yield return null;
                        ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"walk_{i:00}.png"));
                        yield return new WaitForSecondsRealtime(0.11f);
                    }
                    Debug.Log("[walkshots] done");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Debug.Log("[walkshots] nobody walking");
        }

        /// <summary>
        /// Developer switch "interiors": roofs off, fog lifted, and a screenshot of the family basso,
        /// a few homes and one shop of each trade (persistentDataPath/interiors); then one shop burned,
        /// squeezed and made a front, to see the fortunes.
        /// </summary>
        IEnumerator InteriorTour()
        {
            string dir = Path.Combine(Application.persistentDataPath, "interiors");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            Application.runInBackground = true;
            OSistema.Views.RoofView.DevOpenAll = true;
            var fog = FindAnyObjectByType<OSistema.Views.FogOfWar>();
            if (fog != null) fog.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(2f);
            var cam = Camera.main;
            int n = 0;
            IEnumerator Shot(Property p, string name)
            {
                controller.StopFollowing();
                var r = p.Rect;
                cam.orthographicSize = Mathf.Max(r.height * 0.5f + 1f, (r.width * 0.5f + 1f) / cam.aspect);
                cam.transform.position = new Vector3(r.center.x, r.center.y, cam.transform.position.z);
                yield return new WaitForSecondsRealtime(0.7f);
                cam.transform.position = new Vector3(r.center.x, r.center.y, cam.transform.position.z);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{n++:00}_{name}.png"));
                yield return new WaitForSecondsRealtime(0.4f);
            }
            var home = game.Properties.At(Vector2Int.RoundToInt(game.PlayerBase.center));
            if (home != null) yield return Shot(home, "family");
            int homes = 0;
            foreach (var p in game.Properties.All)
            {
                if (homes >= 4 || p.Kind != PropertyKind.Home || p.OwnedByPlayer || p.Interior.width * p.Interior.height > 100) continue;
                if (homes > 0 && (p.Rect.center - (Vector2)game.PlayerBase.center).sqrMagnitude > 40 * 40) continue;
                homes++;
                yield return Shot(p, $"home_{homes}");
            }
            var seen = new HashSet<ShopKind>();
            Business test = null;
            foreach (var b in game.Org.Businesses)
            {
                if (!seen.Add(b.Kind)) continue;
                var p = game.Properties.At(b.Cell);
                if (p == null) continue;
                test ??= b;
                yield return Shot(p, $"shop_{b.Kind}");
            }
            if (test != null)
            {
                var p = game.Properties.At(test.Cell);
                foreach (var (status, label) in new[] { (BusinessStatus.Closed, "burned"), (BusinessStatus.Paying, "squeezed"), (BusinessStatus.Owned, "front") })
                {
                    test.Status = status;
                    Interiors.Hourly(game);
                    yield return Shot(p, $"fortune_{test.Kind}_{label}");
                }
            }
            Debug.Log($"[interiors] DONE {n} screenshots in {dir}");
        }

        IEnumerator UiTour()
        {
            string dir = Path.Combine(Application.persistentDataPath, "uitour");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            Application.runInBackground = true;
            yield return new WaitForSecondsRealtime(3f);

            var steps = new List<(string name, Action open, Action close)>();
            var seen = new HashSet<ShopKind>();
            foreach (var b in game.Org.Businesses)
            {
                if (!seen.Add(b.Kind)) continue;
                var shop = b;
                steps.Add(($"shop_{shop.Kind}", () => OpenShop(shop), CloseShop));
            }
            foreach (BizTab t in Enum.GetValues(typeof(BizTab)))
            {
                var tab = t;
                steps.Add(($"famiglia_{tab}", () => { if (openWindow != Window.Business) ToggleWindow(Window.Business); OpenBizTab(tab); }, () => { }));
            }
            steps.Add(("famiglia_person", (System.Action)(() => { var who = System.Linq.Enumerable.FirstOrDefault(game.Pawns.Crew, c => !c.Dead && !c.Controlled) ?? game.Player; if (who != null) OpenFamigliaPerson(who); }), () => { }));
            steps.Add(("famiglia_close", () => ToggleWindow(Window.Business), () => { }));
            steps.Add(("taccuino", () => ToggleWindow(Window.Jobs), () => ToggleWindow(Window.Jobs)));
            steps.Add(("phone", () => ToggleWindow(Window.Phone), () => ToggleWindow(Window.Phone)));
            steps.Add(("help", () => ToggleWindow(Window.Help), () => ToggleWindow(Window.Help)));
            steps.Add(("map", ToggleMap, ToggleMap));
            foreach (var t in new[] { BuildCat.Tutto, BuildCat.Vita, BuildCat.Magazzino })
            {
                var tab = t;
                steps.Add(($"build_{tab}", () => { buildMode = true; buildCat = tab; RefreshDock(); }, () => { }));
            }
            steps.Add(("build_close", ToggleBuildMode, () => { }));
            steps.Add(("pause", () => OpenMenu(Menu.Pause), () => { }));
            steps.Add(("settings", () => OpenMenu(Menu.Settings), CloseMenu));
            steps.Add(("event_long", () =>
            {
                var e = new OSistema.Story.GameEvent
                {
                    Id = "uitour", Title = "A long scene", Neapolitan = "Chi va chiano va sano e va luntano.", English = "Who goes slowly goes safely and far.",
                    Body = string.Join(" ", System.Linq.Enumerable.Repeat("A long body of text, to see the card scroll instead of squashing.", 8)),
                };
                for (int i = 0; i < 8; i++) e.Option($"Option {i + 1}", "What happens if you pick it, in a line or two of hint text.", _ => { });
                game.Events.Raise(e);
            }, () => { if (game.Events.Current != null) game.Events.Choose(0, game); }));
            steps.Add(("inspector", () => { controller.Selected.Clear(); if (game.Player != null) controller.Selected.Add(game.Player); RefreshAll(); }, () => { controller.Selected.Clear(); RefreshAll(); }));
            steps.Add(("bagging", OpenBagging, CloseMinigame));
            steps.Add(("rolling", OpenRolling, CloseMinigame));
            steps.Add(("paper", () => { if (game.Newspaper.Latest != null) ShowPaper(game.Newspaper.Latest, pause: false); }, ClosePaper));
            steps.Add(("creator", () => OpenMenu(Menu.Creator), CloseMenu));

            int n = 0;
            foreach (var (name, open, close) in steps)
            {
                try { open(); }
                catch (Exception e) { Debug.LogError($"[uitour] {name} failed to open: {e}"); continue; }
                yield return new WaitForSecondsRealtime(0.8f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{n++:00}_{name}.png"));
                yield return new WaitForSecondsRealtime(0.4f);
                try { close(); }
                catch (Exception e) { Debug.LogError($"[uitour] {name} failed to close: {e}"); }
            }
            Debug.Log($"[uitour] DONE {n} screenshots in {dir}");
        }

        // ---------- "checks": eating and E on lavoretto steps, logged as [devcheck] ----------

        void StartDevChecks() => StartCoroutine(DevChecks());

        IEnumerator DevChecks()
        {
            Application.runInBackground = true;
            yield return new WaitForSecondsRealtime(2f);
            var s = game.Stock;
            var p = game.Player;

            // 1. Eating: no meals cooked, groceries in. A hungry crewman should cook, then eat; so should the boss left alone.
            var guy = game.Org.Recruit(game, out var err, free: true);
            Debug.Log($"[devcheck] recruit: {(guy != null ? guy.Nickname : err)}");
            s.Meals = 0; s.Groceries = 6; s.Cash = 200;
            if (guy != null) guy.Needs.Food = 0.2f;
            game.PlayerSelfCare = true;
            p.Autopilot = true;
            p.LastInputTick = -1000000;
            p.Needs.Food = 0.1f;
            game.Ticks.SetSpeed(2);
            float guyStart = guy?.Needs.Food ?? 0f, bossStart = p.Needs.Food;
            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForSecondsRealtime(2f);
                Debug.Log($"[devcheck] eat t={i * 2}s meals={s.Meals} groceries={s.Groceries} | {guy?.Nickname}: {guy?.CurrentJob?.Label} food={guy?.Needs.Food:0.00} | boss: {p.CurrentJob?.Label} food={p.Needs.Food:0.00}");
            }
            Debug.Log($"[devcheck] EAT RESULT crewman food {guyStart:0.00}->{guy?.Needs.Food:0.00}, boss food {bossStart:0.00}->{p.Needs.Food:0.00} " +
                      ((guy == null || guy.Needs.Food > guyStart + 0.3f) && p.Needs.Food > bossStart + 0.3f ? "PASS" : "FAIL"));
            p.Autopilot = false;
            game.PlayerSelfCare = false;
            game.Ticks.SetSpeed(1);

            // 2. A delivery: E standing on the pick-up door, then on the drop-off door.
            var doors = new List<Vector2Int>();
            foreach (var d in game.Map.PositionsOf(BuildingDefs.Door))
                if (!game.PlayerBase.Contains(d) && !game.Properties.IsOwnedHome(d) && (d - p.Cell).magnitude < 60) doors.Add(d);
            if (doors.Count >= 2)
            {
                var c = new OSistema.Crime.Contract
                {
                    Id = 9001, Kind = OSistema.Crime.ContractKind.Delivery, Giver = "devcheck", Title = "Deliver a package", Reward = 10,
                    From = doors[0], To = doors[doors.Count - 1], ExpiresTick = game.Ticks.TicksGame + Core.GameClock.TicksPerDay,
                };
                game.Contracts.All.Add(c);
                p.EndCurrentJob(game);
                game.Contracts.Assign(c, p, game, out var aerr);
                yield return PressE(p, c.From.Value, "delivery pick-up (on the door)");
                for (int i = 0; i < 20 && c.Stage < 2; i++) yield return new WaitForSecondsRealtime(0.5f);
                Debug.Log($"[devcheck] delivery after pick-up: stage {c.Stage} carrying '{p.Carrying}'");
                yield return PressE(p, c.To, "delivery drop-off (on the door)");
                for (int i = 0; i < 20 && c.State == OSistema.Crime.ContractState.Active; i++) yield return new WaitForSecondsRealtime(0.5f);
                Debug.Log($"[devcheck] DELIVERY RESULT {c.State} {(c.State == OSistema.Crime.ContractState.Done ? "PASS" : "FAIL")} ({c.Outcome})");
            }

            // 3. A cigarette run brought home: E inside the basso.
            var run = game.Contracts.AddShipment(game, OSistema.Crime.ContractKind.CigaretteRun, 2);
            if (run != null)
            {
                p.EndCurrentJob(game);
                game.Contracts.Assign(run, p, game, out _);
                run.Stage = 2;
                p.Carrying = "cases of cigarettes";
                int before = s.Cigarettes;
                Vector2Int inside = Vector2Int.RoundToInt(game.PlayerBase.center);
                foreach (var c in GridUtil.CellsAround(inside, 3))
                    if (game.PlayerBase.Contains(c) && game.Map.IsPassable(c) && game.Map.GetBuilding(c) == null) { inside = c; break; }
                yield return PressE(p, inside, "cases into the basso (inside)");
                for (int i = 0; i < 20 && run.State == OSistema.Crime.ContractState.Active; i++) yield return new WaitForSecondsRealtime(0.5f);
                Debug.Log($"[devcheck] SHIPMENT RESULT {run.State}, cigarettes {before}->{s.Cigarettes} {(run.State == OSistema.Crime.ContractState.Done && s.Cigarettes == before + 2 ? "PASS" : "FAIL")}");
            }
            Debug.Log("[devcheck] DONE");
        }

        IEnumerator PressE(OSistema.Pawns.Pawn p, Vector2Int cell, string what)
        {
            p.Teleport(cell);
            p.Velocity = Vector2.zero;
            yield return new WaitForSecondsRealtime(0.3f);
            var action = controller.Interaction();
            Debug.Log($"[devcheck] E at {what}: '{action?.Label ?? "(nothing)"}'");
            action?.Act();
        }
    }
}
