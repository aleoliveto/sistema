using System.Collections.Generic;
using System.Linq;
using OSistema.Core;
using OSistema.Pawns;

namespace OSistema.Story
{
    /// <summary>Leaving for another district: who comes along. Whoever stays keeps working there.</summary>
    public static class TravelCards
    {
        public static GameEvent Leave(string toId, Game game)
        {
            string name = District.NameOf(toId);
            var free = game.Pawns.Crew.Where(c => c.CanAct && !c.Controlled && !c.InJail && !c.InHospital).ToList();
            var guards = free.Where(c => c.Role == CrewRole.Bodyguard).ToList();
            var e = new GameEvent
            {
                Id = "travel",
                Title = $"The road to {name}",
                English = "Half an hour on the coast road.",
                Body = "Whoever stays behind keeps their post: pushers sell, lookouts watch. While you're away you only hear about it on the phone.",
            };
            e.Option("Go alone", "Quick and quiet.", g => g.TravelTo(toId, new List<Pawn>()));
            if (guards.Count > 0)
                e.Option($"Take the bodyguards ({guards.Count})", string.Join(", ", guards.Select(c => c.Nickname)), g => g.TravelTo(toId, guards));
            if (free.Count > 0)
                e.Option($"Take everyone free ({free.Count})", "Your piazze at home will be left to whoever's on them.", g => g.TravelTo(toId, free));
            return e.Option("Not now", "", g => { });
        }
    }
}
