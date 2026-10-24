using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>
    /// Someone down in the street: pick him up and get him home, out of sight of the neighbours and the police.
    /// There the wounds are seen to with the medicine at home, unless they're too bad for that.
    /// </summary>
    public sealed class CarryHomeJob : Job
    {
        readonly Pawn patient;
        Walker walker;
        bool carrying;

        public CarryHomeJob(Pawn pawn, Pawn patient) : base(pawn) => this.patient = patient;

        public override string Label => carrying ? $"Carrying {patient.Nickname ?? patient.DisplayName} home" : $"Going to pick up {patient.Nickname ?? patient.DisplayName}";
        public override bool IsWork => true;

        public override bool Tick(Game game)
        {
            if (patient.Dead || !patient.IsPresent || !Pawn.CanAct) return true;
            if (!carrying)
            {
                walker ??= new Walker(patient.Cell, adjacent: true) { Flexible = true };
                var s = walker.Tick(Pawn, game);
                if (s == WalkStatus.Failed) return true;
                if (s == WalkStatus.Walking && (patient.Position - Pawn.Position).sqrMagnitude > 1.5f * 1.5f) return false;
                carrying = true;
                var home = Housing.RandomCellAtHome(patient, game) ?? Vector2Int.RoundToInt(game.PlayerBase.center);
                walker = new Walker(home, adjacent: false, speedMultiplier: 0.6f);   // a man on your back: slow going
                return false;
            }
            var status = walker.Tick(Pawn, game);
            patient.Mover.Place(Pawn.Position - Pawn.Forward * 0.25f);   // over his shoulder
            if (status == WalkStatus.Walking) return false;
            if (status == WalkStatus.Arrived) Arrived(game);
            return true;
        }

        void Arrived(Game game)
        {
            string who = patient.DisplayName;
            if (Healing.TooHurtForHome(patient))
                game.Log.Add($"{who} is home, but it's a bullet deep in: medicine from the farmacia won't do. Only the hospital can fix it (118), and they report gunshots.", MessageKind.Bad, patient.Cell);
            else if (game.Stock.Medicine > 0 && Healing.NeedsCare(patient)) Healing.WithMedicine(game, patient);
            else if (Healing.NeedsCare(patient))
                game.Log.Add($"{who} is home, but there's no medicine to treat him. The farmacia sells it.", MessageKind.Bad, patient.Cell);
            else game.Log.Add($"{who} is home, lying down.", MessageKind.Info, patient.Cell);
        }
    }
}
