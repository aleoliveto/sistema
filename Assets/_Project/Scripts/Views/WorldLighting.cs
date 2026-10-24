using System.Collections.Generic;
using OSistema.Core;
using OSistema.Crime;
using OSistema.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OSistema.Views
{
    /// <summary>
    /// Day and night with URP 2D lights. Nights in the vicoli are dark and blue, broken by warm
    /// sodium-yellow pools under the wall lamps: not every lamp works, some stutter, shrines flicker
    /// with candles, a window glows here and there, shops spill light until closing. Rain dims and
    /// cools everything. Only lights near the camera are switched on.
    /// </summary>
    public sealed class WorldLighting : MonoBehaviour
    {
        static readonly Color DayColor = new Color(1f, 0.97f, 0.9f);
        static readonly Color DuskColor = new Color(1f, 0.7f, 0.48f);
        static readonly Color NightColor = new Color(0.36f, 0.43f, 0.78f);
        static readonly Color RainColor = new Color(0.72f, 0.78f, 0.88f);
        static readonly Color Sodium = new Color(1f, 0.72f, 0.38f);
        static readonly Color Candle = new Color(1f, 0.6f, 0.3f);
        static readonly Color Window = new Color(1f, 0.78f, 0.5f);
        static readonly Color ShopLight = new Color(1f, 0.88f, 0.7f);

        enum Kind { Lamp, Candle, Window, Shop }
        enum Health { Good, Faulty, Dead }

        sealed class Lamp
        {
            public Light2D Light;
            public Kind Kind;
            public Health Health;
            public float Base, Seed;
            public Business Shop;
            public int OffAfter;   // windows: the hour the family goes to bed (24+ = after midnight)
        }

        /// <summary>True after dark (or under heavy rain): headlights on.</summary>
        public static bool Night;

        Game game;
        Light2D global;
        bool ownsGlobal;
        readonly List<Lamp> lamps = new List<Lamp>();
        Camera cam;

        public void Init(Game g)
        {
            game = g;
            cam = Camera.main;

            foreach (var light in FindObjectsByType<Light2D>())
                if (light.lightType == Light2D.LightType.Global) global = light;
            if (global == null)
            {
                var go = new GameObject("Global Light");
                go.transform.SetParent(transform, false);
                global = go.AddComponent<Light2D>();
                global.lightType = Light2D.LightType.Global;
                ownsGlobal = true;
            }

            gameObject.AddComponent<WeatherView>().Init(g);

            var map = g.Map;
            var rng = new System.Random(g.Map.Width * 31 + 7);
            foreach (var def in BuildingDefs.All)
            {
                if (!def.LightSource) continue;
                foreach (var cell in map.PositionsOf(def))
                    Add(OSistema.Pawns.Mover.CellCenter(cell), def == BuildingDefs.Shrine ? Kind.Candle : Kind.Lamp, rng);
            }
            // Lamps on brackets on the walls of the vicoli, shining into the lane.
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var decor = map.GetDecor(x, y);
                if (decor != MapDecor.WallLamp && decor != MapDecor.Shrine) continue;
                var out_ = OpenSide(map, x, y);
                var at = new Vector2(x + 0.5f, y + 0.5f) + (Vector2)out_ * 0.75f;
                if (decor == MapDecor.Shrine) { Add(at, Kind.Candle, rng); continue; }
                Add(at, Kind.Lamp, rng);
                Bracket(new Vector2(x + 0.5f, y + 0.5f) + (Vector2)out_ * 0.45f, out_);
            }
            // Windows of homes, and shops lit until closing.
            foreach (var p in g.Properties.All)
            {
                if (p.Kind != PropertyKind.Home || rng.NextDouble() > 0.6) continue;
                var l = Add(OSistema.Pawns.Mover.CellCenter(p.Door), Kind.Window, rng);
                l.OffAfter = 22 + rng.Next(5);
            }
            // Inside the family's own basso: a warm ceiling bulb.
            var home = g.PlayerBase;
            var bulb = Add(home.center, Kind.Window, rng);
            bulb.OffAfter = 26;
            bulb.Base = 0.9f;
            bulb.Light.pointLightInnerRadius = 1.5f;
            bulb.Light.pointLightOuterRadius = Mathf.Max(home.width, home.height) * 0.75f;
            foreach (var b in g.Org.Businesses)
            {
                var l = Add(OSistema.Pawns.Mover.CellCenter(b.Cell), Kind.Shop, rng);
                l.Shop = b;
            }
        }

        static Vector2Int OpenSide(Map map, int x, int y)
        {
            foreach (var d in new[] { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right })
            {
                int nx = x + d.x, ny = y + d.y;
                if (!map.InBounds(nx, ny) || map.GetBuilding(nx, ny) == BuildingDefs.Wall) continue;
                var t = map.GetTerrain(nx, ny);
                if (t == TerrainType.Sidewalk || t == TerrainType.Road || t == TerrainType.Paving) return d;
            }
            return Vector2Int.down;
        }

        /// <summary>The painted wrought-iron lamp, fixed to the wall and reaching into the lane.</summary>
        void Bracket(Vector2 at, Vector2Int dir)
        {
            var sprite = PropArt.Load("Props/street_00");
            if (sprite == null) return;
            var go = new GameObject("Wall lamp");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            // The art reaches right from a bracket on the left.
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = SpriteFactory.LitMaterial;
            sr.sortingOrder = Overhead.Order;   // above the street: people pass under it
            HeightShadow.Add(sr, 2.2f, 0.3f);
            Overhead.Register(new Rect(at.x - 0.45f, at.y - 0.45f, 0.9f, 0.9f), sr);
        }

        Lamp Add(Vector2 at, Kind kind, System.Random rng)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            var lamp = new Lamp { Light = light, Kind = kind, Seed = (float)rng.NextDouble() * 100f };
            switch (kind)
            {
                case Kind.Lamp:
                    light.color = Sodium;
                    lamp.Base = 1.7f;
                    light.pointLightInnerRadius = 1.1f;
                    light.pointLightOuterRadius = 6.5f + (float)rng.NextDouble() * 1.5f;
                    light.falloffIntensity = 0.55f;
                    double h = rng.NextDouble();
                    lamp.Health = h < 0.1 ? Health.Dead : h < 0.22 ? Health.Faulty : Health.Good;
                    break;
                case Kind.Candle:
                    light.color = Candle;
                    lamp.Base = 0.85f;
                    light.pointLightInnerRadius = 0.2f;
                    light.pointLightOuterRadius = 3f;
                    break;
                case Kind.Window:
                    light.color = Window;
                    lamp.Base = 0.55f;
                    light.pointLightInnerRadius = 0.3f;
                    light.pointLightOuterRadius = 2.6f;
                    break;
                default:
                    light.color = ShopLight;
                    lamp.Base = 0.9f;
                    light.pointLightInnerRadius = 1f;
                    light.pointLightOuterRadius = 4.5f;
                    break;
            }
            light.intensity = lamp.Base;
            light.enabled = false;
            lamps.Add(lamp);
            return lamp;
        }

        void OnDestroy()
        {
            if (ownsGlobal && global != null) Destroy(global.gameObject);
        }

        float wet, wetSet = -1f;

        void LateUpdate()
        {
            if (game == null || global == null) return;
            float day = game.Clock.Daylight;
            float hour = game.Clock.HourF;
            float rain = game.Weather.Rain;
            Sun.Update(hour, day, rain);
            // The lava stone darkens and shines in the rain, and dries slowly after (scaled with game speed).
            float speed = Mathf.Max(1f, game.Ticks.Speed);
            wet = Mathf.MoveTowards(wet, rain > 0.15f ? Mathf.Clamp01(rain * 1.6f) : 0f, Time.deltaTime * speed * (rain > 0.15f ? 0.12f : 0.015f));
            if (Mathf.Abs(wet - wetSet) > 0.01f) { wetSet = wet; SpriteFactory.MapMaterial.SetFloat("_Wet", wet); }
            bool evening = hour > 16f && hour < 22f;
            var tint = day > 0.5f
                ? Color.Lerp(evening ? DuskColor : DayColor, DayColor, (day - 0.5f) * 2f)
                : Color.Lerp(NightColor, evening ? DuskColor : DayColor, day * 2f);
            tint = Color.Lerp(tint, tint * RainColor, rain * 0.8f);
            global.color = tint;
            global.intensity = Mathf.Lerp(0.2f, 1.05f, day) * (1f - rain * 0.3f);

            // Lamps come on at dusk (earlier under rain), only where the camera can see them.
            bool dark = day < 0.7f + rain * 0.15f;
            Night = dark;
            var view = ViewRect();
            float t = Time.time;
            foreach (var lamp in lamps)
            {
                bool on = dark && view.Contains(lamp.Light.transform.position) && Lit(lamp, hour, t);
                if (lamp.Light.enabled != on) lamp.Light.enabled = on;
                if (!on) continue;
                float k = 1f;
                switch (lamp.Kind)
                {
                    case Kind.Candle:
                        k = 0.82f + 0.18f * Mathf.PerlinNoise(t * 4f, lamp.Seed);
                        break;
                    case Kind.Lamp:
                        // A faint shimmer; a faulty one stutters and drops out now and then.
                        k = 0.96f + 0.04f * Mathf.PerlinNoise(t * 1.5f, lamp.Seed);
                        if (lamp.Health == Health.Faulty)
                        {
                            float n = Mathf.PerlinNoise(t * 2.2f, lamp.Seed);
                            if (n < 0.3f) k *= Mathf.PerlinNoise(t * 30f, lamp.Seed) > 0.5f ? 0.15f : 0.8f;
                        }
                        break;
                }
                // Light scatters in the rain: softer, a little bigger, dimmer.
                lamp.Light.intensity = lamp.Base * k * (1f - rain * 0.15f);
            }
        }

        bool Lit(Lamp lamp, float hour, float t)
        {
            switch (lamp.Kind)
            {
                case Kind.Lamp: return lamp.Health != Health.Dead;
                case Kind.Window:
                    float h = hour < 12f ? hour + 24f : hour;   // after midnight counts as 24+
                    return h >= 18f && h < lamp.OffAfter;
                case Kind.Shop:
                    return lamp.Shop != null && lamp.Shop.Status != BusinessStatus.Closed && hour >= 7.5f && hour < 21f;
                default: return true;
            }
        }

        /// <summary>A scooter's headlight: a cone of light ahead of it. Points the way its transform's up points.</summary>
        public static Light2D Headlight(Transform parent)
        {
            var go = new GameObject("Headlight");
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(1f, 0.94f, 0.8f);
            l.intensity = 1.2f;
            l.pointLightInnerRadius = 0.4f;
            l.pointLightOuterRadius = 5.5f;
            l.pointLightInnerAngle = 30f;
            l.pointLightOuterAngle = 75f;
            l.falloffIntensity = 0.6f;
            l.enabled = false;
            return l;
        }

        /// <summary>Turns a headlight on at night, aimed along the heading (degrees, 0 = up).</summary>
        public static void AimHeadlight(Light2D l, bool riding, Vector2 at, float heading)
        {
            if (l == null) return;
            bool on = riding && Night;
            if (l.enabled != on) l.enabled = on;
            if (!on) return;
            var fwd = new Vector2(-Mathf.Sin(heading * Mathf.Deg2Rad), Mathf.Cos(heading * Mathf.Deg2Rad));
            l.transform.position = new Vector3(at.x + fwd.x * 0.5f, at.y + fwd.y * 0.5f, 0f);
            l.transform.rotation = Quaternion.Euler(0f, 0f, heading);
        }

        Rect ViewRect()
        {
            if (cam == null) cam = Camera.main;
            float h = cam.orthographicSize + 8f, w = h * cam.aspect + 8f;
            var p = cam.transform.position;
            return new Rect(p.x - w, p.y - h, w * 2f, h * 2f);
        }
    }
}
