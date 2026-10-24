using System.Collections.Generic;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.Core
{
    /// <summary>Things you can carry in a slot: what goes in your bag, a scooter's seat, the stash.</summary>
    public enum Item { None, Doses, Packs, Fakes, Pistol, Beretta, Kalash, Medicine, Meal }

    public static class Items
    {
        public static int StackOf(Item i) => i switch
        {
            Item.Doses => 20, Item.Packs => 20, Item.Fakes => 5, Item.Medicine => 5, Item.Meal => 3,
            _ => 1,
        };

        public static string Name(Item i) => i switch
        {
            Item.Doses => "Doses", Item.Packs => "ROSSE", Item.Fakes => "Fakes", Item.Pistol => "Pistol",
            Item.Beretta => "Beretta", Item.Kalash => "Kalashnikov", Item.Medicine => "Medicine", Item.Meal => "Meal",
            _ => "",
        };

        public static string Hint(Item i) => i switch
        {
            Item.Doses => "Half-gram doses of 'a roba. Sold with G at a piazza or your corner. The police take them if they search you.",
            Item.Packs => "Packs of ROSSE. Sold with G at your corner or the stall.",
            Item.Fakes => "Fake bags and Napoli shirts, for the stall.",
            Item.Pistol or Item.Beretta or Item.Kalash => "Pick its slot (or press its number) to draw it; pick another to put it away. Carrying one is a crime.",
            Item.Medicine => "Right-click in the bag to clean and bandage your wounds.",
            Item.Meal => "Right-click in the bag to eat.",
            _ => "",
        };

        /// <summary>A short tag drawn in the slot until there's painted art for it.</summary>
        public static string Tag(Item i) => i switch
        {
            Item.Doses => "◆", Item.Packs => "▮", Item.Fakes => "◈", Item.Pistol => "P", Item.Beretta => "B",
            Item.Kalash => "AK", Item.Medicine => "✚", Item.Meal => "●", _ => "",
        };

        public static Color Tint(Item i) => i switch
        {
            Item.Doses => new Color(0.85f, 0.85f, 0.9f), Item.Packs => new Color(0.85f, 0.25f, 0.2f),
            Item.Fakes => new Color(0.95f, 0.75f, 0.3f), Item.Medicine => new Color(0.4f, 0.85f, 0.45f),
            Item.Meal => new Color(0.95f, 0.6f, 0.3f), _ => new Color(0.7f, 0.72f, 0.75f),
        };

        public static bool IsGun(Item i) => i == Item.Pistol || i == Item.Beretta || i == Item.Kalash;

        public static Item OfGun(Combat.Gun g) => g == Combat.Gun.Kalash ? Item.Kalash : g == Combat.Gun.Beretta ? Item.Beretta : Item.Pistol;

        public static Combat.Gun GunOf(Item i) => i == Item.Kalash ? Combat.Gun.Kalash : i == Item.Beretta ? Combat.Gun.Beretta : Combat.Gun.Pistol;
    }

    [System.Serializable]
    public struct Slot
    {
        public Item Item;
        public int Count;
        public bool Empty => Item == Item.None || Count <= 0;
    }

    /// <summary>A row of slots, each holding a stack of one thing.</summary>
    public sealed class Inventory
    {
        public Slot[] Slots;

        public Inventory(int size) { Slots = new Slot[size]; }

        public int Count(Item item)
        {
            int n = 0;
            foreach (var s in Slots) if (s.Item == item) n += s.Count;
            return n;
        }

        /// <summary>How many more of this would fit.</summary>
        public int Room(Item item)
        {
            int stack = Items.StackOf(item), n = 0;
            foreach (var s in Slots)
                if (s.Empty) n += stack;
                else if (s.Item == item) n += Mathf.Max(0, stack - s.Count);
            return n;
        }

        /// <summary>Adds what fits (topping up stacks first). Returns how many went in.</summary>
        public int Add(Item item, int n)
        {
            if (item == Item.None || n <= 0) return 0;
            int stack = Items.StackOf(item), added = 0;
            for (int pass = 0; pass < 2 && n > 0; pass++)
                for (int i = 0; i < Slots.Length && n > 0; i++)
                {
                    ref var s = ref Slots[i];
                    if (pass == 0 ? s.Item != item || s.Empty : !s.Empty) continue;
                    if (pass == 1) { s.Item = item; s.Count = 0; }
                    int k = Mathf.Min(n, stack - s.Count);
                    s.Count += k; n -= k; added += k;
                }
            return added;
        }

        /// <summary>Takes up to n (from the last stacks first). Returns how many came out.</summary>
        public int Remove(Item item, int n)
        {
            int taken = 0;
            for (int i = Slots.Length - 1; i >= 0 && n > 0; i--)
            {
                ref var s = ref Slots[i];
                if (s.Item != item) continue;
                int k = Mathf.Min(n, s.Count);
                s.Count -= k; n -= k; taken += k;
                if (s.Count <= 0) s = default;
            }
            return taken;
        }

        public bool IsEmpty
        {
            get { foreach (var s in Slots) if (!s.Empty) return false; return true; }
        }

        public string Describe()
        {
            var parts = new List<string>();
            foreach (Item i in System.Enum.GetValues(typeof(Item)))
            {
                int c = i == Item.None ? 0 : Count(i);
                if (c > 0) parts.Add(c == 1 && Items.StackOf(i) == 1 ? Items.Name(i).ToLower() : $"{c} {Items.Name(i).ToLower()}");
            }
            return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
        }

        // For the save file.
        public int[] SaveItems() { var a = new int[Slots.Length]; for (int i = 0; i < a.Length; i++) a[i] = (int)Slots[i].Item; return a; }
        public int[] SaveCounts() { var a = new int[Slots.Length]; for (int i = 0; i < a.Length; i++) a[i] = Slots[i].Count; return a; }
        public void Load(int[] items, int[] counts)
        {
            if (items == null || counts == null) return;
            for (int i = 0; i < Slots.Length && i < items.Length && i < counts.Length; i++)
                Slots[i] = counts[i] > 0 ? new Slot { Item = (Item)items[i], Count = counts[i] } : default;
        }
    }

    /// <summary>
    /// The boss's bag: the hotbar along the bottom. Doses, packs and fakes are still counted on the pawn
    /// (selling, pushers and searches all use those); the slots are kept in step with them, and the rest
    /// (guns, medicine, a meal) lives only here. The selected slot is what's in his hand: a gun there is drawn.
    /// </summary>
    public static class Bag
    {
        public const int Size = 8, SeatSize = 4;

        public static Inventory For(Pawn p) => p.Bag ??= new Inventory(Size);

        /// <summary>The bag's doses, packs and fakes match the pockets; the selected slot draws or holsters the gun.</summary>
        public static void Sync(Pawn p, Game game)
        {
            if (p == null) return;
            var bag = For(p);
            Match(bag, Item.Doses, ref p.PocketDoses, game);
            Match(bag, Item.Packs, ref p.PocketPacks, game);
            Match(bag, Item.Fakes, ref p.PocketFakes, game);
            // An old save, or a gun handed over somewhere else: it goes in the bag.
            if (p.Armed && bag.Count(Items.OfGun(p.Gun)) == 0)
            {
                if (bag.Add(Items.OfGun(p.Gun), 1) == 0) { game.Stock.AddGun(p.Gun, 1); p.Armed = false; }
                else p.BagSelected = IndexOf(bag, Items.OfGun(p.Gun));
            }
            if (p.BagSelected >= bag.Slots.Length) p.BagSelected = -1;
            var held = p.BagSelected >= 0 ? bag.Slots[p.BagSelected].Item : Item.None;
            bool gun = Items.IsGun(held);
            if (gun && (!p.Armed || p.Gun != Items.GunOf(held)))
            {
                bool swap = p.Armed;
                p.Armed = true;
                p.Gun = Items.GunOf(held);
                if (swap || p.Ammo <= 0) p.Ammo = p.MagSize;
            }
            else if (!gun && p.Armed) p.Armed = false;
        }

        static void Match(Inventory bag, Item item, ref int counter, Game game)
        {
            int have = bag.Count(item);
            if (have > counter) bag.Remove(item, have - counter);
            else if (have < counter)
            {
                int added = bag.Add(item, counter - have);
                int over = counter - have - added;
                if (over <= 0) return;
                // No room in the bag: the rest goes back to the stash.
                counter -= over;
                if (item == Item.Doses) game.Stock.Product += over;
                else if (item == Item.Packs) game.Stock.Cigarettes += over;
                else game.Org.H.fakes += over;
            }
        }

        public static int IndexOf(Inventory inv, Item item)
        {
            for (int i = 0; i < inv.Slots.Length; i++) if (inv.Slots[i].Item == item && !inv.Slots[i].Empty) return i;
            return -1;
        }

        /// <summary>How many more of a pocket item the bag takes (for filling pockets at the stash).</summary>
        public static int Room(Pawn p, Item item) => For(p).Room(item);

        /// <summary>Puts something in the bag; pocket items go through their counters so selling sees them.</summary>
        public static int Give(Pawn p, Item item, int n)
        {
            var bag = For(p);
            n = Mathf.Min(n, bag.Room(item));
            if (n <= 0) return 0;
            switch (item)
            {
                case Item.Doses: p.PocketDoses += n; break;
                case Item.Packs: p.PocketPacks += n; break;
                case Item.Fakes: p.PocketFakes += n; break;
            }
            bag.Add(item, n);
            return n;
        }

        /// <summary>Takes something out of the bag (and its pocket counter).</summary>
        public static int Take(Pawn p, Item item, int n)
        {
            var bag = For(p);
            n = bag.Remove(item, n);
            switch (item)
            {
                case Item.Doses: p.PocketDoses = Mathf.Max(0, p.PocketDoses - n); break;
                case Item.Packs: p.PocketPacks = Mathf.Max(0, p.PocketPacks - n); break;
                case Item.Fakes: p.PocketFakes = Mathf.Max(0, p.PocketFakes - n); break;
            }
            return n;
        }

        /// <summary>A gun anywhere in the bag, drawn or not: what a search finds.</summary>
        public static bool CarriesGun(Pawn p) =>
            p.Armed || (p.Bag != null && (p.Bag.Count(Item.Pistol) + p.Bag.Count(Item.Beretta) + p.Bag.Count(Item.Kalash)) > 0);

        /// <summary>Right-click in the bag: eat the meal, use the medicine.</summary>
        public static void Use(Pawn p, int slot, Game game)
        {
            var bag = For(p);
            if (slot < 0 || slot >= bag.Slots.Length) return;
            var item = bag.Slots[slot].Item;
            if (item == Item.Meal)
            {
                if (p.Needs.Food > 0.95f) { game.Log.Add("You're not hungry.", MessageKind.Info); return; }
                bag.Remove(Item.Meal, 1);
                p.Needs.Eat(0.45f);
                game.Log.Add("You eat on the go.", MessageKind.Info);
            }
            else if (item == Item.Medicine)
            {
                // Healing works from the stash's count: lend it the one from the bag.
                game.Stock.Medicine++;
                if (Crime.Healing.WithMedicine(game, p)) bag.Remove(Item.Medicine, 1);
                else
                {
                    game.Stock.Medicine--;
                    if (!Crime.Healing.NeedsCare(p)) game.Log.Add("Nothing to treat.", MessageKind.Info);
                }
            }
        }
    }

    /// <summary>
    /// What's under each family scooter's seat. It goes with the scooter: kept by where it's parked while it
    /// stands in the street, and on the rider while someone has it.
    /// </summary>
    public sealed class Seats
    {
        readonly Dictionary<string, Inventory> parked = new Dictionary<string, Inventory>();

        static string Key(Game game, Vector2Int c) => $"{game.District?.Id}:{c.x},{c.y}";

        public Inventory At(Game game, Vector2Int c, bool create)
        {
            if (parked.TryGetValue(Key(game, c), out var inv)) return inv;
            if (!create) return null;
            return parked[Key(game, c)] = new Inventory(Bag.SeatSize);
        }

        /// <summary>The seat leaves the kerb with the rider.</summary>
        public Inventory Lift(Game game, Vector2Int c)
        {
            var k = Key(game, c);
            if (!parked.TryGetValue(k, out var inv)) return null;
            parked.Remove(k);
            return inv;
        }

        public void Drop(Game game, Vector2Int c, Inventory seat)
        {
            if (seat == null || seat.IsEmpty) return;
            parked[Key(game, c)] = seat;
        }

        // For the save file.
        public List<(string key, Inventory inv)> All()
        {
            var l = new List<(string, Inventory)>();
            foreach (var kv in parked) if (!kv.Value.IsEmpty) l.Add((kv.Key, kv.Value));
            return l;
        }

        public void Clear() => parked.Clear();

        public void Restore(string key, Inventory inv) { if (inv != null && !inv.IsEmpty) parked[key] = inv; }
    }
}
