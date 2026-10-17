using System;
using OSistema.Core;
using OSistema.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// Work done by hand at the tables: weighing and bagging doses, rolling and boxing cigarettes.
    /// Time keeps running while you work. Later, crew on the Bagger and Roller jobs do it for you.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>A surface drawn with Painter2D every frame.</summary>
        sealed class Canvas : VisualElement
        {
            public Action<Painter2D, Rect> Paint;

            public Canvas(float w, float h)
            {
                style.width = w;
                style.height = h;
                generateVisualContent += ctx => Paint?.Invoke(ctx.painter2D, contentRect);
            }
        }

        static void FillRect(Painter2D p, float x, float y, float w, float h, Color c)
        {
            p.fillColor = c;
            p.BeginPath();
            p.MoveTo(new Vector2(x, y));
            p.LineTo(new Vector2(x + w, y));
            p.LineTo(new Vector2(x + w, y + h));
            p.LineTo(new Vector2(x, y + h));
            p.ClosePath();
            p.Fill();
        }

        static void FillCircle(Painter2D p, Vector2 c, float r, Color col)
        {
            p.fillColor = col;
            p.BeginPath();
            p.Arc(c, r, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Fill();
        }

        static void Line(Painter2D p, Vector2 a, Vector2 b, float w, Color c)
        {
            p.strokeColor = c;
            p.lineWidth = w;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        VisualElement minigameDim;
        Action<float> minigameTick;
        Canvas[] minigameCanvases = Array.Empty<Canvas>();

        public bool MinigameOpen => minigameDim != null;

        VisualElement OpenMinigame(string title, string subtitle)
        {
            CloseMinigame();
            if (game.Ticks.Speed > 1) game.Ticks.SetSpeed(1);   // work happens in real time
            if (game.Ticks.Speed == 0) game.Ticks.SetSpeed(1);
            minigameDim = Ui.Box("dim").Into(modalLayer);
            var panel = Ui.Box("panel", "minigame").Into(minigameDim);
            var head = Ui.Row(panel);
            Ui.Text(title, head, "title", "gold");
            Ui.Box("spacer").Into(head);
            Ui.Btn("Done  [Esc]", CloseMinigame, head, "btn--small");
            Ui.Text(subtitle, panel, "small", "muted", "wrap");
            return panel;
        }

        void CloseMinigame()
        {
            if (minigameDim == null) return;
            minigameDim.RemoveFromHierarchy();
            minigameDim = null;
            minigameTick = null;
            minigameCanvases = Array.Empty<Canvas>();
            RefreshAll();
        }

        void UpdateMinigame()
        {
            if (minigameDim == null) return;
            if (game == null || game.Player == null || !game.Player.CanAct)
            {
                CloseMinigame();
                return;
            }
            minigameTick?.Invoke(Time.unscaledDeltaTime);
            foreach (var c in minigameCanvases) c.MarkDirtyRepaint();
        }

        static bool MouseHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
        static bool SpacePressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        // ---------- Bagging ----------

        public void OpenBagging()
        {
            var org = game.Org;
            var s = game.Stock;
            var panel = OpenMinigame("THE BAGGING TABLE",
                $"Hold the mouse on the block to scrape product onto the scale. A dose is {Prices.DoseDg / 10f:0.0} g. Light bags cheat the customers and they talk; heavy bags give it away. Space seals the bag.");

            float w = 0f, shown = 0f, hold = 0f;
            bool pouring = false;
            int perfect = 0, ok = 0, light = 0, heavy = 0;
            string last = "";

            var row = Ui.Row(panel);
            row.style.justifyContent = Justify.SpaceAround;
            row.style.marginTop = 10;

            var brickCol = Ui.Box("col").Into(row);
            brickCol.style.alignItems = Align.Center;
            var brick = new Canvas(200, 200).Into(brickCol);
            var brickLabel = Ui.Text("", brickCol, "small");
            brick.RegisterCallback<PointerDownEvent>(_ => { pouring = true; hold = 0f; });
            brick.Paint = (p, r) =>
            {
                float frac = Mathf.Clamp01(s.BulkDg / 10000f);
                float bw = 60f + 120f * Mathf.Sqrt(frac), bh = 50f + 80f * Mathf.Sqrt(frac);
                float x = (r.width - bw) / 2f, y = (r.height - bh) / 2f;
                FillRect(p, x + 6, y + 8, bw, bh, new Color(0, 0, 0, 0.35f));
                FillRect(p, x, y, bw, bh, new Color(0.93f, 0.92f, 0.9f));
                FillRect(p, x, y, bw, 6, new Color(0.99f, 0.99f, 0.98f));
                FillRect(p, x + bw - 6, y, 6, bh, new Color(0.82f, 0.81f, 0.8f));
                // cling film sheen and the scraped corner
                Line(p, new Vector2(x + 8, y + bh * 0.3f), new Vector2(x + bw * 0.5f, y + 10), 2f, new Color(1, 1, 1, 0.6f));
                if (pouring) FillCircle(p, new Vector2(x + bw - 4, y + bh - 4), 10f, new Color(0.96f, 0.96f, 0.95f));
            };

            var scaleCol = Ui.Box("col").Into(row);
            scaleCol.style.alignItems = Align.Center;
            var scale = new Canvas(260, 220).Into(scaleCol);
            var readout = Ui.Text("", scaleCol, "minigame__readout");
            scale.Paint = (p, r) =>
            {
                var c = new Vector2(r.width / 2f, r.height * 0.62f);
                FillRect(p, 30, r.height * 0.72f, r.width - 60, 40, new Color(0.16f, 0.16f, 0.18f));
                FillCircle(p, c, 78f, new Color(0.62f, 0.64f, 0.66f));
                FillCircle(p, c, 72f, new Color(0.78f, 0.8f, 0.82f));
                // the open bag, and the little heap growing in it
                FillRect(p, c.x - 34, c.y - 44, 68, 80, new Color(0.92f, 0.95f, 0.98f, 0.55f));
                FillRect(p, c.x - 34, c.y - 44, 68, 6, new Color(0.5f, 0.55f, 0.65f, 0.8f));
                float heap = 6f + 22f * Mathf.Sqrt(Mathf.Max(0f, w));
                if (w > 0.005f) FillCircle(p, c + new Vector2(0, 10), heap, new Color(0.97f, 0.97f, 0.96f));
                if (!s.DigitalScale)
                {
                    // An old analog scale: a needle that wobbles and lags.
                    var dial = new Vector2(r.width / 2f, 34);
                    FillCircle(p, dial, 30f, new Color(0.95f, 0.93f, 0.86f));
                    for (int k = 0; k <= 10; k++)
                    {
                        float a = Mathf.Lerp(200f, 340f, k / 10f) * Mathf.Deg2Rad;
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Line(p, dial + dir * 22f, dial + dir * (k % 5 == 0 ? 29f : 26f), 1.5f, new Color(0.2f, 0.2f, 0.2f));
                    }
                    float na = Mathf.Lerp(200f, 340f, Mathf.Clamp01(shown / 1f)) * Mathf.Deg2Rad;
                    Line(p, dial, dial + new Vector2(Mathf.Cos(na), Mathf.Sin(na)) * 26f, 2f, new Color(0.75f, 0.1f, 0.1f));
                }
            };

            var right = Ui.Box("col").Into(row);
            right.style.width = 230;
            var seal = Ui.Btn("Seal the bag  [Space]", () => Seal(), right);
            Ui.Btn("Scrape a little back", () => { w = Mathf.Max(0f, w - 0.04f); }, right, "btn--small").style.marginTop = 6;
            var stats = Ui.Text("", right, "small", "wrap");
            stats.style.marginTop = 10;
            var result = Ui.Text("", right, "bold", "wrap");
            result.style.marginTop = 6;

            void Seal()
            {
                if (w < 0.1f) { last = "<color=#F2B866>The bag's empty.</color>"; return; }
                if (!org.CanMakeDose(game)) return;
                float target = Prices.DoseDg / 10f, err = w - target;
                if (Mathf.Abs(err) <= 0.03f) { perfect++; last = $"<color=#8FD18A>{w:0.00} g — spot on.</color>"; }
                else if (Mathf.Abs(err) <= 0.08f) { ok++; last = $"{w:0.00} g — near enough."; }
                else if (err < 0f) { light++; last = $"<color=#F07A6E>{w:0.00} g — light. Someone will notice.</color>"; }
                else { heavy++; last = $"<color=#F2B866>{w:0.00} g — heavy. You gave product away.</color>"; }
                org.MakeDose(w, game);
                w = 0f;
                shown = 0f;
            }

            minigameCanvases = new[] { brick, scale };
            minigameTick = dt =>
            {
                if (!MouseHeld) pouring = false;
                bool can = org.CanMakeDose(game);
                if (pouring && can)
                {
                    hold += dt;
                    float rate = 0.08f + 0.35f * Mathf.Clamp01(hold / 1.5f);
                    w = Mathf.Min(w + rate * dt, s.BulkDg / 10f);
                }
                if (s.DigitalScale) shown = w;
                else shown = Mathf.Lerp(shown, w + (Mathf.PerlinNoise(Time.time * 3f, 0f) - 0.5f) * 0.12f, dt * 4f);
                readout.text = s.DigitalScale ? $"{w:0.00} g" : $"≈ {Mathf.Max(0f, shown):0.0} g";
                if (SpacePressed) Seal();
                seal.SetEnabled(can);
                brickLabel.text = $"Product: {s.BulkDg / 10f:0.0} g · bags: {s.Baggies}";
                stats.text = $"Spot on {perfect} · near {ok} · light {light} · heavy {heavy}\nDoses ready: <b>{s.Product}</b>\nHow honest your doses are: {s.DoseQuality * 100f:0}%";
                result.text = !can ? (s.Baggies <= 0 ? "<color=#F07A6E>No bags left. The salumeria sells them.</color>" : "<color=#F07A6E>No product left.</color>") : last;
            };
        }

        // ---------- Rolling ----------

        public void OpenRolling()
        {
            var org = game.Org;
            var s = game.Stock;
            var panel = OpenMinigame("THE ROLLING TABLE",
                $"One pack of 20 at a time. Fill the trough to the line (about {Prices.TobaccoPerPack} g), pack it down, roll the papers in one steady pull, then box them. Good cigarettes sell for more.");

            int stage = 0;               // 0 fill, 1 tamp, 2 roll, 3 box
            float fill = 0f, hold = 0f, tampT = 0f, rollProgress = 0f, good = 0f, bad = 0f, lastX = 0f, speed = 0f;
            int taps = 0, hits = 0;
            bool pouring = false, dragging = false;
            string last = "";
            float zoneLo = s.RollingMachine ? 0.36f : 0.42f, zoneHi = s.RollingMachine ? 0.64f : 0.58f;
            float speedLo = s.RollingMachine ? 180f : 250f, speedHi = s.RollingMachine ? 950f : 700f;

            var row = Ui.Row(panel);
            row.style.justifyContent = Justify.SpaceAround;
            row.style.marginTop = 10;

            var left = new Canvas(220, 220).Into(row);
            var brand = Ui.Text("ROSSE", left, "pack__brand");
            var warning = Ui.Text("IL FUMO\nUCCIDE", left, "pack__warning");
            var mid = new Canvas(300, 220).Into(row);
            var right = Ui.Box("col").Into(row);
            right.style.width = 220;
            var step = Ui.Text("", right, "bold", "wrap");
            var action = Ui.Btn("", () => Action(), right);
            action.style.marginTop = 6;
            var stats = Ui.Text("", right, "small", "wrap");
            stats.style.marginTop = 10;
            var result = Ui.Text("", right, "small", "wrap");

            left.RegisterCallback<PointerDownEvent>(_ => { if (stage == 0) { pouring = true; hold = 0f; } });
            mid.RegisterCallback<PointerDownEvent>(e =>
            {
                if (stage != 2) return;
                dragging = true;
                lastX = Mouse.current.position.ReadValue().x;
            });

            left.Paint = (p, r) =>
            {
                if (stage < 3)
                {
                    // The tobacco pouch.
                    FillRect(p, 40, 50, 140, 130, new Color(0.45f, 0.3f, 0.18f));
                    FillRect(p, 40, 50, 140, 18, new Color(0.35f, 0.22f, 0.12f));
                    for (int k = 0; k < 40; k++)
                        FillRect(p, 50 + (k * 37) % 120, 75 + (k * 53) % 95, 6, 2, new Color(0.62f, 0.44f, 0.24f));
                    if (pouring) FillCircle(p, new Vector2(180, 180), 8f, new Color(0.6f, 0.42f, 0.22f));
                }
                else DrawPack(p, r);
            };
            mid.Paint = (p, r) =>
            {
                // The trough of the rolling machine, with the fill line.
                FillRect(p, 20, 60, 260, 70, new Color(0.25f, 0.25f, 0.27f));
                float f = Mathf.Clamp01(fill / 20f);
                FillRect(p, 26, 66 + 58 * (1f - f), 248, 58 * f, new Color(0.6f, 0.42f, 0.22f));
                float lo = 66 + 58 * (1f - 15f / 20f), hi = 66 + 58 * (1f - 13f / 20f);
                FillRect(p, 20, lo, 6, hi - lo, new Color(0.4f, 0.8f, 0.4f));
                FillRect(p, 274, lo, 6, hi - lo, new Color(0.4f, 0.8f, 0.4f));
                if (stage == 1)
                {
                    // The tamping bar: hit the green.
                    FillRect(p, 20, 150, 260, 18, new Color(0.15f, 0.15f, 0.16f));
                    FillRect(p, 20 + 260 * zoneLo, 150, 260 * (zoneHi - zoneLo), 18, new Color(0.3f, 0.6f, 0.3f));
                    float m = 0.5f + 0.5f * Mathf.Sin(tampT * 5.7f);
                    FillRect(p, 20 + 260 * m - 2, 146, 4, 26, new Color(0.95f, 0.85f, 0.4f));
                }
                if (stage >= 2)
                {
                    // The handle, pulled across.
                    float x = 30 + 240 * Mathf.Clamp01(rollProgress);
                    FillRect(p, 20, 186, 260, 6, new Color(0.4f, 0.4f, 0.42f));
                    bool inBand = speed >= speedLo && speed <= speedHi;
                    FillRect(p, x - 10, 176, 20, 26, dragging ? (inBand ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.9f, 0.4f, 0.3f)) : new Color(0.8f, 0.8f, 0.82f));
                    for (int k = 0; k < 20 && k < rollProgress * 20; k++)
                        FillRect(p, 30 + k * 12.5f, 140, 8, 34, new Color(0.96f, 0.95f, 0.92f));
                }
            };

            void Action()
            {
                switch (stage)
                {
                    case 0:
                        if (fill >= 6f) stage = 1;
                        break;
                    case 1:
                        float m = 0.5f + 0.5f * Mathf.Sin(tampT * 5.7f);
                        taps++;
                        if (m >= zoneLo && m <= zoneHi) hits++;
                        if (taps >= 3) stage = 2;
                        break;
                    case 3:
                        float fillScore = 1f - Mathf.Clamp01(Mathf.Abs(fill - Prices.TobaccoPerPack) / 4f);
                        float rollScore = good + bad > 0f ? good / (good + bad) : 0.5f;
                        float q = 0.4f * fillScore + 0.25f * hits / 3f + 0.35f * rollScore;
                        org.MakePack(Mathf.RoundToInt(fill), q, game);
                        last = q > 0.8f ? "<color=#8FD18A>A beautiful pack. They'll pass for the real thing.</color>"
                            : q > 0.55f ? "A decent pack." : "<color=#F2B866>Loose and lumpy. It'll sell, cheap.</color>";
                        stage = 0;
                        fill = 0f;
                        taps = hits = 0;
                        rollProgress = good = bad = 0f;
                        break;
                }
            }

            minigameCanvases = new[] { left, mid };
            minigameTick = dt =>
            {
                bool can = org.CanMakePack(game);
                if (!MouseHeld) { pouring = false; dragging = false; }
                if (stage == 0 && pouring && can)
                {
                    hold += dt;
                    float rate = 3f + 7f * Mathf.Clamp01(hold / 1.2f);
                    fill = Mathf.Min(fill + rate * dt, Mathf.Min(20f, s.TobaccoG));
                }
                if (stage == 1) tampT += dt;
                if (stage == 2 && dragging)
                {
                    float x = Mouse.current.position.ReadValue().x;
                    float dx = Mathf.Max(0f, x - lastX);
                    lastX = x;
                    speed = Mathf.Lerp(speed, dt > 0f ? dx / dt : 0f, 0.3f);
                    if (dx > 0f)
                    {
                        rollProgress += dx / 420f;
                        if (speed >= speedLo && speed <= speedHi) good += dt; else bad += dt;
                    }
                    if (rollProgress >= 1f) { stage = 3; dragging = false; }
                }
                if (SpacePressed && stage != 2) Action();
                brand.Show(stage == 3);
                warning.Show(stage == 3);

                step.text = stage switch
                {
                    0 => "1 · Hold on the pouch to fill the trough to the green line.",
                    1 => $"2 · Pack it down: hit the green. ({taps}/3)",
                    2 => "3 · Drag the handle to the right in one steady pull. Green means good speed.",
                    _ => "4 · Box them.",
                };
                action.text = stage switch { 0 => "Next  [Space]", 1 => "Tamp  [Space]", 2 => "…pull the handle", _ => "Box the pack  [Space]" };
                action.SetEnabled(can && stage != 2 && (stage != 0 || fill >= 6f));
                stats.text = $"Tobacco {s.TobaccoG} g · papers {s.Papers} · empty packs {s.EmptyPacks}\nPacks ready: <b>{s.Cigarettes}</b> · quality {s.PackQuality * 100f:0}%\nSells for about €{org.PackPrice(game)} a pack.";
                result.text = !can && stage == 0
                    ? "<color=#F07A6E>Not enough to make a pack. Tobacco and papers from the tabaccaio, empty packs from Nanninella.</color>"
                    : last;
            };
        }

        // The ROSSE pack: an original contraband brand.
        static void DrawPack(Painter2D p, Rect r)
        {
            float x = 55, y = 20, w = 110, h = 170;
            FillRect(p, x + 6, y + 8, w, h, new Color(0, 0, 0, 0.35f));
            FillRect(p, x, y, w, h, new Color(0.96f, 0.95f, 0.92f));
            FillRect(p, x, y, w, 52, new Color(0.72f, 0.1f, 0.12f));
            FillRect(p, x, y + 52, w, 4, new Color(0.2f, 0.2f, 0.2f));
            FillCircle(p, new Vector2(x + w / 2f, y + 88), 20f, new Color(0.72f, 0.1f, 0.12f));
            FillCircle(p, new Vector2(x + w / 2f, y + 88), 15f, new Color(0.96f, 0.95f, 0.92f));
            FillRect(p, x + w / 2f - 3, y + 76, 6, 24, new Color(0.72f, 0.1f, 0.12f));
            FillRect(p, x, y + h - 44, w, 44, new Color(0.08f, 0.08f, 0.08f));
            FillRect(p, x + 4, y + h - 40, w - 8, 36, new Color(0.96f, 0.95f, 0.92f));
            FillRect(p, x + 7, y + h - 37, w - 14, 30, new Color(0.08f, 0.08f, 0.08f));
        }
    }
}
