using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.Views;
using UnityEngine;

namespace OSistema.UI
{
    /// <summary>
    /// Who's watching the street. A lookout on duty whistles when the police come near; you get a
    /// warning when a patrol comes into your own view. Each patrol is announced once.
    /// </summary>
    public sealed partial class Hud
    {
        readonly HashSet<int> policeAnnounced = new HashSet<int>();

        void UpdatePoliceAlerts()
        {
            var p = game.Player;
            foreach (var cop in game.Npcs.All)
            {
                if (cop.Kind != NpcKind.Police || cop.Despawned || policeAnnounced.Contains(cop.Id)) continue;

                // A vedetta on his post sees them first.
                Pawn spotter = null;
                foreach (var c in game.Pawns.Crew)
                {
                    if (!c.CanAct || !(c.CurrentJob is PostJob post) || post.Role != CrewRole.Lookout || !post.AtPost) continue;
                    float range = 10f + 1.2f * c.AwarenessSkill;
                    if ((c.Position - cop.Mover.Position).sqrMagnitude > range * range) continue;
                    spotter = c;
                    break;
                }
                if (spotter != null)
                {
                    policeAnnounced.Add(cop.Id);
                    spotter.Say("police");
                    SoundManager.PlayAt(AudioSynth.Whistle, spotter.Position, 0.5f);
                    FlashAlert("'A POLIZIA!");
                    game.Log.Add($"{spotter.Nickname} whistles from his corner: police coming.", MessageKind.Bad, cop.Mover.Cell);
                    continue;
                }

                // You see them yourself.
                if (p != null && p.IsPresent && FogOfWar.CanSee(cop.Mover.Position) && (p.Position - cop.Mover.Position).sqrMagnitude < 15f * 15f)
                {
                    policeAnnounced.Add(cop.Id);
                    bool dirty = game.Org.CarriedDg > 0 || p.Carrying != null || game.Org.DealingCorner != null || p.Armed;
                    game.Log.Add(dirty ? "Police in the street, and you've got something on you. Keep your distance." : "A police patrol is walking the street.",
                        dirty ? MessageKind.Bad : MessageKind.Info, cop.Mover.Cell);
                    if (dirty) FlashAlert("POLIZIA");
                }
            }
            if (policeAnnounced.Count > 500) policeAnnounced.Clear();

            // Clan squads coming for a piazza, a killing or a stesa: a lookout sees them first, or you do.
            foreach (var r in game.Pawns.Rivals)
            {
                if (!r.IsPresent || policeAnnounced.Contains(-r.Id)) continue;
                if (r.Mission != RivalMission.Assault && r.Mission != RivalMission.Hit && r.Mission != RivalMission.Stesa) continue;
                if (r.Mission == RivalMission.Assault && r.MissionSquare != null && r.MissionSquare.Rival) continue;   // their war, not yours
                Pawn spotter = null;
                foreach (var c in game.Pawns.Crew)
                {
                    if (!c.CanAct || !(c.CurrentJob is PostJob post) || post.Role != CrewRole.Lookout || !post.AtPost) continue;
                    float range = 12f + 1.2f * c.AwarenessSkill;
                    if ((c.Position - r.Position).sqrMagnitude <= range * range) { spotter = c; break; }
                }
                bool seen = spotter != null || (p != null && FogOfWar.CanSee(r.Position) && (p.Position - r.Position).sqrMagnitude < 14f * 14f);
                if (!seen) continue;
                // One warning per squad: mark everyone on the same mission.
                foreach (var o in game.Pawns.Rivals)
                    if (o.Clan == r.Clan && o.Mission == r.Mission) policeAnnounced.Add(-o.Id);
                var clan = game.ClanOf(r);
                string what = r.Mission == RivalMission.Assault ? "coming to take the piazza" : r.Mission == RivalMission.Hit ? "armed, looking for someone" : "riding in for a stesa";
                if (spotter != null)
                {
                    spotter.Say("police");
                    SoundManager.PlayAt(AudioSynth.Whistle, spotter.Position, 0.6f);
                    game.Log.Add($"{spotter.Nickname} whistles: the {clan.Name}, {what}!", MessageKind.Bad, r.Cell);
                }
                else game.Log.Add($"Men of the {clan.Name}, {what}.", MessageKind.Bad, r.Cell);
                FlashAlert($"{clan.Name.ToUpper()}!");
            }
        }
    }
}
