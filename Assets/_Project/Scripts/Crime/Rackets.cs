using System.Collections.Generic;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// A piazza di spaccio: a street corner where the crew sells. Pushers stand on the spot,
    /// lookouts (vedette) watch the approaches a few cells away.
    /// </summary>
    public sealed class DrugSquare
    {
        public const int OpenHour = 9;
        public const int CloseHour = 1; // closes at 01:00

        public Vector2Int Cell;
        public int ZoneId;
        public readonly List<Vector2Int> LookoutPosts = new List<Vector2Int>();
        public int SalesToday;
        public int LostToday; // customers who left with nothing
        public int RevenueToday;

        public static bool IsOpenAt(int hour) => hour >= OpenHour || hour < CloseHour;
    }

    public enum BusinessStatus { Free, Targeted, Paying, Refusing }

    /// <summary>A shop in the district that can be made to pay pizzo.</summary>
    public sealed class Business
    {
        public Vector2Int Cell;
        public string Name;
        public ShopKind Kind;
        public BusinessStatus Status;
        public int WeeklyPizzo;
        public float Defiance;        // 0..1: how likely the owner resists or goes to the police
        public int LastCollectedWeek = -1;
        public Pawn ClaimedBy;

        public string StatusLabel => Status switch
        {
            BusinessStatus.Free => "Not paying",
            BusinessStatus.Targeted => "Collector on the way",
            BusinessStatus.Paying => $"Paying €{WeeklyPizzo}/week",
            _ => "Refused",
        };
    }
}
