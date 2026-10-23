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

        // ---------- What both tables share: a streak, a pop of feedback, and the odd scare ----------

        /// <summary>A word that pops up over the table and fades ("Spot on!", "Perfetto!").</summary>
        sealed class Pop
        {
            readonly Label label;
            float t = 9f;

            public Pop(VisualElement parent)
            {
                label = Ui.Text("", parent, "minigame__pop");
                label.pickingMode = PickingMode.Ignore;
            }

            public void Show(string text, string colour)
            {
                label.text = $"<color={colour}>{text}</color>";
                t = 0f;
            }

            public void Tick(float dt)
            {
                t += dt;
                float a = t < 0.12f ? t / 0.12f : Mathf.Clamp01(1f - (t - 0.7f) / 0.5f);
                label.style.opacity = a;
                float s = t < 0.12f ? 0.6f + 0.6f * (t / 0.12f) : 1.2f - 0.2f * Mathf.Clamp01((t - 0.12f) / 0.2f);
                label.style.scale = new Scale(new Vector3(s, s, 1f));
                label.style.translate = new Translate(0f, -18f * t, 0f);
            }
        }

        /// <summary>
        /// Now and then, while you work, something outside: footsteps on the stairs, a siren, a knock. Press H in time
        /// to throw the cloth over the table; too slow and someone saw, so the rione heats up a little.
        /// </summary>
        sealed class Scare
        {
            readonly Label banner;
            readonly Game game;
            float next, window;
            int which = -1;
            public bool Active => which >= 0;

            static readonly string[] Lines =
            {
                "Footsteps on the stairs!", "A siren in the street!", "Someone knocks on the door!", "A torch beam at the window!",
            };

            public Scare(VisualElement parent, Game game)
            {
                this.game = game;
                banner = Ui.Text("", parent, "minigame__scare");
                banner.Show(false);
                next = 25f + (float)game.Rng.NextDouble() * 35f;
            }

            /// <summary>Returns a line for the result box when something happened this frame, else null.</summary>
            public string Tick(float dt, bool working)
            {
                if (which < 0)
                {
                    if (!working) return null;
                    next -= dt;
                    if (next > 0f) return null;
                    which = game.Rng.Next(Lines.Length);
                    window = 1.6f;
                    banner.text = $"{Lines[which]}  <b>[H]</b> cover the table";
                    banner.Show(true);
                    OSistema.Views.SoundManager.PlayUi(OSistema.Views.AudioSynth.Click, 0.5f);
                    return null;
                }
                window -= dt;
                banner.style.opacity = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 10f));
                if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                {
                    End();
                    return "<color=#8FD18A>Cloth over the table, light off. It passed.</color>";
                }
                if (window > 0f) return null;
                End();
                if (game.Player != null) game.Territory.AddHeat(game.Player.Cell, 2f);
                return "<color=#F07A6E>Too slow: someone saw the table. The rione will talk.</color>";
            }

            void End()
            {
                which = -1;
                banner.Show(false);
                next = 35f + (float)game.Rng.NextDouble() * 50f;
            }
        }

        // ---------- Bagging ----------

        public void OpenBagging()
        {
            var org = game.Org;
            var s = game.Stock;
            var panel = OpenMinigame("THE BAGGING TABLE",
                $"Hold on the block to scrape product onto the scale; a dose is {Prices.DoseDg / 10f:0.0} g. Then hold Space to seal and let go in the green. Spot-on bags in a row build your streak. Tired hands shake.");

            float target = Prices.DoseDg / 10f;
            float w = 0f, shown = 0f, hold = 0f, seal = 0f;
            int stage = 0;               // 0 pouring, 1 sealing
            bool pouring = false, cut = false;
            int perfect = 0, ok = 0, light = 0, heavy = 0, burned = 0, streak = 0, best = 0;
            string last = "";
            // Sealing: a hot iron melts the bag in a moment; tying a knot by hand gives a wider, slower window.
            float sealRate = s.HeatSealer ? 1.25f : 0.8f, sealLo = s.HeatSealer ? 0.62f : 0.55f, sealHi = s.HeatSealer ? 0.8f : 0.85f;

            var row = Ui.Row(panel);
            row.style.justifyContent = Justify.SpaceAround;
            row.style.marginTop = 10;

            var brickCol = Ui.Box("col").Into(row);
            brickCol.style.alignItems = Align.Center;
            var brick = new Canvas(200, 200).Into(brickCol);
            var brickLabel = Ui.Text("", brickCol, "small");
            brick.RegisterCallback<PointerDownEvent>(_ => { if (stage == 0) { pouring = true; hold = 0f; } });

            var scaleCol = Ui.Box("col").Into(row);
            scaleCol.style.alignItems = Align.Center;
            var scale = new Canvas(260, 240).Into(scaleCol);
            var readout = Ui.Text("", scaleCol, "minigame__readout");
            var pop = new Pop(scaleCol);

            var right = Ui.Box("col").Into(row);
            right.style.width = 240;
            var step = Ui.Text("", right, "bold", "wrap");
            // The button ties a quick knot: always holds, never a perfect seal. Space and the iron do better.
            var sealBtn = Ui.Btn("Tie a quick knot", () => { if (stage == 0 && w >= 0.1f) Finish(0.6f); }, right);
            sealBtn.style.marginTop = 6;
            Ui.Btn("Scrape a little back", () => { if (stage == 0) w = Mathf.Max(0f, w - 0.04f); }, right, "btn--small").style.marginTop = 6;
            var cutBtn = Ui.Btn("", () => cut = !cut, right, "btn--small");
            cutBtn.style.marginTop = 6;
            var streakLabel = Ui.Text("", right, "minigame__streak");
            var stats = Ui.Text("", right, "small", "wrap");
            stats.style.marginTop = 6;
            var result = Ui.Text("", right, "small", "wrap");
            result.style.marginTop = 6;
            var scare = new Scare(panel, game);

            brick.Paint = (p, r) =>
            {
                float frac = Mathf.Clamp01(s.BulkDg / 10000f);
                float bw = 60f + 120f * Mathf.Sqrt(frac), bh = 50f + 80f * Mathf.Sqrt(frac);
                float x = (r.width - bw) / 2f, y = (r.height - bh) / 2f;
                FillRect(p, x + 6, y + 8, bw, bh, new Color(0, 0, 0, 0.35f));
                FillRect(p, x, y, bw, bh, new Color(0.93f, 0.92f, 0.9f));
                FillRect(p, x, y, bw, 6, new Color(0.99f, 0.99f, 0.98f));
                FillRect(p, x + bw - 6, y, 6, bh, new Color(0.82f, 0.81f, 0.8f));
                Line(p, new Vector2(x + 8, y + bh * 0.3f), new Vector2(x + bw * 0.5f, y + 10), 2f, new Color(1, 1, 1, 0.6f));
                if (pouring) FillCircle(p, new Vector2(x + bw - 4, y + bh - 4), 10f, new Color(0.96f, 0.96f, 0.95f));
                // The box of lactose, when you're cutting.
                if (cut)
                {
                    FillRect(p, 14, r.height - 54, 44, 44, new Color(0.3f, 0.45f, 0.75f));
                    FillRect(p, 18, r.height - 50, 36, 14, new Color(0.95f, 0.95f, 0.98f));
                }
            };
            scale.Paint = (p, r) =>
            {
                var c = new Vector2(r.width / 2f, r.height * 0.6f);
                FillRect(p, 30, r.height * 0.72f, r.width - 60, 40, new Color(0.16f, 0.16f, 0.18f));
                FillCircle(p, c, 78f, new Color(0.62f, 0.64f, 0.66f));
                FillCircle(p, c, 72f, new Color(0.78f, 0.8f, 0.82f));
                // The open bag, and the little heap growing in it (bluish when cut).
                FillRect(p, c.x - 34, c.y - 44, 68, 80, new Color(0.92f, 0.95f, 0.98f, 0.55f));
                float heap = 6f + 22f * Mathf.Sqrt(Mathf.Max(0f, w));
                if (w > 0.005f) FillCircle(p, c + new Vector2(0, 10), heap, cut ? new Color(0.93f, 0.95f, 1f) : new Color(0.97f, 0.97f, 0.96f));
                // The bag's mouth: open while pouring, pressed and glowing while sealing.
                var mouth = stage == 1 ? Color.Lerp(new Color(0.5f, 0.55f, 0.65f), new Color(1f, 0.45f, 0.15f), Mathf.Clamp01(seal / sealHi)) : new Color(0.5f, 0.55f, 0.65f, 0.8f);
                FillRect(p, c.x - 34, c.y - 44, 68, stage == 1 ? 10 : 6, mouth);
                // The target: a band on the dial around the dose.
                if (!s.DigitalScale)
                {
                    var dial = new Vector2(r.width / 2f, 34);
                    FillCircle(p, dial, 30f, new Color(0.95f, 0.93f, 0.86f));
                    for (int k = 0; k <= 10; k++)
                    {
                        float a = Mathf.Lerp(200f, 340f, k / 10f) * Mathf.Deg2Rad;
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Line(p, dial + dir * 22f, dial + dir * (k % 5 == 0 ? 29f : 26f), 1.5f, new Color(0.2f, 0.2f, 0.2f));
                    }
                    float ta = Mathf.Lerp(200f, 340f, target) * Mathf.Deg2Rad;
                    Line(p, dial + new Vector2(Mathf.Cos(ta), Mathf.Sin(ta)) * 18f, dial + new Vector2(Mathf.Cos(ta), Mathf.Sin(ta)) * 30f, 3f, new Color(0.3f, 0.7f, 0.3f));
                    float na = Mathf.Lerp(200f, 340f, Mathf.Clamp01(shown / 1f)) * Mathf.Deg2Rad;
                    Line(p, dial, dial + new Vector2(Mathf.Cos(na), Mathf.Sin(na)) * 26f, 2f, new Color(0.75f, 0.1f, 0.1f));
                }
                // The sealing bar.
                if (stage == 1)
                {
                    float y = r.height - 22;
                    FillRect(p, 30, y, r.width - 60, 12, new Color(0.15f, 0.15f, 0.16f));
                    FillRect(p, 30 + (r.width - 60) * sealLo, y, (r.width - 60) * (sealHi - sealLo), 12, new Color(0.3f, 0.6f, 0.3f));
                    FillRect(p, 30 + (r.width - 60) * Mathf.Clamp01(seal) - 2, y - 4, 4, 20, new Color(1f, 0.6f, 0.2f));
                }
            };

            void Finish(float forced = -1f)
            {
                if (!org.CanMakeDose(game)) { stage = 0; return; }
                // The seal first: let go in the green.
                float sq = forced >= 0f ? forced : seal >= sealLo && seal <= sealHi ? 1f : seal < sealLo ? 0.4f : 0f;
                if (sq <= 0f)
                {
                    // Held too long: the bag melts. The product goes back on the block; the bag is lost.
                    burned++;
                    s.Baggies--;
                    streak = 0;
                    pop.Show("Melted!", "#F07A6E");
                    last = "<color=#F07A6E>Held the iron too long: the bag melted. Scrape it back and try another.</color>";
                    w = 0f; shown = 0f; stage = 0;
                    return;
                }
                float err = w - target;
                if (Mathf.Abs(err) <= 0.03f && sq >= 1f)
                {
                    perfect++; streak++; best = Mathf.Max(best, streak);
                    pop.Show(streak >= 3 ? $"Spot on ×{streak}" : "Spot on!", "#8FD18A");
                    last = $"<color=#8FD18A>{w:0.00} g — spot on.</color>";
                }
                else if (Mathf.Abs(err) <= 0.08f)
                {
                    ok++; streak = 0;
                    pop.Show(sq < 1f ? "Loose seal" : "Near enough", "#E8D9B0");
                    last = $"{w:0.00} g — near enough{(sq < 1f ? ", but the seal's loose" : "")}.";
                }
                else if (err < 0f)
                {
                    light++; streak = 0;
                    pop.Show("Light!", "#F07A6E");
                    last = $"<color=#F07A6E>{w:0.00} g — light. Someone will notice.</color>";
                }
                else
                {
                    heavy++; streak = 0;
                    pop.Show("Heavy", "#F2B866");
                    last = $"<color=#F2B866>{w:0.00} g — heavy. You gave product away.</color>";
                }
                org.MakeDose(w, game, cut ? 0.25f : 0f, sq);
                w = 0f; shown = 0f; stage = 0;
            }

            minigameCanvases = new[] { brick, scale };
            minigameTick = dt =>
            {
                var kb = Keyboard.current;
                if (!MouseHeld) pouring = false;
                bool can = org.CanMakeDose(game);
                var me = game.Player;
                // Tired or wounded hands shake: the pour comes in fits.
                float shake = me == null ? 0f : Mathf.Clamp01((0.35f - me.Needs.Rest) / 0.35f) * 0.8f;
                if (stage == 0 && pouring && can)
                {
                    hold += dt;
                    float rate = 0.08f + 0.35f * Mathf.Clamp01(hold / 1.5f);
                    rate *= 1f + shake * (Mathf.PerlinNoise(Time.unscaledTime * 6f, 3f) - 0.3f) * 2f;
                    w = Mathf.Min(w + Mathf.Max(0f, rate) * dt, s.BulkDg / 10f / (cut ? 0.75f : 1f));
                }
                if (s.DigitalScale) shown = w;
                else shown = Mathf.Lerp(shown, w + (Mathf.PerlinNoise(Time.time * 3f, 0f) - 0.5f) * 0.12f, dt * 4f);
                readout.text = s.DigitalScale ? $"{w:0.00} g" : $"≈ {Mathf.Max(0f, shown):0.0} g";

                // Space: start sealing; hold to heat; let go to finish.
                if (kb != null && stage == 0 && kb.spaceKey.wasPressedThisFrame && w >= 0.1f && can) { stage = 1; seal = 0f; }
                if (stage == 1)
                {
                    bool held = kb != null && kb.spaceKey.isPressed;
                    if (held) seal += dt * sealRate;
                    if (!held || seal > 1.05f) Finish();
                }

                var scared = scare.Tick(dt, stage == 0 && (pouring || w > 0f) || stage == 1);
                if (scared != null) last = scared;
                pop.Tick(dt);
                sealBtn.SetEnabled(can && stage == 0 && w >= 0.1f);
                cutBtn.text = cut ? "✓ Cutting with lactose (25% stretched)" : "Cut with lactose (stretch it)";
                step.text = stage == 0 ? "1 · Hold on the block to pour; watch the scale." : "2 · Hold Space… let go in the green.";
                streakLabel.text = streak >= 2 ? $"MANO FERMA ×{streak}" : best >= 3 ? $"best streak ×{best}" : "";
                brickLabel.text = $"Product: {s.BulkDg / 10f:0.0} g · bags: {s.Baggies}";
                stats.text = $"Spot on {perfect} · near {ok} · light {light} · heavy {heavy}{(burned > 0 ? $" · melted {burned}" : "")}\nDoses ready: <b>{s.Product}</b> · honest: {s.DoseQuality * 100f:0}%";
                result.text = !can ? (s.Baggies <= 0 ? "<color=#F07A6E>No bags left. The salumeria sells them.</color>" : "<color=#F07A6E>No product left.</color>")
                    : cut && string.IsNullOrEmpty(last) ? "Cut bags stretch the product, but the customers feel it and talk sooner." : last;
            };
        }

        // ---------- Rolling ----------

        public void OpenRolling()
        {
            var org = game.Org;
            var s = game.Stock;
            var panel = OpenMinigame("THE ROLLING TABLE",
                "One pack of 20 at a time: fill to this pack's line (the tobacco's never the same twice), pack it down, pull the handle steadily, snap the lid shut. Good packs in a row build your streak, and it shows in the price.");

            int stage = 0;               // 0 fill, 1 tamp, 2 roll, 3 box
            float fill = 0f, hold = 0f, tampT = 0f, rollProgress = 0f, good = 0f, bad = 0f, lastX = 0f, speed = 0f, lidT = 0f, spilled = 0f;
            float tampScore = 0f;
            int taps = 0, streak = 0, best = 0, packs = 0;
            bool pouring = false, dragging = false, lidShut = false;
            float lidScore = 0f;
            string last = "";
            float zoneLo = s.RollingMachine ? 0.36f : 0.42f, zoneHi = s.RollingMachine ? 0.64f : 0.58f;
            float speedLo = s.RollingMachine ? 180f : 250f, speedHi = s.RollingMachine ? 950f : 700f;
            // Each pack's tobacco: dry fills faster and wants a little more; damp is slow and wants less.
            float lineAt = Prices.TobaccoPerPack, pourMul = 1f;
            string batch = "";
            var rolled = new System.Collections.Generic.List<bool>();   // each cigarette as it comes out: good or not
            void NewPack()
            {
                int kind = game.Rng.Next(3);
                lineAt = Prices.TobaccoPerPack + (kind == 0 ? 1.5f : kind == 1 ? 0f : -1.5f);
                pourMul = kind == 0 ? 1.35f : kind == 1 ? 1f : 0.75f;
                batch = kind == 0 ? "Dry tobacco: it runs fast." : kind == 1 ? "Good tobacco." : "Damp tobacco: it clumps.";
                fill = 0f; spilled = 0f; taps = 0; tampScore = 0f; rollProgress = good = bad = 0f; lidT = 0f; lidShut = false; lidScore = 0f;
                rolled.Clear();
            }
            NewPack();

            var row = Ui.Row(panel);
            row.style.justifyContent = Justify.SpaceAround;
            row.style.marginTop = 10;

            var left = new Canvas(220, 220).Into(row);
            var brand = Ui.Text("ROSSE", left, "pack__brand");
            var warning = Ui.Text("IL FUMO\nUCCIDE", left, "pack__warning");
            var midCol = Ui.Box("col").Into(row);
            midCol.style.alignItems = Align.Center;
            var mid = new Canvas(300, 220).Into(midCol);
            var pop = new Pop(midCol);
            var right = Ui.Box("col").Into(row);
            right.style.width = 230;
            var step = Ui.Text("", right, "bold", "wrap");
            var action = Ui.Btn("", () => Action(), right);
            action.style.marginTop = 6;
            var streakLabel = Ui.Text("", right, "minigame__streak");
            var stats = Ui.Text("", right, "small", "wrap");
            stats.style.marginTop = 6;
            var result = Ui.Text("", right, "small", "wrap");
            var scare = new Scare(panel, game);

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
                else
                {
                    DrawPack(p, r);
                    // The lid swinging: snap it shut when it's straight.
                    float a = lidShut ? 0f : Mathf.Sin(lidT * 4.2f) * 70f;
                    var hinge = new Vector2(55, 20);
                    var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                    Line(p, hinge, hinge + dir * 110f, 8f, lidShut ? new Color(0.72f, 0.1f, 0.12f) : new Color(0.8f, 0.2f, 0.2f, 0.9f));
                }
            };
            mid.Paint = (p, r) =>
            {
                // The trough of the rolling machine, with this pack's line.
                FillRect(p, 20, 60, 260, 70, new Color(0.25f, 0.25f, 0.27f));
                float f = Mathf.Clamp01(fill / 20f);
                FillRect(p, 26, 66 + 58 * (1f - f), 248, 58 * f, new Color(0.6f, 0.42f, 0.22f));
                float lo = 66 + 58 * (1f - (lineAt + 1f) / 20f), hi = 66 + 58 * (1f - (lineAt - 1f) / 20f);
                FillRect(p, 20, lo, 6, hi - lo, new Color(0.4f, 0.8f, 0.4f));
                FillRect(p, 274, lo, 6, hi - lo, new Color(0.4f, 0.8f, 0.4f));
                if (spilled > 0f)
                    for (int k = 0; k < Mathf.Min(14, (int)(spilled * 4f)); k++)
                        FillRect(p, 30 + (k * 41) % 240, 50 - (k * 7) % 12, 5, 2, new Color(0.62f, 0.44f, 0.24f));
                if (stage == 1)
                {
                    // The tamping bar: hit the green; the gold middle is perfect. It speeds up with each tap.
                    FillRect(p, 20, 150, 260, 18, new Color(0.15f, 0.15f, 0.16f));
                    FillRect(p, 20 + 260 * zoneLo, 150, 260 * (zoneHi - zoneLo), 18, new Color(0.3f, 0.6f, 0.3f));
                    FillRect(p, 20 + 260 * 0.47f, 150, 260 * 0.06f, 18, new Color(0.85f, 0.7f, 0.25f));
                    float m = Marker();
                    FillRect(p, 20 + 260 * m - 2, 146, 4, 26, new Color(0.95f, 0.95f, 0.95f));
                }
                if (stage >= 2)
                {
                    float x = 30 + 240 * Mathf.Clamp01(rollProgress);
                    FillRect(p, 20, 186, 260, 6, new Color(0.4f, 0.4f, 0.42f));
                    bool inBand = speed >= speedLo && speed <= speedHi;
                    FillRect(p, x - 10, 176, 20, 26, dragging ? (inBand ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.9f, 0.4f, 0.3f)) : new Color(0.8f, 0.8f, 0.82f));
                    // The cigarettes coming out, each one good (white) or crooked (grey).
                    for (int k = 0; k < rolled.Count; k++)
                        FillRect(p, 30 + k * 12.5f, rolled[k] ? 140 : 144, 8, rolled[k] ? 34 : 28, rolled[k] ? new Color(0.96f, 0.95f, 0.92f) : new Color(0.7f, 0.68f, 0.64f));
                }
            };

            float Marker() => 0.5f + 0.5f * Mathf.Sin(tampT * (5.2f + taps * 1.8f));

            void Action()
            {
                switch (stage)
                {
                    case 0:
                        if (fill >= 6f) { stage = 1; tampT = 0f; }
                        break;
                    case 1:
                    {
                        float m = Marker();
                        taps++;
                        if (Mathf.Abs(m - 0.5f) <= 0.03f) { tampScore += 1.2f; pop.Show("Perfetto!", "#E3B862"); }
                        else if (m >= zoneLo && m <= zoneHi) { tampScore += 1f; pop.Show("Good", "#8FD18A"); }
                        else pop.Show("Miss", "#F07A6E");
                        if (taps >= 3) stage = 2;
                        break;
                    }
                    case 3:
                    {
                        if (!lidShut)
                        {
                            // Snap the lid: straight is perfect, a swinging lid crushes a corner.
                            float a = Mathf.Abs(Mathf.Sin(lidT * 4.2f) * 70f);
                            lidScore = a < 8f ? 1f : a < 22f ? 0.6f : 0.2f;
                            lidShut = true;
                            pop.Show(lidScore >= 1f ? "Snap!" : lidScore > 0.5f ? "A bit crooked" : "Crushed corner", lidScore >= 1f ? "#8FD18A" : lidScore > 0.5f ? "#E8D9B0" : "#F2B866");
                            Box();
                        }
                        break;
                    }
                }
            }

            void Box()
            {
                float fillScore = 1f - Mathf.Clamp01((Mathf.Abs(fill - lineAt) - 0.5f) / 3.5f);
                float rollScore = rolled.Count > 0 ? rolled.FindAll(x => x).Count / (float)rolled.Count : 0.5f;
                float q = 0.35f * fillScore + 0.2f * Mathf.Clamp01(tampScore / 3f) + 0.3f * rollScore + 0.15f * lidScore;
                // A steady hand that's been at it a while: a streak lifts every pack a little.
                if (q > 0.8f) { streak++; best = Mathf.Max(best, streak); } else streak = 0;
                q = Mathf.Clamp01(q + Mathf.Min(streak, 5) * 0.02f);
                org.MakePack(Mathf.RoundToInt(fill + spilled), q, game);
                packs++;
                last = q > 0.85f ? $"<color=#8FD18A>★★★ A beautiful pack. They'll pass for the real thing.</color>"
                    : q > 0.6f ? "★★ A decent pack." : "<color=#F2B866>★ Loose and lumpy. It'll sell, cheap.</color>";
                stage = 4;   // a beat to admire it, then the next pack
                lidT = 0f;
            }

            minigameCanvases = new[] { left, mid };
            minigameTick = dt =>
            {
                bool can = org.CanMakePack(game);
                if (!MouseHeld) { pouring = false; dragging = false; }
                if (stage == 0 && pouring && can)
                {
                    hold += dt;
                    float rate = (3f + 7f * Mathf.Clamp01(hold / 1.2f)) * pourMul;
                    fill = Mathf.Min(fill + rate * dt, Mathf.Min(20f, s.TobaccoG));
                    // Over the brim: it spills on the table (and it's gone).
                    if (fill > lineAt + 3f) { spilled += fill - (lineAt + 3f); fill = lineAt + 3f; if (spilled > 0.3f && spilled < 0.5f) pop.Show("Spilling!", "#F2B866"); }
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
                        bool inBand = speed >= speedLo && speed <= speedHi;
                        if (inBand) good += dt; else bad += dt;
                        // Each twentieth of the pull pops out one cigarette.
                        while (rolled.Count < Mathf.Min(20, (int)(rollProgress * 20f))) rolled.Add(inBand);
                    }
                    if (rollProgress >= 1f)
                    {
                        while (rolled.Count < 20) rolled.Add(false);
                        int goodOnes = rolled.FindAll(c => c).Count;
                        pop.Show(goodOnes >= 18 ? "Smooth!" : goodOnes >= 12 ? $"{goodOnes}/20 straight" : "Crooked", goodOnes >= 18 ? "#8FD18A" : goodOnes >= 12 ? "#E8D9B0" : "#F2B866");
                        stage = 3; dragging = false; lidT = 0f;
                    }
                }
                if (stage == 3 && !lidShut) lidT += dt;
                if (stage == 4)
                {
                    lidT += dt;
                    if (lidT > 0.9f) { stage = 0; NewPack(); }
                }
                if (SpacePressed && stage != 2 && stage != 4) Action();
                var scared = scare.Tick(dt, stage < 4 && (fill > 0f || pouring));
                if (scared != null) last = scared;
                pop.Tick(dt);
                brand.Show(stage >= 3);
                warning.Show(stage >= 3);

                step.text = stage switch
                {
                    0 => $"1 · {batch} Hold on the pouch to fill to the green line.",
                    1 => $"2 · Pack it down: hit the green, gold is perfect. ({taps}/3)",
                    2 => "3 · Drag the handle right in one steady pull. Green means good speed.",
                    3 => "4 · Snap the lid shut when it's straight.",
                    _ => "Boxed.",
                };
                action.text = stage switch { 0 => "Next  [Space]", 1 => "Tamp  [Space]", 2 => "…pull the handle", 3 => "Snap the lid  [Space]", _ => "…" };
                action.SetEnabled(can && stage != 2 && stage != 4 && (stage != 0 || fill >= 6f));
                streakLabel.text = streak >= 2 ? $"MANO D'ORO ×{streak}" : best >= 3 ? $"best streak ×{best}" : "";
                stats.text = $"Tobacco {s.TobaccoG} g · papers {s.Papers} · empty packs {s.EmptyPacks}\nPacks ready: <b>{s.Cigarettes}</b> ({packs} this sitting) · quality {s.PackQuality * 100f:0}%\nSells for about €{org.PackPrice(game)} a pack.";
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
