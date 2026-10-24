using System.Collections.Generic;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// The boss's bag along the bottom of the screen: eight slots, 1-8 to pick one (a gun there is drawn).
    /// I opens it beside whatever is at hand: the scooter's seat, a hiding flat, the stash at home.
    /// Click a stack to move it across; right-click moves one (or, in the bag, eats or uses it).
    /// </summary>
    public sealed partial class Hud
    {
        VisualElement bagBar, invLayer, invOther, invBag, invTabs;
        Label invOtherTitle, invOtherNote;
        readonly VisualElement[] bagSlots = new VisualElement[Bag.Size];
        string bagDrawn;
        int storeIndex;
        bool invOpen;
        float invRefresh;

        // ---------- What the other side can be ----------

        abstract class Store
        {
            public string Title, Note;
            public abstract List<(Item item, int count)> Tiles();
            public abstract int Put(Item item, int n);
            public abstract int Take(Item item, int n);
            public virtual int Size => 8;
        }

        sealed class SlotStore : Store
        {
            readonly Inventory inv;
            public SlotStore(Inventory i, string title, string note) { inv = i; Title = title; Note = note; }
            public override int Size => inv.Slots.Length;
            public override List<(Item, int)> Tiles()
            {
                var l = new List<(Item, int)>();
                foreach (var s in inv.Slots) l.Add(s.Empty ? (Item.None, 0) : (s.Item, s.Count));
                return l;
            }
            public override int Put(Item item, int n) => inv.Add(item, n);
            public override int Take(Item item, int n) => inv.Remove(item, n);
        }

        /// <summary>The stash at the basso: no slots to fill up, just what the family has.</summary>
        sealed class StashStore : Store
        {
            readonly Game game;
            static readonly Item[] Order = { Item.Doses, Item.Packs, Item.Fakes, Item.Pistol, Item.Beretta, Item.Kalash, Item.Medicine, Item.Meal };
            public StashStore(Game g) { game = g; Title = "THE STASH"; Note = "At home: as much as you like."; }
            public override int Size => Order.Length;

            int Have(Item i)
            {
                var s = game.Stock;
                return i switch
                {
                    Item.Doses => s.Product, Item.Packs => s.Cigarettes, Item.Fakes => game.Org.H.fakes,
                    Item.Pistol => s.Pistols, Item.Beretta => s.Berettas, Item.Kalash => s.Kalash,
                    Item.Medicine => s.Medicine, Item.Meal => s.Meals, _ => 0,
                };
            }

            void Change(Item i, int d)
            {
                var s = game.Stock;
                switch (i)
                {
                    case Item.Doses: s.Product += d; break;
                    case Item.Packs: s.Cigarettes += d; break;
                    case Item.Fakes: game.Org.H.fakes += d; break;
                    case Item.Medicine: s.Medicine += d; break;
                    case Item.Meal: s.Meals += d; break;
                    default: if (Items.IsGun(i)) s.AddGun(Items.GunOf(i), d); break;
                }
            }

            public override List<(Item, int)> Tiles()
            {
                var l = new List<(Item, int)>();
                foreach (var i in Order) l.Add(Have(i) > 0 ? (i, Have(i)) : (Item.None, 0));
                return l;
            }
            public override int Put(Item item, int n) { Change(item, n); return n; }
            public override int Take(Item item, int n) { n = Mathf.Min(n, Have(item)); Change(item, -n); return n; }
        }

        /// <summary>A hiding flat: it only hides doses.</summary>
        sealed class FlatStore : Store
        {
            readonly Property flat;
            public FlatStore(Property f) { flat = f; Title = f.Name.ToUpper(); Note = "A hiding flat keeps only doses."; }
            public override int Size => 1;
            public override List<(Item, int)> Tiles() => new List<(Item, int)> { flat.StashDoses > 0 ? (Item.Doses, flat.StashDoses) : (Item.None, 0) };
            public override int Put(Item item, int n) { if (item != Item.Doses) return 0; flat.StashDoses += n; return n; }
            public override int Take(Item item, int n) { n = Mathf.Min(n, item == Item.Doses ? flat.StashDoses : 0); flat.StashDoses -= n; return n; }
        }

        /// <summary>Everything within reach to open, nearest first.</summary>
        List<Store> StoresAt(Pawn p)
        {
            var l = new List<Store>();
            if (p == null) return l;
            if (p.Riding && p.HasFamilyScooter)
                l.Add(new SlotStore(p.Seat ??= new Inventory(Bag.SeatSize), "UNDER THE SEAT", "Rides with the scooter, and stays with it where you park."));
            else if (FamilyScooters.Nearest(game, p.Position, FamilyScooters.PlayerReach, out var at))
                l.Add(new SlotStore(game.Seats.At(game, at, true), "UNDER THE SEAT", "This scooter's seat. It stays with the scooter."));
            var flat = game.Org.NascondiglioAt(p.Cell, game);
            if (flat != null) l.Add(new FlatStore(flat));
            if (game.PlayerBase.Contains(p.Cell)) l.Add(new StashStore(game));
            return l;
        }

        // ---------- The bar ----------

        void BuildBag()
        {
            bagBar = Ui.Box("bagbar").Into(dockRoot);
            var tabs = dockRoot.Q(className: "hotbar");
            if (tabs != null) bagBar.PlaceBehind(tabs);
            bagBar.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < Bag.Size; i++)
            {
                int k = i;
                var slot = SlotTile(bagBar, $"{i + 1}");
                slot.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) SelectBagSlot(k); else if (e.button == 1) BagRightClick(k); e.StopPropagation(); });
                Tip(slot, () => SlotTip(k));
                bagSlots[i] = slot;
            }

            invLayer = Ui.Box("layer").Into(modalLayer);
            invLayer.Show(false);
            invLayer.pickingMode = PickingMode.Ignore;
            invLayer.RegisterCallback<PointerDownEvent>(e => { if (e.target == invLayer && invOpen) ToggleInventory(); });
            var card = Ui.Box("panel", "inv").Into(invLayer);
            var cols = Ui.Row(card);
            cols.style.alignItems = Align.FlexStart;
            var left = Ui.Box("col").Into(cols);
            Ui.Text("YOUR BAG", left, "section");
            invBag = Ui.Box("inv__grid").Into(left);
            Ui.Text("Click: put it away · right-click: use", left, "tiny", "muted");
            var right = Ui.Box("col").Into(cols);
            right.style.marginLeft = 24;
            invTabs = Ui.Row(right);
            invOtherTitle = Ui.Text("", right, "section");
            invOther = Ui.Box("inv__grid").Into(right);
            invOtherNote = Ui.Text("", right, "tiny", "muted");
            var foot = Ui.Row(card);
            foot.style.justifyContent = Justify.FlexEnd;
            Ui.Btn("Close (I)", ToggleInventory, foot, "btn--small");
        }

        VisualElement SlotTile(VisualElement parent, string key)
        {
            var slot = Ui.Box("bagslot").Into(parent);
            if (key != null) Ui.Text(key, slot, "bagslot__key").pickingMode = PickingMode.Ignore;
            Ui.Text("", slot, "bagslot__tag").pickingMode = PickingMode.Ignore;
            Ui.Text("", slot, "bagslot__count").pickingMode = PickingMode.Ignore;
            return slot;
        }

        static void FillTile(VisualElement slot, Item item, int count, bool on)
        {
            var tag = slot.Q<Label>(className: "bagslot__tag");
            var cnt = slot.Q<Label>(className: "bagslot__count");
            tag.text = Items.Tag(item);
            tag.style.color = Items.Tint(item);
            cnt.text = item == Item.None || count <= 1 && Items.StackOf(item) == 1 ? "" : count.ToString();
            slot.Toggle("bagslot--on", on);
            slot.Toggle("bagslot--empty", item == Item.None);
        }

        string SlotTip(int k)
        {
            var p = game?.Player;
            if (p == null) return "";
            var s = Bag.For(p).Slots[k];
            return s.Empty ? $"Empty ({k + 1})" : $"{Items.Name(s.Item)}{(s.Count > 1 ? $" ×{s.Count}" : "")}\n{Items.Hint(s.Item)}";
        }

        public void SelectBagSlot(int i)
        {
            var p = game?.Player;
            if (p == null || i < 0 || i >= Bag.Size) return;
            p.BagSelected = p.BagSelected == i ? -1 : i;
            Bag.Sync(p, game);
            bagDrawn = null;
        }

        void BagRightClick(int i)
        {
            var p = game?.Player;
            if (p == null) return;
            Bag.Use(p, i, game);
            Bag.Sync(p, game);
            bagDrawn = null;
            if (invOpen) DrawInventory();
        }

        public void ToggleInventory()
        {
            if (game?.Player == null) return;
            invOpen = !invOpen;
            invLayer.Show(invOpen);
            invLayer.pickingMode = invOpen ? PickingMode.Position : PickingMode.Ignore;
            storeIndex = 0;
            if (invOpen) DrawInventory();
        }

        /// <summary>Every frame: redraw the bar when what's in it changes; close the bag if what was open is out of reach.</summary>
        void UpdateBag()
        {
            var p = game?.Player;
            if (bagBar == null) return;
            bagBar.Show(p != null && !p.Dead && p.IsPresent);
            if (p == null) return;
            var bag = Bag.For(p);
            var sb = new System.Text.StringBuilder();
            foreach (var s in bag.Slots) sb.Append((int)s.Item).Append(':').Append(s.Count).Append(',');
            sb.Append(p.BagSelected);
            string key = sb.ToString();
            // While it's open, keep what's at hand current as you walk about.
            if (invOpen && (invRefresh -= Time.unscaledDeltaTime) <= 0f) { invRefresh = 0.5f; bagDrawn = null; }
            if (key == bagDrawn) return;
            bagDrawn = key;
            for (int i = 0; i < Bag.Size; i++) FillTile(bagSlots[i], bag.Slots[i].Item, bag.Slots[i].Count, i == p.BagSelected);
            if (invOpen) DrawInventory();
        }

        void DrawInventory()
        {
            var p = game.Player;
            var bag = Bag.For(p);
            invBag.Clear();
            for (int i = 0; i < bag.Slots.Length; i++)
            {
                int k = i;
                var t = SlotTile(invBag, $"{i + 1}");
                FillTile(t, bag.Slots[i].Item, bag.Slots[i].Count, i == p.BagSelected);
                Tip(t, () => SlotTip(k));
                t.RegisterCallback<PointerDownEvent>(e => { MoveOut(k, e.button == 1); e.StopPropagation(); });
            }

            var stores = StoresAt(p);
            invTabs.Clear();
            if (stores.Count > 1)
                for (int i = 0; i < stores.Count; i++)
                {
                    int k = i;
                    Ui.Btn(stores[i].Title, () => { storeIndex = k; DrawInventory(); }, invTabs, "pill").Toggle("btn--on", k == storeIndex);
                }
            invOther.Clear();
            if (stores.Count == 0)
            {
                invOtherTitle.text = "NOTHING TO OPEN HERE";
                invOtherNote.text = "Stand by a family scooter, in a hiding flat, or at home to put things away.";
                return;
            }
            storeIndex = Mathf.Clamp(storeIndex, 0, stores.Count - 1);
            var store = stores[storeIndex];
            invOtherTitle.text = store.Title;
            invOtherNote.text = store.Note;
            var tiles = store.Tiles();
            for (int i = 0; i < tiles.Count; i++)
            {
                var (item, count) = tiles[i];
                var t = SlotTile(invOther, null);
                FillTile(t, item, count, false);
                if (item == Item.None) continue;
                Tip(t, () => $"{Items.Name(item)} ×{count}\nClick: take as many as fit · right-click: take one");
                t.RegisterCallback<PointerDownEvent>(e => { MoveIn(store, item, count, e.button == 1); e.StopPropagation(); });
            }
        }

        Store CurrentStore()
        {
            var stores = StoresAt(game.Player);
            return stores.Count == 0 ? null : stores[Mathf.Clamp(storeIndex, 0, stores.Count - 1)];
        }

        /// <summary>From the bag into what's open: the whole stack, or one.</summary>
        void MoveOut(int slot, bool one)
        {
            var p = game.Player;
            var s = Bag.For(p).Slots[slot];
            if (s.Empty) return;
            if (one && (s.Item == Item.Meal || s.Item == Item.Medicine)) { BagRightClick(slot); return; }
            var store = CurrentStore();
            if (store == null) { game.Log.Add("Nowhere to put it down here.", MessageKind.Info); return; }
            int n = one ? 1 : s.Count;
            int put = store.Put(s.Item, n);
            if (put <= 0) { game.Log.Add(store is FlatStore ? "A hiding flat only keeps doses." : "No room.", MessageKind.Info); return; }
            Bag.Take(p, s.Item, put);
            Bag.Sync(p, game);
            bagDrawn = null;
            DrawInventory();
        }

        /// <summary>From what's open into the bag: as many as fit, or one.</summary>
        void MoveIn(Store store, Item item, int count, bool one)
        {
            var p = game.Player;
            int want = Mathf.Min(one ? 1 : count, Bag.Room(p, item));
            if (item == Item.Doses) want = Mathf.Min(want, Organization.PocketMaxDoses - p.PocketDoses);
            if (item == Item.Packs) want = Mathf.Min(want, Organization.PocketMaxPacks - p.PocketPacks);
            if (item == Item.Fakes) want = Mathf.Min(want, Organization.PocketMaxFakes - p.PocketFakes);
            if (want <= 0) { game.Log.Add("Your bag is full.", MessageKind.Info); return; }
            int got = store.Take(item, want);
            int added = Bag.Give(p, item, got);
            if (added < got) store.Put(item, got - added);
            Bag.Sync(p, game);
            bagDrawn = null;
            DrawInventory();
        }
    }
}
