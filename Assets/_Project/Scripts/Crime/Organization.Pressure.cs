using System.Linq;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.Story;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// The rione pushing back. Shopkeepers squeezed too hard band together in an anti-racket association; with
    /// enough evidence, the prosecutors go after what the family owns, not just who it is.
    /// </summary>
    public sealed partial class Organization
    {
        void PressureDaily(Game game)
        {
            AntiRacketDaily(game);
            SeizureDaily(game);
        }

        // ---------- Anti-racket ----------

        public bool AntiRacket(Zone zone) => zone != null && H.antiRacket.Contains(zone.Id);

        void AntiRacketDaily(Game game)
        {
            var rng = game.Rng;
            foreach (var zone in game.Territory.Zones)
            {
                if (H.antiRacket.Contains(zone.Id)) continue;
                // Enough shops squeezed (paying, refusing, burned out) in one rione, and it's been hot.
                var shops = Businesses.Where(b => game.Territory.ZoneAt(b.Cell) == zone).ToList();
                int squeezed = shops.Count(b => b.Status == BusinessStatus.Paying || b.Status == BusinessStatus.Refusing || b.Status == BusinessStatus.Closed);
                if (squeezed < 3 || rng.NextDouble() > 0.02 + zone.Heat / 2000f) continue;
                H.antiRacket.Add(zone.Id);
                foreach (var b in shops)
                    if (!H.godchildShops.Contains(Businesses.IndexOf(b))) b.Defiance = Mathf.Min(1f, b.Defiance + 0.35f);
                game.Newspaper.Report(game, 4, $"Shopkeepers of {zone.Name} say no", "An anti-racket association has been founded in a parish hall. The members promise to report every request for money.");
                game.Events.Raise(AntiRacketCard(zone, game));
                return;   // one a day at most
            }
        }

        GameEvent AntiRacketCard(Zone zone, Game game)
        {
            return new GameEvent
            {
                Id = "anti_racket",
                Title = $"The shopkeepers of {zone.Name}",
                Neapolitan = "Hanno miso 'nu manifesto 'ncopp' 'a porta d''a chiesa.",
                English = "They've put up a poster on the church door.",
                Body = $"The shops of {zone.Name} have founded an anti-racket association. They'll refuse the pizzo together and report every visit. The founder is the baker, and the parish priest is with them.",
            }
            .Option("Send a message to the founder", "Heat +15, evidence +5. Half the time the association falls apart; otherwise he becomes a martyr and they dig in.", g =>
            {
                g.Territory.AddHeat(Vector2Int.RoundToInt(zone.Rect.center), 15f);
                g.Org.AddEvidence(5f, g);
                if (g.Rng.NextDouble() < 0.5)
                {
                    g.Org.H.antiRacket.Remove(zone.Id);
                    g.Log.Add($"The baker's shutters were found scorched. The association in {zone.Name} has quietly dissolved.", MessageKind.Good);
                }
                else
                {
                    foreach (var b in g.Org.Businesses.Where(b => g.Territory.ZoneAt(b.Cell) == zone)) b.Defiance = Mathf.Min(1f, b.Defiance + 0.2f);
                    g.Log.Add($"The baker went on television. In {zone.Name}, nobody will pay now.", MessageKind.Bad);
                }
            })
            .Option("Ease off the rione for now", $"No more pizzo in {zone.Name}. Respect −3; the association fades in a few weeks.", g =>
            {
                foreach (var b in g.Org.Businesses.Where(b => g.Territory.ZoneAt(b.Cell) == zone && (b.Status == BusinessStatus.Paying || b.Status == BusinessStatus.Refusing || b.Status == BusinessStatus.Targeted)))
                    b.Status = BusinessStatus.Free;
                g.Org.Respect = Mathf.Max(0f, g.Org.Respect - 3f);
            })
            .Option("Ignore them", "They'll refuse more and report more.", g => { });
        }

        // ---------- Asset seizures ----------

        void SeizureDaily(Game game)
        {
            if (Evidence < 80f || game.Rng.NextDouble() > 0.06) return;
            // Something bought (not rented, not the basso) that the prosecutors can put a name to.
            var target = game.Properties.Owned.Where(p => p.Rent <= 0 && !p.Rect.Overlaps(game.PlayerBase) && p.Kind != PropertyKind.Civic)
                                              .OrderByDescending(p => p.Price).FirstOrDefault();
            if (target == null) return;
            game.Events.Raise(SeizureCard(target, game));
        }

        GameEvent SeizureCard(Property p, Game game)
        {
            const int lawyer = 1500, frontMan = 900;
            return new GameEvent
            {
                Id = "seizure",
                Title = "Sequestro preventivo",
                Neapolitan = "'E carte dicono ca è d''o vuosto. Mo s''a pigliano.",
                English = "The papers say it's yours. Now they're taking it.",
                Body = $"The prosecutors have asked a judge to seize {p.Name} (worth €{p.Price}) as the proceeds of crime. The order could be signed within days.",
            }
            .Option($"Avvocato Ruocco fights it (€{lawyer})", "Seven times in ten the judge says no.", g =>
            {
                g.Stock.Cash -= lawyer;
                if (g.Rng.NextDouble() < 0.7) g.Log.Add($"The judge refused the seizure of {p.Name}. For now.", MessageKind.Good);
                else g.Org.Seize(p, g);
            }, g => g.Stock.Cash >= lawyer)
            .Option($"Put it in a cousin's name (€{frontMan})", "A testa di legno: it stays in the family. Evidence +2.", g =>
            {
                g.Stock.Cash -= frontMan;
                g.Org.AddEvidence(2f, g);
                g.Log.Add($"{p.Name} now belongs, on paper, to a cousin with a clean record.", MessageKind.Good);
            }, g => g.Stock.Cash >= frontMan)
            .Option("Let them take it", "It's gone, and whoever lived there comes home to the basso.", g => g.Org.Seize(p, g));
        }

        public void Seize(Property p, Game game)
        {
            p.OwnedByPlayer = false;
            p.Rent = 0;
            p.StashDoses = 0;
            if (p.Manager != null && p.Manager.Role == CrewRole.Manager) p.Manager.Role = CrewRole.General;
            p.Manager = null;
            p.Operation = Operation.None;
            var business = Businesses.Find(b => p.Rect.Contains(b.Cell));
            if (business != null && business.Status == BusinessStatus.Owned) business.Status = BusinessStatus.Free;
            foreach (var c in game.Pawns.Crew)
                if (c.Home.HasValue && p.Rect.Contains(c.Home.Value)) Housing.MoveIn(c, null);
            Evidence = Mathf.Max(0f, Evidence - 10f);
            game.Log.Add($"{p.Name} has been seized by the State. A sign on the door: \"Bene confiscato alla camorra\".", MessageKind.Bad, p.Door);
            game.Newspaper.Report(game, 5, "Camorra property seized", $"{p.Name} has been confiscated. The council says it will become a centre for the young people of the rione.");
        }
    }
}
