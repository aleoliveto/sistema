using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    public enum IconKind
    {
        Dirty, Clean, Product, Food, Medicine, Respect, Sun, Moon,
        Pause, Play, Fast, Faster, Build, Furnish, Crime, Orders, Jobs, Business, Help, House, Scooter, Pistol, Phone, Paper,
    }

    /// <summary>
    /// Small vector icons drawn with Painter2D, so the UI needs no image assets and stays sharp at any scale.
    /// Coordinates are in a 0..1 box, y pointing down.
    /// </summary>
    public sealed class Icon : VisualElement
    {
        static readonly Color Gold = new Color(0.91f, 0.76f, 0.42f);
        static readonly Color Ink = new Color(0.93f, 0.9f, 0.84f);

        IconKind kind;
        Color tint = Ink;

        public Icon(IconKind kind, float size = 18f)
        {
            this.kind = kind;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(IconKind k)
        {
            if (k == kind) return;
            kind = k;
            MarkDirtyRepaint();
        }

        public void Tint(Color c)
        {
            tint = c;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float s = Mathf.Min(r.width, r.height);
            if (s <= 0f) return;
            var p = ctx.painter2D;
            Vector2 P(float x, float y) => new Vector2(r.x + x * s, r.y + y * s);

            void Circle(float cx, float cy, float rad, Color c)
            {
                p.fillColor = c;
                p.BeginPath();
                p.Arc(P(cx, cy), rad * s, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }

            void Poly(Color c, params float[] xy)
            {
                p.fillColor = c;
                p.BeginPath();
                p.MoveTo(P(xy[0], xy[1]));
                for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(xy[i], xy[i + 1]));
                p.ClosePath();
                p.Fill();
            }

            void Line(Color c, float w, params float[] xy)
            {
                p.strokeColor = c;
                p.lineWidth = w * s;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(P(xy[0], xy[1]));
                for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(xy[i], xy[i + 1]));
                p.Stroke();
            }

            void Rect(Color c, float x, float y, float w, float h) => Poly(c, x, y, x + w, y, x + w, y + h, x, y + h);

            void Euro(Color c)
            {
                p.strokeColor = c;
                p.lineWidth = 0.09f * s;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.Arc(P(0.53f, 0.5f), 0.2f * s, Angle.Degrees(40f), Angle.Degrees(320f));
                p.Stroke();
                Line(c, 0.07f, 0.26f, 0.44f, 0.56f, 0.44f);
                Line(c, 0.07f, 0.26f, 0.56f, 0.52f, 0.56f);
            }

            switch (kind)
            {
                case IconKind.Dirty:
                    Circle(0.5f, 0.5f, 0.46f, new Color(0.55f, 0.45f, 0.2f));
                    Circle(0.5f, 0.5f, 0.38f, Gold);
                    Euro(new Color(0.35f, 0.27f, 0.1f));
                    break;
                case IconKind.Clean:
                    Circle(0.5f, 0.5f, 0.46f, new Color(0.45f, 0.55f, 0.65f));
                    Circle(0.5f, 0.5f, 0.38f, new Color(0.85f, 0.92f, 0.97f));
                    Euro(new Color(0.2f, 0.32f, 0.45f));
                    break;
                case IconKind.Product:
                    Poly(new Color(0.75f, 0.45f, 0.9f), 0.3f, 0.35f, 0.7f, 0.35f, 0.85f, 0.8f, 0.5f, 0.92f, 0.15f, 0.8f);
                    Poly(new Color(0.55f, 0.3f, 0.7f), 0.4f, 0.12f, 0.6f, 0.12f, 0.62f, 0.35f, 0.38f, 0.35f);
                    break;
                case IconKind.Food:
                    Circle(0.5f, 0.55f, 0.4f, new Color(0.95f, 0.93f, 0.88f));
                    Circle(0.5f, 0.55f, 0.27f, new Color(0.88f, 0.42f, 0.22f)); // a pizza, obviously
                    Circle(0.42f, 0.5f, 0.06f, new Color(0.95f, 0.9f, 0.75f));
                    Circle(0.58f, 0.62f, 0.06f, new Color(0.95f, 0.9f, 0.75f));
                    Circle(0.6f, 0.46f, 0.05f, new Color(0.3f, 0.6f, 0.25f));
                    break;
                case IconKind.Medicine:
                    Rect(new Color(0.35f, 0.8f, 0.6f), 0.38f, 0.12f, 0.24f, 0.76f);
                    Rect(new Color(0.35f, 0.8f, 0.6f), 0.12f, 0.38f, 0.76f, 0.24f);
                    break;
                case IconKind.Respect:
                    Poly(Gold, 0.1f, 0.78f, 0.1f, 0.3f, 0.32f, 0.52f, 0.5f, 0.15f, 0.68f, 0.52f, 0.9f, 0.3f, 0.9f, 0.78f);
                    Rect(new Color(0.7f, 0.55f, 0.25f), 0.1f, 0.78f, 0.8f, 0.1f);
                    break;
                case IconKind.Sun:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Line(new Color(1f, 0.82f, 0.4f), 0.07f, 0.5f + Mathf.Cos(a) * 0.32f, 0.5f + Mathf.Sin(a) * 0.32f, 0.5f + Mathf.Cos(a) * 0.46f, 0.5f + Mathf.Sin(a) * 0.46f);
                    }
                    Circle(0.5f, 0.5f, 0.24f, new Color(1f, 0.82f, 0.4f));
                    break;
                case IconKind.Moon:
                    Circle(0.5f, 0.5f, 0.38f, new Color(0.85f, 0.88f, 1f));
                    Circle(0.66f, 0.38f, 0.33f, new Color(0.09f, 0.08f, 0.07f));
                    break;
                case IconKind.Pause:
                    Rect(tint, 0.24f, 0.2f, 0.17f, 0.6f);
                    Rect(tint, 0.59f, 0.2f, 0.17f, 0.6f);
                    break;
                case IconKind.Play:
                    Poly(tint, 0.3f, 0.18f, 0.8f, 0.5f, 0.3f, 0.82f);
                    break;
                case IconKind.Fast:
                    Poly(tint, 0.12f, 0.2f, 0.5f, 0.5f, 0.12f, 0.8f);
                    Poly(tint, 0.5f, 0.2f, 0.88f, 0.5f, 0.5f, 0.8f);
                    break;
                case IconKind.Faster:
                    Poly(tint, 0.02f, 0.22f, 0.34f, 0.5f, 0.02f, 0.78f);
                    Poly(tint, 0.34f, 0.22f, 0.66f, 0.5f, 0.34f, 0.78f);
                    Poly(tint, 0.66f, 0.22f, 0.98f, 0.5f, 0.66f, 0.78f);
                    break;
                case IconKind.Build:
                    Line(new Color(0.65f, 0.45f, 0.25f), 0.12f, 0.22f, 0.82f, 0.58f, 0.46f);
                    Poly(tint, 0.48f, 0.2f, 0.78f, 0.14f, 0.9f, 0.3f, 0.66f, 0.54f, 0.5f, 0.42f);
                    break;
                case IconKind.Furnish:
                    Rect(new Color(0.72f, 0.28f, 0.3f), 0.18f, 0.32f, 0.64f, 0.26f);
                    Rect(new Color(0.85f, 0.38f, 0.4f), 0.12f, 0.5f, 0.76f, 0.2f);
                    Rect(new Color(0.55f, 0.2f, 0.22f), 0.08f, 0.42f, 0.12f, 0.36f);
                    Rect(new Color(0.55f, 0.2f, 0.22f), 0.8f, 0.42f, 0.12f, 0.36f);
                    break;
                case IconKind.Crime:
                case IconKind.Pistol:
                    Poly(tint, 0.1f, 0.28f, 0.88f, 0.28f, 0.88f, 0.44f, 0.5f, 0.44f, 0.42f, 0.84f, 0.24f, 0.84f, 0.3f, 0.44f, 0.1f, 0.44f);
                    Rect(new Color(0.55f, 0.4f, 0.25f), 0.25f, 0.5f, 0.16f, 0.3f);
                    break;
                case IconKind.Orders:
                    Line(tint, 0.07f, 0.25f, 0.9f, 0.25f, 0.1f);
                    Poly(new Color(0.94f, 0.48f, 0.43f), 0.28f, 0.12f, 0.82f, 0.28f, 0.28f, 0.46f);
                    break;
                case IconKind.Jobs:
                    Rect(new Color(0.93f, 0.87f, 0.7f), 0.2f, 0.12f, 0.6f, 0.76f);
                    Line(new Color(0.45f, 0.35f, 0.25f), 0.05f, 0.3f, 0.32f, 0.7f, 0.32f);
                    Line(new Color(0.45f, 0.35f, 0.25f), 0.05f, 0.3f, 0.48f, 0.7f, 0.48f);
                    Line(new Color(0.45f, 0.35f, 0.25f), 0.05f, 0.3f, 0.64f, 0.56f, 0.64f);
                    break;
                case IconKind.Business:
                    Rect(new Color(0.55f, 0.36f, 0.2f), 0.1f, 0.32f, 0.8f, 0.52f);
                    Line(new Color(0.55f, 0.36f, 0.2f), 0.07f, 0.36f, 0.32f, 0.36f, 0.18f, 0.64f, 0.18f, 0.64f, 0.32f);
                    Rect(Gold, 0.44f, 0.5f, 0.12f, 0.1f);
                    break;
                case IconKind.Help:
                    p.strokeColor = tint;
                    p.lineWidth = 0.08f * s;
                    p.BeginPath();
                    p.Arc(P(0.5f, 0.5f), 0.4f * s, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Stroke();
                    Circle(0.5f, 0.3f, 0.06f, tint);
                    Line(tint, 0.09f, 0.5f, 0.45f, 0.5f, 0.72f);
                    break;
                case IconKind.House:
                    Poly(new Color(0.72f, 0.4f, 0.28f), 0.5f, 0.1f, 0.92f, 0.46f, 0.08f, 0.46f);
                    Rect(new Color(0.86f, 0.74f, 0.5f), 0.18f, 0.46f, 0.64f, 0.44f);
                    Rect(new Color(0.42f, 0.27f, 0.16f), 0.42f, 0.62f, 0.16f, 0.28f);
                    break;
                case IconKind.Phone:
                    Rect(new Color(0.2f, 0.2f, 0.22f), 0.28f, 0.06f, 0.44f, 0.88f);
                    Rect(new Color(0.55f, 0.75f, 0.85f), 0.33f, 0.16f, 0.34f, 0.56f);
                    Circle(0.5f, 0.83f, 0.05f, tint);
                    break;
                case IconKind.Paper:
                    Rect(new Color(0.93f, 0.9f, 0.82f), 0.1f, 0.16f, 0.8f, 0.68f);
                    Rect(new Color(0.25f, 0.22f, 0.2f), 0.18f, 0.24f, 0.64f, 0.12f);
                    Rect(new Color(0.55f, 0.5f, 0.45f), 0.18f, 0.44f, 0.3f, 0.3f);
                    Line(new Color(0.55f, 0.5f, 0.45f), 0.05f, 0.56f, 0.48f, 0.82f, 0.48f);
                    Line(new Color(0.55f, 0.5f, 0.45f), 0.05f, 0.56f, 0.6f, 0.82f, 0.6f);
                    Line(new Color(0.55f, 0.5f, 0.45f), 0.05f, 0.56f, 0.72f, 0.76f, 0.72f);
                    break;
                case IconKind.Scooter:
                    Circle(0.22f, 0.72f, 0.14f, tint);
                    Circle(0.78f, 0.72f, 0.14f, tint);
                    Poly(new Color(0.75f, 0.15f, 0.15f), 0.22f, 0.6f, 0.4f, 0.4f, 0.72f, 0.42f, 0.8f, 0.6f);
                    Line(tint, 0.07f, 0.62f, 0.42f, 0.7f, 0.18f, 0.82f, 0.18f);
                    break;
            }
        }
    }
}
