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
        public Vector2Int Cell;
        public int OpenHour = 9;
        public int CloseHour = 1;   // equal hours = open around the clock
        public long LastLostWarningTick = -100000;
        public int ZoneId;
        public bool Rival;          // run by the rival clan
        public int Clan;            // which clan (index into Game.Clans), for rival piazze
        public readonly List<Vector2Int> LookoutPosts = new List<Vector2Int>();
        public int SalesToday;
        public int LostToday; // customers who left with nothing
        public int RevenueToday;

        public bool IsOpenAt(int hour)
        {
            if (OpenHour == CloseHour) return true;
            if (OpenHour < CloseHour) return hour >= OpenHour && hour < CloseHour;
            return hour >= OpenHour || hour < CloseHour; // shift runs past midnight
        }

        public string HoursLabel => OpenHour == CloseHour ? "24 hours" : $"{OpenHour:00}:00–{CloseHour:00}:00";
    }

    public enum BusinessStatus { Free, Targeted, Paying, Refusing, Closed, Owned }

    /// <summary>What to do to a business that won't pay.</summary>
    public enum Retaliation { None, Scare, Burn }

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
        public Retaliation PendingAction;
        public long ClosedUntil;
        public string CounterDefId;   // restored when a burned shop reopens

        public string StatusLabel => Status switch
        {
            BusinessStatus.Free => "Not paying",
            BusinessStatus.Targeted => "Collector on the way",
            BusinessStatus.Paying => $"Paying €{WeeklyPizzo}/week",
            BusinessStatus.Closed => "Burned out",
            BusinessStatus.Owned => "Yours (front)",
            _ when PendingAction == Retaliation.Scare => "Refused · scare ordered",
            _ when PendingAction == Retaliation.Burn => "Refused · arson ordered",
            _ => "Refused",
        };
    }
}
