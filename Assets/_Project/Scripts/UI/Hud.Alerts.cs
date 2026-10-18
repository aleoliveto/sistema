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
        }
    }
}
