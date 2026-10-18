using System.Collections.Generic;
using OSistema.Core;
using OSistema.Npcs;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// The State answering noise. A neighbour hears a door forced, or shots go on too long, and
    /// somebody calls 112: a car's worth of police heads for the spot, sirens going. If they find you
    /// there, and you're the one they're looking for, they come after you.
    /// Also: whose homes are locked to you, and the ones you've broken into.
    /// </summary>
    public sealed class Response
    {
        /// <summary>The player is wanted for something that just happened, until this tick.</summary>
        public long SuspectUntil = -1;
        public string SuspectWhy;

        long lastShootingCall = -100000;
        readonly Dictionary<int, long> forcedUntil = new Dictionary<int, long>();
        readonly HashSet<int> robbedToday = new HashSet<int>();
        int robbedDay = -1;

        public event System.Action<string> Sirens;

        // ---------- Doors ----------

        /// <summary>Somebody else's home (or a shop after closing): you can't just walk in.</summary>
        public bool IsLocked(Vector2Int cell, Game game)
        {
            if (game.PlayerBase.Contains(cell)) return false;
            var prop = game.Properties.At(cell);
            if (prop == null || prop.OwnedByPlayer || prop.Kind == PropertyKind.Civic) return false;
            if (prop.Kind == PropertyKind.Shop)
            {
                int h = game.Clock.Hour;
                if (h >= 8 && h < 21) return false;
            }
            if (forcedUntil.TryGetValue(prop.Id, out long until) && game.Ticks.TicksGame < until) return false;
            // A lavoretto that takes you inside: the door's open for you.
            var job = game.Player != null ? game.Contracts.ActiveFor(game.Player) : null;
            if (job != null && prop.Rect.Contains(Contracts.StepTarget(job))) return false;
            return true;
        }

        public bool IsForced(Property prop, Game game) => prop != null && forcedUntil.TryGetValue(prop.Id, out long until) && game.Ticks.TicksGame < until;

        /// <summary>Shoulder to the door. Noisy: at night the family is home, and someone may call the police.</summary>
        public void BreakIn(Property prop, Game game)
        {
            var p = game.Player;
            if (p == null) return;
            long now = game.Ticks.TicksGame;
            forcedUntil[prop.Id] = now + GameClock.TicksPerDay;
            int h = game.Clock.Hour;
            bool night = h >= 22 || h < 6;
            float heard = (prop.Kind == PropertyKind.Shop ? 0.4f : night ? 0.45f : 0.3f) - p.AwarenessSkill * 0.02f;
            Views.SoundManager.PlayAt(Views.AudioSynth.Punch, Pawns.Mover.CellCenter(prop.Door), 0.9f);
            game.Territory.AddHeat(prop.Door, 4f);
            if (game.Rng.NextDouble() < heard)
            {
                game.Log.Add(night ? "A light comes on upstairs. Someone's screaming for the police." : "A neighbour on the balcony saw you. She's already dialling.", MessageKind.Bad, prop.Door);
                Suspect(game, "Breaking and entering", GameClock.TicksPerHour);
                Call(prop.Door, game, 2, "A break-in has been reported. Police are on their way.");
                game.Org.AddEvidence(2f, game);
            }
            else game.Log.Add("The lock gives. Nobody seems to have heard.", MessageKind.Info, prop.Door);
        }

        /// <summary>Going through drawers and the safe of a home you've broken into. Once a day per house.</summary>
        public bool CanRob(Property prop, Game game)
        {
            if (game.Clock.Day != robbedDay) { robbedDay = game.Clock.Day; robbedToday.Clear(); }
            return IsForced(prop, game) && !robbedToday.Contains(prop.Id);
        }

        public void Rob(Property prop, bool safe, Game game)
        {
            robbedToday.Add(prop.Id);
            int cash = safe ? game.Rng.Next(250, 700) : prop.Kind == PropertyKind.Shop ? game.Rng.Next(80, 260) : game.Rng.Next(20, 160);
            game.Stock.Cash += cash;
            game.Territory.AddHeat(prop.Door, 8f);
            game.Org.Respect = Mathf.Max(0f, game.Org.Respect - 1f);   // stealing from the rione isn't honourable
            game.Log.Add($"You turn the place over: €{cash}{(safe ? " from the safe" : "")}.", MessageKind.Good, prop.Door);
            game.PopupAt(Pawns.Mover.CellCenter(prop.Door), $"+€{cash}");
            // Somebody will notice tomorrow, and talk.
            if (game.Rng.NextDouble() < 0.4) Suspect(game, "Burglary", GameClock.TicksPerHour / 2);
        }

        // ---------- Gunfire ----------

        /// <summary>Called a few times a second: shooting that goes on too long brings the police.</summary>
        public void Tick(Game game)
        {
            long now = game.Ticks.TicksGame;
            if (now % 60 != 0) return;
            var combat = game.Combat;
            if (now - combat.LastShotTick > 120 || now - lastShootingCall < GameClock.TicksPerHour * 2) return;
            if (combat.ShotsSince(now - 1200) < 6) return;
            lastShootingCall = now;
            var at = Vector2Int.FloorToInt(combat.LastShotPosition);
            if (now - combat.LastPlayerShotTick < 1200) Suspect(game, "The shooting", GameClock.TicksPerHour * 2);
            game.Territory.AddHeat(at, 15f);
            Call(at, game, 3, "Sirens. The shooting's been called in: police are coming.");
        }

        void Suspect(Game game, string why, long ticks)
        {
            SuspectUntil = System.Math.Max(SuspectUntil, game.Ticks.TicksGame + ticks);
            SuspectWhy = why;
        }

        /// <summary>A car's worth of police, from the nearest way into the rione, heading for the spot.</summary>
        void Call(Vector2Int at, Game game, int count, string message)
        {
            if (game.Territory.Entrances.Count == 0) return;
            var entrance = game.Territory.Entrances[0];
            foreach (var e in game.Territory.Entrances)
                if ((e - at).sqrMagnitude < (entrance - at).sqrMagnitude) entrance = e;
            for (int i = 0; i < count; i++)
            {
                var cop = game.Npcs.Spawn(NpcKind.Police, entrance, 0.075f, new Color32(30, 50, 130, 255));
                cop.IsPatrol = true;
                cop.Responding = true;
                cop.TripsLeft = 3;
                cop.Target = at;
                cop.Exit = entrance;
            }
            Views.SoundManager.PlayAt(Views.AudioSynth.Siren, Pawns.Mover.CellCenter(at), 0.6f);
            game.Log.Add(message, MessageKind.Bad, at);
            Sirens?.Invoke(message);
        }

        /// <summary>A responding officer close to a wanted player gives chase.</summary>
        public void OfficerNear(Npc cop, Game game)
        {
            var p = game.Player;
            if (p == null || !p.IsPresent || game.Pursuit.Active || game.Ticks.TicksGame > SuspectUntil) return;
            if ((cop.Mover.Position - p.Position).sqrMagnitude > 7f * 7f) return;
            if (!Combat.CombatSystem.HasLineOfSight(game.Map, cop.Mover.Cell, p.Cell)) return;
            game.Pursuit.Start(cop, $"{SuspectWhy}: the police are looking for you, and they've seen you.", game);
        }
    }
}
