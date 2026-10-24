using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>
    /// The runner (cavallino): hangs about near the piazza; when a pusher is running out he fetches a few doses
    /// from the nascondiglio and hands them over; when the nascondiglio runs low he restocks it from the basso.
    /// The pusher never holds much, so an arrest costs little.
    /// </summary>
    public sealed class RunnerJob : Job
    {
        enum Step { Decide, ToStash, ToPusher, ToBasso, ToNascondiglio, Hang }

        readonly DrugSquare square;
        Step step = Step.Decide;
        Walker walker;
        Pawn pusher;
        Property stash;
        int wait;

        public RunnerJob(Pawn pawn, DrugSquare square) : base(pawn) => this.square = square;

        public override bool IsWork => true;

        public override string Label => step switch
        {
            Step.ToStash => $"Fetching doses from {StashName}",
            Step.ToPusher => pusher != null ? $"Bringing doses to {pusher.Nickname}" : "Bringing doses to the piazza",
            Step.ToBasso => "Picking up product at the basso",
            Step.ToNascondiglio => $"Restocking {StashName}",
            _ => "Runner, waiting near the piazza",
        };

        string StashName => stash != null ? stash.Name : "the basso";

        public override bool Tick(Game game)
        {
            var org = game.Org;
            if (!org.Squares.Contains(square) || !square.IsOpenAt(game.Clock.Hour) || !JobGiver.OnShift(Pawn, game))
            {
                // Off shift: what he carries goes back where it came from, if he's passing; otherwise he keeps it for tomorrow.
                return true;
            }
            switch (step)
            {
                case Step.Decide: Decide(game); return false;
                case Step.Hang:
                    if (walker != null && walker.Tick(Pawn, game) == WalkStatus.Walking) return false;
                    walker = null;
                    if (--wait <= 0) step = Step.Decide;
                    return false;
                case Step.ToPusher:
                    if (pusher == null || !pusher.CanAct) { step = Step.Decide; return false; }
                    if (walker == null || (walker.Target - pusher.Cell).sqrMagnitude > 2) walker = new Walker(pusher.Cell, adjacent: true) { Flexible = true };
                    if ((pusher.Position - Pawn.Position).sqrMagnitude < 1.6f * 1.6f)
                    {
                        int give = Mathf.Min(Pawn.PocketDoses, Organization.PusherWithRunner - pusher.PocketDoses);
                        if (give > 0)
                        {
                            Pawn.PocketDoses -= give;
                            pusher.PocketDoses += give;
                            if (game.Rng.NextDouble() < 0.3) Pawn.Say("sale");
                        }
                        step = Step.Decide;
                        return false;
                    }
                    if (walker.Tick(Pawn, game) == WalkStatus.Failed) { walker = null; step = Step.Decide; }
                    return false;
                default:
                    var status = walker.Tick(Pawn, game);
                    if (status == WalkStatus.Walking) return false;
                    walker = null;
                    if (status == WalkStatus.Failed) { step = Step.Decide; return false; }
                    Arrive(game);
                    return false;
            }
        }

        void Arrive(Game game)
        {
            var org = game.Org;
            switch (step)
            {
                case Step.ToStash:
                    // A few for the pushers; the rest stays hidden.
                    Pawn.PocketDoses += org.TakeDoses(stash, Organization.RunnerLoad - Pawn.PocketDoses, game);
                    break;
                case Step.ToBasso:
                    Pawn.PocketDoses += org.TakeDoses(null, Organization.RestockLoad, game);
                    if (stash != null && Pawn.PocketDoses > 0)
                    {
                        step = Step.ToNascondiglio;
                        walker = new Walker(org.StashCellOf(stash, game), adjacent: true);
                        return;
                    }
                    break;
                case Step.ToNascondiglio:
                    // Keep a pocketful for the pushers, hide the rest.
                    int keep = Mathf.Min(Pawn.PocketDoses, Organization.RunnerLoad);
                    org.PutDoses(stash, Pawn.PocketDoses - keep, game);
                    Pawn.PocketDoses = keep;
                    break;
            }
            step = Step.Decide;
        }

        void Decide(Game game)
        {
            var org = game.Org;
            stash = org.StashFor(square, game);
            // A pusher (or the capo serving) at the piazza running out.
            pusher = null;
            foreach (var p in game.Pawns.Crew)
                if (p.CanAct && p.CurrentJob is PostJob post && post.Square == square && post.AtPost &&
                    (post.Role == CrewRole.Pusher || post.Role == CrewRole.Capo) && p.PocketDoses < Organization.PusherLow)
                { pusher = p; break; }

            if (pusher != null && Pawn.PocketDoses > 0) { step = Step.ToPusher; walker = null; return; }
            if (pusher != null && org.DosesIn(stash, game) > 0) { Go(Step.ToStash, org.StashCellOf(stash, game)); return; }
            // The nascondiglio is running low and there's product at home: a restocking run.
            if (stash != null && stash.StashDoses < Organization.RestockBelow && game.Stock.Product > 0)
            {
                Go(Step.ToBasso, org.StashCell(game));
                return;
            }
            // Ready for the next one: a pocketful on him.
            if (Pawn.PocketDoses < Organization.PusherWithRunner && org.DosesIn(stash, game) > 0)
            {
                Go(Step.ToStash, org.StashCellOf(stash, game));
                return;
            }
            // Hang about a few steps from the piazza, not on it.
            step = Step.Hang;
            wait = 120;
            if ((Pawn.Position - Mover.CellCenter(square.Cell)).sqrMagnitude > 6f * 6f)
            {
                foreach (var c in GridUtil.CellsAround(square.Cell, 5))
                    if (GridUtil.Manhattan(c, square.Cell) >= 3 && game.Map.IsPassable(c) && game.Map.GetBuilding(c) == null &&
                        game.Map.GetTerrain(c.x, c.y) != TerrainType.Floor)
                    { walker = new Walker(c, adjacent: false); break; }
            }
        }

        void Go(Step s, Vector2Int to)
        {
            step = s;
            walker = new Walker(to, adjacent: true);
        }
    }
}
