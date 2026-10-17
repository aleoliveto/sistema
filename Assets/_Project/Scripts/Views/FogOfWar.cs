using OSistema.Core;
using OSistema.World;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// What the crew can see. Each crew member sees in a radius (shorter at night); walls and doors
    /// block sight. Never-seen cells are black, remembered cells dimmed, and characters outside
    /// current sight aren't drawn. Presentation only: the simulation doesn't depend on it.
    /// </summary>
    public sealed class FogOfWar : MonoBehaviour
    {
        const int SightDay = 14;
        const int SightNight = 9;
        const float RecomputeSeconds = 0.1f;
        const byte Unseen = 0, Explored = 1, Visible = 2;

        static FogOfWar instance;

        Game game;
        Map map;
        byte[] state;
        float[] alpha;
        Texture2D texture;
        Color32[] pixels;
        float timer;

        /// <summary>True when the crew currently has eyes on this world position (or fog is off).</summary>
        public static bool CanSee(Vector2 position)
        {
            if (instance == null || instance.map == null) return true;
            return instance.IsVisible(new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y)));
        }

        public static bool HasExplored(Vector2Int cell) =>
            instance == null || instance.map == null || (instance.map.InBounds(cell) && instance.state[instance.map.Index(cell.x, cell.y)] != Unseen);

        public bool IsVisible(Vector2Int c) => map.InBounds(c) && state[map.Index(c.x, c.y)] == Visible;

        public void Init(Game g)
        {
            instance = this;
            game = g;
            map = g.Map;
            state = new byte[map.CellCount];
            alpha = new float[map.CellCount];
            for (int i = 0; i < alpha.Length; i++) alpha[i] = 1f;
            pixels = new Color32[map.CellCount];

            texture = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear, // soft edges between seen and unseen
                wrapMode = TextureWrapMode.Clamp,
            };
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(texture, new Rect(0, 0, map.Width, map.Height), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
            sr.sharedMaterial = SpriteFactory.UnlitMaterial;
            sr.sortingOrder = 50;

            // The crew knows the streets around their own basso from day one.
            var home = Vector2Int.RoundToInt(g.PlayerBase.center);
            for (int dy = -22; dy <= 22; dy++)
            for (int dx = -22; dx <= 22; dx++)
            {
                var c = new Vector2Int(home.x + dx, home.y + dy);
                if (map.InBounds(c) && dx * dx + dy * dy <= 22 * 22) state[map.Index(c.x, c.y)] = Explored;
            }
            Recompute();
            for (int i = 0; i < alpha.Length; i++) alpha[i] = Target(state[i]);
            Upload();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // Unseen streets stay faintly readable (you know your city), but nobody in them is shown.
        static float Target(byte s) => s == Visible ? 0f : s == Explored ? 0.42f : 0.8f;

        void LateUpdate()
        {
            if (game == null || state == null) return;
            timer += Time.unscaledDeltaTime;
            if (timer >= RecomputeSeconds)
            {
                timer = 0f;
                Recompute();
            }

            // Fade towards the new state so sight sweeps smoothly as people move.
            float step = Time.unscaledDeltaTime * 4f;
            for (int i = 0; i < alpha.Length; i++)
                alpha[i] = Mathf.MoveTowards(alpha[i], Target(state[i]), step);
            Upload();
        }

        void Upload()
        {
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(4, 4, 8, (byte)(alpha[i] * 255f));
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        void Recompute()
        {
            for (int i = 0; i < state.Length; i++)
                if (state[i] == Visible) state[i] = Explored;

            // You always know your own home.
            foreach (var c in GridUtil.Cells(new RectInt(game.PlayerBase.x - 1, game.PlayerBase.y - 1, game.PlayerBase.width + 2, game.PlayerBase.height + 2), false))
                if (map.InBounds(c)) state[map.Index(c.x, c.y)] = Visible;

            int radius = game.Clock.Daylight > 0.4f ? SightDay : SightNight;
            foreach (var pawn in game.Pawns.Crew)
            {
                if (!pawn.IsPresent) continue;
                int r = pawn.Health.Downed || pawn.Sleep != Pawns.SleepState.Awake ? 2 : radius;
                RevealFrom(pawn.Cell, r);
            }
        }

        void RevealFrom(Vector2Int origin, int radius)
        {
            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > r2) continue;
                var c = new Vector2Int(origin.x + dx, origin.y + dy);
                if (!map.InBounds(c)) continue;
                int i = map.Index(c.x, c.y);
                if (state[i] == Visible) continue;
                if (HasSight(origin, c)) state[i] = Visible;
            }
        }

        bool Opaque(int x, int y)
        {
            var b = map.GetBuilding(x, y);
            return b == BuildingDefs.Wall || b == BuildingDefs.Door;
        }

        // Bresenham: the target cell itself may be opaque (you see the wall), cells in between may not.
        bool HasSight(Vector2Int a, Vector2Int b)
        {
            int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                if (x0 == x1 && y0 == y1) return true;
                if ((x0 != a.x || y0 != a.y) && Opaque(x0, y0)) return false;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
    }
}
