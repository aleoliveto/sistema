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
            // Homes (w_home1 = 1x1 sheet, w_home2 = 2x1, w_home4 = 2x2: Tools/ArtGen/cut_world.py).
            ["bed"] = new[] { "Props/w_home4_single_l", "Props/w_home4_single_r", "Props/home_00" },
            ["double_bed"] = new[] { "Props/w_home4_00", "Props/w_home4_01" },
            ["bunk_bed"] = new[] { "Props/w_home4_08" },
            ["table"] = new[] { "Props/w_home4_03" },
            ["dining_set"] = new[] { "Props/w_home4_03" },
            ["checked_table"] = new[] { "Props/w_home2_05" },
            ["sofa"] = new[] { "Props/w_home2_00" },
            ["green_sofa"] = new[] { "Props/w_home2_01" },
            ["corner_sofa"] = new[] { "Props/w_home4_04" },
            ["armchair"] = new[] { "Props/w_home1_14", "Props/home_03" },
            ["wardrobe"] = new[] { "Props/w_home2_02" },
            ["dresser"] = new[] { "Props/w_home2_03" },
            ["sideboard"] = new[] { "Props/w_home2_06" },
            ["tv_cabinet"] = new[] { "Props/w_home2_07" },
            ["tv"] = new[] { "Props/w_home1_05" },
            ["kitchen_counter"] = new[] { "Props/w_home2_04" },
            ["stove"] = new[] { "Props/w_home1_13" },
            ["fridge"] = new[] { "Props/w_home1_04" },
            ["pantry"] = new[] { "Props/home_11" },
            ["rug"] = new[] { "Props/w_home4_05", "Props/w_home4_06" },
            ["plant"] = new[] { "Props/w_home1_07" },
            ["potted_palm"] = new[] { "Props/w_home1_08" },
            ["home_shrine"] = new[] { "Props/w_home1_10" },
            ["safe"] = new[] { "Props/w_home1_09" },
            ["dining_chair"] = new[] { "Props/w_home1_00" },
            ["nightstand"] = new[] { "Props/w_home1_01" },
            ["bookcase"] = new[] { "Props/w_home1_02" },
            ["washing_machine"] = new[] { "Props/w_home1_03" },
            ["floor_lamp"] = new[] { "Props/w_home1_06" },
            ["coat_stand"] = new[] { "Props/w_home1_11" },
            ["laundry_basket"] = new[] { "Props/w_home1_12" },
            ["bar_stool"] = new[] { "Props/w_home1_15" },
            ["card_table"] = new[] { "Props/w_home4_07" },
            // Work tables.
            ["bagging_table"] = new[] { "Props/w_shop4_04" },
            ["rolling_table"] = new[] { "Props/w_shop4_03" },
            ["bunker"] = new[] { "Props/work_09" },
            // Shops (w_shop1 = 1x1, w_shop2 = 2x1 counters, w_shop4 = 2x2).
            ["pizzeria_counter"] = new[] { "Props/w_shop2_00" },
            ["bar_counter"] = new[] { "Props/w_shop2_01" },
            ["salumeria_counter"] = new[] { "Props/w_shop2_02" },
            ["macelleria_counter"] = new[] { "Props/w_shop2_03" },
            ["farmacia_counter"] = new[] { "Props/w_shop2_04" },
            ["tabaccheria_counter"] = new[] { "Props/w_shop2_05" },
            ["glass_vitrine"] = new[] { "Props/w_shop2_02" },
            ["display_table"] = new[] { "Props/w_shop2_06" },
            ["lusso_counter"] = new[] { "Props/w_shop1_12" },
            ["mobili_counter"] = new[] { "Props/w_shop1_00" },
            ["moto_counter"] = new[] { "Props/w_shop1_00" },
            ["till"] = new[] { "Props/w_shop1_00" },
            ["newsstand"] = new[] { "Props/w_shop1_01" },
            ["drinks_fridge"] = new[] { "Props/w_shop1_02" },
            ["cig_stock"] = new[] { "Props/w_shop1_03" },
            ["slot_machine"] = new[] { "Props/w_shop1_04" },
            ["cafe_table"] = new[] { "Props/w_shop1_05" },
            ["ice_cream"] = new[] { "Props/w_shop1_06" },
            ["fruit_stall"] = new[] { "Props/w_shop1_07" },
            ["flour_sacks"] = new[] { "Props/w_shop1_08" },
            ["wine_rack"] = new[] { "Props/w_shop1_09" },
            ["bread_rack"] = new[] { "Props/w_shop1_10" },
            ["pharmacy_cabinet"] = new[] { "Props/w_shop1_11" },
            ["jewel_case"] = new[] { "Props/w_shop1_12" },
            ["clothes_rack"] = new[] { "Props/w_shop1_13" },
            ["clothes_rail"] = new[] { "Props/w_shop1_13" },
            ["tyre_stack"] = new[] { "Props/w_shop1_14" },
            ["parts_bench"] = new[] { "Props/w_shop1_15" },
            ["shop_shelf"] = new[] { "Props/shop_15" },
            ["pizza_oven"] = new[] { "Props/w_shop4_00" },
            ["pool_table"] = new[] { "Props/w_shop4_05" },
            ["pew"] = new[] { "Props/w_shop4_08_one" },
            ["charred"] = new[] { "Props/furn_charred" },
            ["burnt_shelf"] = new[] { "Props/furn_burnt_shelf" },
            // Doors: PropLayer picks the style and turns them; this only says doors have art.
            ["door"] = new[] { "Doors/door_00" },
            // The street.
            ["market_stall"] = new[] { "Props/w_shop4_01", "Props/w_shop4_06", "Props/w_shop4_07" },
            ["bin"] = new[] { "Props/street_01" },
            ["bench"] = new[] { "Props/street_03" },
            ["shrine"] = new[] { "Props/street_08" },
            ["fountain"] = new[] { "Props/street_09" },
            ["bin_bags"] = new[] { "Props/street_02", "Props/work_12" },
            ["bicycle"] = new[] { "Props/street_05" },
            ["crates"] = new[] { "Props/street_07", "Props/work_15" },
            ["potted_plants"] = new[] { "Props/street_14" },
            ["parked_car"] = new[] { "Vehicles/wcar_00", "Vehicles/wcar_01", "Vehicles/wcar_02", "Vehicles/wcar_03", "Vehicles/wcar_04", "Vehicles/wcar_05", "Vehicles/wcar_06", "Vehicles/wcar_07" },
            ["family_scooter"] = new[] { "Vehicles/scooter_00" },   // drawn as the boss's own model and colour (For)
            ["parked_scooter"] = new[] { "Vehicles/scooter_00", "Vehicles/scooter_01", "Vehicles/scooter_02", "Vehicles/scooter_03", "Vehicles/scooter_04", "Vehicles/scooter_05", "Vehicles/scooter_06", "Vehicles/scooter_07", "Vehicles/scooter_08", "Vehicles/scooter_09", "Vehicles/scooter_11", "Vehicles/scooter_12" },
        };

        /// <summary>How big each kind of thing is, in cells along its longest side.</summary>
        public static float SizeOf(BuildingDef d) => d.Id switch
        {
            // The new cars share one canvas as long as the longest (a van): small cars come out smaller.
            "parked_car" => 2.5f,
            "door" => 1.25f,      // the jambs stand a little into the wall either side
            "coat_stand" => 0.7f,
            "laundry_basket" => 0.65f,
            "fridge" => 0.8f,
            "tv" => 0.85f,
            "safe" => 0.7f,
            "home_shrine" => 0.85f,
            "slot_machine" => 0.8f,
            "till" => 0.8f,
            "newsstand" => 0.85f,
            "tyre_stack" => 0.8f,
            "cafe_table" => 0.85f,
            "flour_sacks" => 0.85f,
            "parked_scooter" or "family_scooter" => 1.15f,
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

        /// <summary>The family's scooters are drawn as the boss's model and colour, so you know yours in the street.</summary>
        public static int FamilyModel, FamilyColour;

        /// <summary>The sprite for a def at a cell (the cell picks the variant), or null.</summary>
        public static Sprite For(BuildingDef d, int x, int y)
        {
            if (d == null || !ByDef.TryGetValue(d.Id, out var names)) return null;
            if (d.Id == "family_scooter") return Scooter(FamilyModel, FamilyColour);
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

        /// <summary>The parked scooter at a cell as the sprite number (model × 4 + colour), so a stolen one looks the same ridden.</summary>
        public static int ParkedScooterLook(int x, int y)
        {
            var names = ByDef["parked_scooter"];
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            var n = names[(int)(h % (uint)names.Length)];
            return int.TryParse(n.Substring(n.Length - 2), out int i) ? i : 0;
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
