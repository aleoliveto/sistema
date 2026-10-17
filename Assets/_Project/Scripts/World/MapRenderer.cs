using OSistema.Core;
using OSistema.Views;
using UnityEngine;

namespace OSistema.World
{
    /// <summary>
    /// Draws the whole map as one point-filtered texture (1 pixel = 1 cell) plus a blueprint overlay.
    /// Cheap enough for big maps; real tile art can replace it later without touching the simulation.
    /// </summary>
    public sealed class MapRenderer : MonoBehaviour
    {
        static readonly Color32 BuildBlueprint = new Color32(90, 190, 255, 140);
        static readonly Color32 DemolishBlueprint = new Color32(255, 70, 60, 150);
        static readonly Color NightTint = new Color(0.32f, 0.36f, 0.55f);

        Game game;
        Map map;
        Texture2D texture, overlayTexture;
        Color32[] pixels, overlayPixels;
        SpriteRenderer mapSprite, overlaySprite;
        bool dirty;
        int drawnBlueprintVersion = -1;

        public void Init(Game g)
        {
            game = g;
            map = g.Map;

            texture = CreateTexture();
            pixels = new Color32[map.CellCount];
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                pixels[map.Index(x, y)] = CellColor(x, y);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            mapSprite = CreateLayer(gameObject, texture, 0);

            overlayTexture = CreateTexture();
            overlayPixels = new Color32[map.CellCount];
            var overlayGo = new GameObject("Blueprints");
            overlayGo.transform.SetParent(transform, false);
            overlaySprite = CreateLayer(overlayGo, overlayTexture, 1);

            map.CellChanged += OnCellChanged;
        }

        void OnDestroy()
        {
            if (map != null) map.CellChanged -= OnCellChanged;
        }

        void OnCellChanged(int x, int y)
        {
            pixels[map.Index(x, y)] = CellColor(x, y);
            dirty = true;
        }

        void LateUpdate()
        {
            if (game == null || texture == null) return; // state lost to a script reload during Play

            if (dirty)
            {
                texture.SetPixels32(pixels);
                texture.Apply(false);
                dirty = false;
            }

            if (game.Construction.Version != drawnBlueprintVersion) RedrawBlueprints();

            var tint = Color.Lerp(NightTint, Color.white, game.Clock.Daylight);
            WorldLight.Tint = tint;
            mapSprite.color = tint;
        }

        void RedrawBlueprints()
        {
            drawnBlueprintVersion = game.Construction.Version;
            System.Array.Clear(overlayPixels, 0, overlayPixels.Length);
            foreach (var bp in game.Construction.All)
                overlayPixels[map.Index(bp.Cell.x, bp.Cell.y)] = bp.Deconstruct ? DemolishBlueprint : BuildBlueprint;
            overlayTexture.SetPixels32(overlayPixels);
            overlayTexture.Apply(false);
        }

        Color32 CellColor(int x, int y)
        {
            var building = map.GetBuilding(x, y);
            var terrain = map.GetTerrain(x, y);
            Color32 c = building != null ? building.Color : TerrainDefs.BaseColor(terrain);

            // Per-cell noise so surfaces don't look flat; sidewalks get a stone checker.
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            h ^= h >> 13;
            float n = 0.9f + (h % 1000) / 1000f * 0.12f;
            if (building == null && terrain == TerrainType.Sidewalk && ((x + y) & 1) == 0) n *= 0.92f;
            return new Color32((byte)Mathf.Min(255, c.r * n), (byte)Mathf.Min(255, c.g * n), (byte)Mathf.Min(255, c.b * n), 255);
        }

        Texture2D CreateTexture() => new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        static SpriteRenderer CreateLayer(GameObject go, Texture2D tex, int order)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
            sr.sharedMaterial = SpriteFactory.UnlitMaterial;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
