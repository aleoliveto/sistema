using System.Collections.Generic;
using OSistema.Core;
using OSistema.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OSistema.Views
{
    /// <summary>
    /// Day and night with URP 2D lights: a global light follows the clock, and street lamps and
    /// edicole votive glow after dark. Only lamps near the camera are switched on.
    /// </summary>
    public sealed class WorldLighting : MonoBehaviour
    {
        static readonly Color DayColor = new Color(1f, 0.97f, 0.9f);
        static readonly Color DuskColor = new Color(1f, 0.72f, 0.5f);
        static readonly Color NightColor = new Color(0.45f, 0.52f, 0.9f);

        Game game;
        Light2D global;
        bool ownsGlobal;
        readonly List<Light2D> lamps = new List<Light2D>();
        Camera cam;

        public void Init(Game g)
        {
            game = g;
            cam = Camera.main;

            foreach (var light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                if (light.lightType == Light2D.LightType.Global) global = light;
            if (global == null)
            {
                var go = new GameObject("Global Light");
                go.transform.SetParent(transform, false);
                global = go.AddComponent<Light2D>();
                global.lightType = Light2D.LightType.Global;
                ownsGlobal = true;
            }

            foreach (var def in BuildingDefs.All)
            {
                if (!def.LightSource) continue;
                foreach (var cell in g.Map.PositionsOf(def)) lamps.Add(CreateLamp(cell, def == BuildingDefs.Shrine));
            }
        }

        Light2D CreateLamp(Vector2Int cell, bool candle)
        {
            var go = new GameObject(candle ? "Shrine light" : "Lamp");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = candle ? new Color(1f, 0.65f, 0.35f) : new Color(1f, 0.82f, 0.55f);
            light.intensity = candle ? 0.9f : 1.1f;
            light.pointLightInnerRadius = 0.5f;
            light.pointLightOuterRadius = candle ? 3.5f : 7f;
            light.enabled = false;
            return light;
        }

        void OnDestroy()
        {
            if (ownsGlobal && global != null) Destroy(global.gameObject);
        }

        void LateUpdate()
        {
            if (game == null || global == null) return;
            float day = game.Clock.Daylight;
            float hour = game.Clock.HourF;
            bool evening = hour > 16f && hour < 22f;
            var tint = day > 0.5f
                ? Color.Lerp(evening ? DuskColor : DayColor, DayColor, (day - 0.5f) * 2f)
                : Color.Lerp(NightColor, evening ? DuskColor : DayColor, day * 2f);
            global.color = tint;
            global.intensity = Mathf.Lerp(0.32f, 1.05f, day);

            // Lamps come on at dusk, only where the camera can see them.
            bool lit = day < 0.7f;
            var view = ViewRect();
            float flicker = 0.92f + 0.08f * Mathf.PerlinNoise(Time.time * 3f, 0f);
            foreach (var lamp in lamps)
            {
                bool on = lit && view.Contains(lamp.transform.position);
                if (lamp.enabled != on) lamp.enabled = on;
                if (on && lamp.pointLightOuterRadius < 4f) lamp.intensity = 0.9f * flicker; // candles flicker
            }
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
