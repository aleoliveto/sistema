using System.Collections.Generic;
using OSistema.Core;
using OSistema.Views;
using UnityEngine;

namespace OSistema.World
{
    /// <summary>
    /// Draws the map as chunks of procedural tiles (32 px per cell, 32x32 cells per chunk),
    /// repainting only the cells that change. A 1-pixel-per-cell overlay shows blueprints and piazze.
    /// </summary>
    public sealed class MapRenderer : MonoBehaviour
    {
        const int ChunkCells = 32;
        const int Tile = TilePainter.Hi;

        static readonly Color32 BuildBlueprint = new Color32(90, 190, 255, 140);
        static readonly Color32 DemolishBlueprint = new Color32(255, 70, 60, 150);
        static readonly Color32 SquareColor = new Color32(200, 60, 220, 150);
        static readonly Color32 LookoutColor = new Color32(200, 60, 220, 70);
        static readonly Color32 RivalSquareColor = new Color32(230, 40, 40, 150);

        sealed class Chunk
        {
            public Texture2D Texture;
            public Color32[] Pixels;
            public RectInt Cells;
            public bool Dirty;
        }

        Game game;
        Map map;
        Chunk[,] chunks;
        int chunksX, chunksY;
        readonly HashSet<Vector2Int> pendingCells = new HashSet<Vector2Int>();

        Texture2D overlayTexture;
        Color32[] overlayPixels;
        int drawnBlueprintVersion = -1;
        int drawnSquaresVersion = -1;
        int drawnRivalSquares = -1;

        public void Init(Game g)
        {
            game = g;
            map = g.Map;
            WorldLight.Tint = Color.white; // real 2D lights do day and night now

            chunksX = (map.Width + ChunkCells - 1) / ChunkCells;
            chunksY = (map.Height + ChunkCells - 1) / ChunkCells;
            chunks = new Chunk[chunksX, chunksY];
            for (int cy = 0; cy < chunksY; cy++)
            for (int cx = 0; cx < chunksX; cx++)
                chunks[cx, cy] = CreateChunk(cx, cy);

            overlayTexture = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            overlayPixels = new Color32[map.CellCount];
            var overlayGo = new GameObject("Overlay");
            overlayGo.transform.SetParent(transform, false);
            var overlay = overlayGo.AddComponent<SpriteRenderer>();
            overlay.sprite = Sprite.Create(overlayTexture, new Rect(0, 0, map.Width, map.Height), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
            overlay.sharedMaterial = SpriteFactory.UnlitMaterial;
            overlay.sortingOrder = 1;

            gameObject.AddComponent<WorldLighting>().Init(g);
            new GameObject("Props").AddComponent<PropLayer>().Init(map);
            map.CellChanged += OnCellChanged;
        }

        Chunk CreateChunk(int cx, int cy)
        {
            var cells = new RectInt(cx * ChunkCells, cy * ChunkCells,
                Mathf.Min(ChunkCells, map.Width - cx * ChunkCells), Mathf.Min(ChunkCells, map.Height - cy * ChunkCells));
            var chunk = new Chunk
            {
                Cells = cells,
                Pixels = new Color32[cells.width * Tile * cells.height * Tile],
                Texture = new Texture2D(cells.width * Tile, cells.height * Tile, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                },
            };
            for (int y = cells.yMin; y < cells.yMax; y++)
            for (int x = cells.xMin; x < cells.xMax; x++)
                PaintInto(chunk, x, y);
            chunk.Texture.SetPixels32(chunk.Pixels);
            chunk.Texture.Apply(false);

            var go = new GameObject($"Chunk {cx},{cy}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(cells.x, cells.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(chunk.Texture, new Rect(0, 0, chunk.Texture.width, chunk.Texture.height), Vector2.zero, Tile, 0, SpriteMeshType.FullRect);
            sr.sharedMaterial = SpriteFactory.LitMaterial;
            sr.sortingOrder = 0;
            return chunk;
        }

        void PaintInto(Chunk chunk, int x, int y)
        {
            int lx = x - chunk.Cells.x, ly = y - chunk.Cells.y;
            TilePainter.PaintCellHi(map, x, y, chunk.Pixels, chunk.Cells.width * Tile, lx * Tile, ly * Tile);
        }

        void OnDestroy()
        {
            if (map != null) map.CellChanged -= OnCellChanged;
        }

        // Neighbours matter too: walls join up and cast shadows on the cells around them.
        void OnCellChanged(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (map.InBounds(x + dx, y + dy)) pendingCells.Add(new Vector2Int(x + dx, y + dy));
        }

        void LateUpdate()
        {
            if (game == null || chunks == null) return; // state lost to a script reload during Play

            if (pendingCells.Count > 0)
            {
                foreach (var c in pendingCells)
                {
                    var chunk = chunks[c.x / ChunkCells, c.y / ChunkCells];
                    PaintInto(chunk, c.x, c.y);
                    chunk.Dirty = true;
                }
                pendingCells.Clear();
                foreach (var chunk in chunks)
                {
                    if (!chunk.Dirty) continue;
                    chunk.Texture.SetPixels32(chunk.Pixels);
                    chunk.Texture.Apply(false);
                    chunk.Dirty = false;
                }
            }

            if (game.Construction.Version != drawnBlueprintVersion || game.Org.SquaresVersion != drawnSquaresVersion || game.Rival.Squares.Count != drawnRivalSquares)
                RedrawOverlay();
        }

        void RedrawOverlay()
        {
            drawnBlueprintVersion = game.Construction.Version;
            drawnSquaresVersion = game.Org.SquaresVersion;
            drawnRivalSquares = game.Rival.Squares.Count;
            System.Array.Clear(overlayPixels, 0, overlayPixels.Length);
            foreach (var sq in game.Rival.Squares) overlayPixels[map.Index(sq.Cell.x, sq.Cell.y)] = RivalSquareColor;
            foreach (var sq in game.Org.Squares)
            {
                overlayPixels[map.Index(sq.Cell.x, sq.Cell.y)] = SquareColor;
                foreach (var post in sq.LookoutPosts) overlayPixels[map.Index(post.x, post.y)] = LookoutColor;
            }
            foreach (var bp in game.Construction.All)
                overlayPixels[map.Index(bp.Cell.x, bp.Cell.y)] = bp.Deconstruct ? DemolishBlueprint : BuildBlueprint;
            overlayTexture.SetPixels32(overlayPixels);
            overlayTexture.Apply(false);
        }
    }
}
