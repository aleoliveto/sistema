using System.Collections.Generic;
using OSistema.World;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// The painted furniture, shop fittings, street props and vehicles in Resources/Props and
    /// Resources/Vehicles, cut from the art sheets. A def with art here is drawn as a sprite over the
    /// map (see <see cref="PropLayer"/>) instead of being painted into the map texture.
    /// </summary>
    public static class PropArt
    {
        // Def id → art name(s). Several names: one is picked per cell, so not every bed is the same bed.
        static readonly Dictionary<string, string[]> ByDef = new Dictionary<string, string[]>
        {
            ["bed"] = new[] { "Props/home_00" },
            ["table"] = new[] { "Props/home_04" },
            ["sofa"] = new[] { "Props/home_02" },
            ["armchair"] = new[] { "Props/home_03" },
            ["wardrobe"] = new[] { "Props/home_06" },
            ["tv"] = new[] { "Props/home_07" },
            ["kitchen_counter"] = new[] { "Props/home_08" },
            ["stove"] = new[] { "Props/home_09" },
            ["fridge"] = new[] { "Props/home_10" },
            ["pantry"] = new[] { "Props/home_11" },
            ["rug"] = new[] { "Props/home_12" },
            ["plant"] = new[] { "Props/home_13" },
            ["home_shrine"] = new[] { "Props/home_14" },
            ["safe"] = new[] { "Props/home_15" },
            ["bagging_table"] = new[] { "Props/work_00" },
            ["rolling_table"] = new[] { "Props/work_01" },
            ["bunker"] = new[] { "Props/work_09" },
            ["pizzeria_counter"] = new[] { "Props/shop_01" },
            ["bar_counter"] = new[] { "Props/shop_02" },
            ["tabaccheria_counter"] = new[] { "Props/shop_04" },
            ["salumeria_counter"] = new[] { "Props/shop_05" },
            ["farmacia_counter"] = new[] { "Props/shop_06" },
            ["macelleria_counter"] = new[] { "Props/shop_07" },
            ["lusso_counter"] = new[] { "Props/shop_09" },
            ["mobili_counter"] = new[] { "Props/shop_11" },
            ["moto_counter"] = new[] { "Props/shop_11" },
            ["pizza_oven"] = new[] { "Props/shop_00" },
            ["cafe_table"] = new[] { "Props/shop_03" },
            ["clothes_rack"] = new[] { "Props/shop_10" },
            ["fruit_stall"] = new[] { "Props/shop_12" },
            ["ice_cream"] = new[] { "Props/shop_14" },
            ["shop_shelf"] = new[] { "Props/shop_15" },
            ["bin"] = new[] { "Props/street_01" },
            ["bench"] = new[] { "Props/street_03" },
            ["market_stall"] = new[] { "Props/street_06" },
            ["shrine"] = new[] { "Props/street_08" },
            ["fountain"] = new[] { "Props/street_09" },
            ["bin_bags"] = new[] { "Props/street_02", "Props/work_12" },
            ["bicycle"] = new[] { "Props/street_05" },
            ["crates"] = new[] { "Props/street_07", "Props/work_15" },
            ["potted_plants"] = new[] { "Props/street_14" },
            // Interiors (RoomPlanner): painted pieces that had no use yet, and the ones from Tools/ArtGen/furniture.py.
            ["double_bed"] = new[] { "Props/home_01" },
            ["checked_table"] = new[] { "Props/home_05" },
            ["till"] = new[] { "Props/shop_11" },
            ["newsstand"] = new[] { "Props/shop_13" },
            ["green_sofa"] = new[] { "Props/shop_08" },
            ["slot_machine"] = new[] { "Props/work_04" },
            ["cig_stock"] = new[] { "Props/work_02" },
            ["clothes_rail"] = new[] { "Props/work_06" },
            ["card_table"] = new[] { "Props/work_03" },
            ["drinks_fridge"] = new[] { "Props/work_13" },
            ["parts_bench"] = new[] { "Props/work_14" },
            ["dining_chair"] = new[] { "Props/furn_dining_chair" },
            ["nightstand"] = new[] { "Props/furn_nightstand" },
            ["dresser"] = new[] { "Props/furn_dresser" },
            ["bookcase"] = new[] { "Props/furn_bookcase" },
            ["washing_machine"] = new[] { "Props/furn_washing_machine" },
            ["floor_lamp"] = new[] { "Props/furn_floor_lamp" },
            ["bar_stool"] = new[] { "Props/furn_bar_stool" },
            ["wine_rack"] = new[] { "Props/furn_wine_rack" },
            ["flour_sacks"] = new[] { "Props/furn_flour_sacks" },
            ["glass_vitrine"] = new[] { "Props/furn_glass_vitrine" },
            ["pharmacy_cabinet"] = new[] { "Props/furn_pharmacy_cabinet" },
            ["jewel_case"] = new[] { "Props/furn_jewel_case" },
            ["display_table"] = new[] { "Props/furn_display_table" },
            ["bread_rack"] = new[] { "Props/furn_bread_rack" },
            ["tyre_stack"] = new[] { "Props/furn_tyre_stack" },
            ["charred"] = new[] { "Props/furn_charred" },
            ["potted_palm"] = new[] { "Props/furn_potted_palm" },
            ["sideboard"] = new[] { "Props/furn_sideboard" },
            ["burnt_shelf"] = new[] { "Props/furn_burnt_shelf" },
            ["parked_car"] = new[] { "Vehicles/car_00", "Vehicles/car_01", "Vehicles/car_02", "Vehicles/car_03", "Vehicles/car_04", "Vehicles/car_05", "Vehicles/car_06", "Vehicles/car_07", "Vehicles/car_08", "Vehicles/car_09", "Vehicles/car_15" },
            ["parked_scooter"] = new[] { "Vehicles/scooter_00", "Vehicles/scooter_01", "Vehicles/scooter_02", "Vehicles/scooter_03", "Vehicles/scooter_04", "Vehicles/scooter_05", "Vehicles/scooter_06", "Vehicles/scooter_07", "Vehicles/scooter_08", "Vehicles/scooter_09", "Vehicles/scooter_11", "Vehicles/scooter_12" },
        };

        /// <summary>How big each kind of thing is, in cells along its longest side.</summary>
        public static float SizeOf(BuildingDef d) => d.Id switch
        {
            "parked_car" => 2.1f,
            "parked_scooter" => 1.15f,
            "rug" => 1.15f,
            "bed" => 1.08f,
            "sofa" => 1.15f,
            "market_stall" => 1.3f,
            "fountain" => 1.2f,
            "plant" => 0.75f,
            "potted_plants" => 0.85f,
            "bicycle" => 0.95f,
            "dining_chair" => 0.55f,
            "bar_stool" => 0.5f,
            "floor_lamp" => 0.6f,
            "nightstand" => 0.7f,
            "washing_machine" => 0.85f,
            "potted_palm" => 1.1f,
            _ => 1.0f,
        };

        /// <summary>
        /// Scale for a piece covering w x h cells (unturned): fills the cells, but never squashes the
        /// painting more than a third out of its proportions.
        /// </summary>
        public static Vector3 FitScale(Sprite s, Vector2Int cells)
        {
            var b = s.bounds.size;   // the longer side is one unit
            float w = cells.x * 0.96f, h = cells.y * 0.96f;
            float sx = w / b.x, sy = h / b.y;
            float lo = Mathf.Min(sx, sy), hi = Mathf.Max(sx, sy);
            if (hi > lo * 1.35f) hi = lo * 1.35f;
            return sx >= sy ? new Vector3(hi, lo, 1f) : new Vector3(lo, hi, 1f);
        }

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static bool Has(BuildingDef d) => d != null && ByDef.ContainsKey(d.Id) && Load(ByDef[d.Id][0]) != null;

        /// <summary>The sprite for a def at a cell (the cell picks the variant), or null.</summary>
        public static Sprite For(BuildingDef d, int x, int y)
        {
            if (d == null || !ByDef.TryGetValue(d.Id, out var names)) return null;
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            return Load(names[(int)(h % (uint)names.Length)]) ?? Load(names[0]);
        }

        public static Sprite Load(string path)
        {
            // Unity throws loaded assets away between Play sessions: never trust a stale cache.
            if (cache.TryGetValue(path, out var s) && s != null) return s;
            var tex = Resources.Load<Texture2D>(path);
            s = null;
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), Mathf.Max(tex.width, tex.height));
            }
            cache[path] = s;
            return s;
        }

        /// <summary>A moving scooter, by model (0 cheap step-through ... 3 maxi) and colour.</summary>
        public static Sprite Scooter(int model, int colour) => Load($"Vehicles/scooter_{Mathf.Clamp(model, 0, 3) * 4 + ((colour % 4) + 4) % 4:00}");

        /// <summary>
        /// Puts a scooter under a rider: the painted one if there is art (drawn nose down, so turned
        /// round), the old tinted shape otherwise. Heading is in degrees, 0 = nose up.
        /// </summary>
        public static void DressScooter(SpriteRenderer sr, float heading, int model, int colour, Color fallbackTint)
        {
            var art = Scooter(model, colour);
            if (art != null)
            {
                sr.sprite = art;
                sr.color = Color.white;
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, heading + 180f);
                // Big enough to see under the rider: a step-through is longer than a person, a maxi much longer.
                float len = 1.3f + model * 0.22f;
                sr.transform.localScale = new Vector3(len, len, 1f);
                // The rider sits on the seat, behind the middle: so the scooter sits forward of him.
                var fwd = new Vector2(-Mathf.Sin(heading * Mathf.Deg2Rad), Mathf.Cos(heading * Mathf.Deg2Rad));
                sr.transform.localPosition = new Vector3(fwd.x, fwd.y, 0f) * len * 0.26f;
            }
            else
            {
                sr.sprite = SpriteFactory.ScooterBody;
                sr.color = fallbackTint;
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, heading);
                sr.transform.localScale = Vector3.one;
                sr.transform.localPosition = Vector3.zero;
            }
        }

        public static Texture2D Icon(int i) => Resources.Load<Texture2D>($"UI/Icons/icon_{i:00}");
    }
}
