using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Deathville.Environment
{
    public class LevelGenerator : Node
    {
        private const int CHUNK_TILE_COUNT = 16;
        private const int TILE_SIZE = 16;
        private const int VOXEL_SIZE = 2;

        private const float NOISE_XSCALE = 20f;
        private const float NOISE_YSCALE = 60f;

        public Vector2 PlayerSpawnPosition { get; private set; }

        [Export]
        private int _minLevelPathLength = 3;
        [Export]
        private int _maxLevelPathLength = 5;
        [Export]
        private float _pathDirectionChangePercent = .25f;
        [Export]
        private int _minChunkWidth = 2;
        [Export]
        private int _maxChunkWidth = 4;
        [Export]
        private int _minChunkHeight = 2;
        [Export]
        private int _maxChunkHeight = 4;
        [Export]
        private NodePath _playerPath;

        private readonly Vector2[] _fourDirections = new Vector2[] { Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left };
        private RandomNumberGenerator _rng = new RandomNumberGenerator();
        private OpenSimplexNoise _noise = new OpenSimplexNoise();

        private struct LevelPathCell
        {
            public Vector2 Position;
            public Vector2 Direction;

            public LevelPathCell(Vector2 position, Vector2 direction)
            {
                Position = position;
                Direction = direction;
            }
        }

        private class PathChunkArea
        {
            public LevelPathCell LevelPathCell;
            public int HorizontalChunkCount;
            public int VerticalChunkCount;
            public Vector2 ChunkOffset;
        }

        public override void _Ready()
        {
            _rng.Randomize();
            var noiseTex = GetNode<Sprite>("Sprite").Texture as NoiseTexture;
            _noise = noiseTex.Noise;
            _noise.Seed = _rng.RandiRange(0, int.MaxValue);
        }

        public void Generate()
        {
            var path = GetLevelPath();

            var areas = new List<PathChunkArea>();
            foreach (var cell in path)
            {
                areas.Add(GenerateAreaForLevelPathCell(cell));
            }

            OffsetAreas(areas);
            FillAreas(areas);
        }

        private List<LevelPathCell> GetLevelPath()
        {
            var path = new List<LevelPathCell>();
            var occupiedCells = new HashSet<Vector2>();
            var numPathCells = _rng.RandiRange(_minLevelPathLength, _maxLevelPathLength);
            for (int i = 0; i < numPathCells; i++)
            {
                LevelPathCell cell;
                if (i == 0)
                {
                    cell = GetFirstPathPoint();
                }
                else
                {
                    cell = GetNextPathPoint(occupiedCells, path[i - 1]);
                }
                occupiedCells.Add(cell.Position);
                path.Add(cell);
            }
            return path;
        }

        private LevelPathCell GetFirstPathPoint()
        {
            return new LevelPathCell(Vector2.Zero, ChooseDirection(Vector2.Zero));
        }

        private LevelPathCell GetNextPathPoint(HashSet<Vector2> occupied, LevelPathCell prevCell)
        {
            var directionChange = _rng.Randf();
            var prevDirection = prevCell.Direction;
            var newDirection = prevDirection;
            if (directionChange < _pathDirectionChangePercent)
            {
                var directions = _fourDirections.OrderBy(x => _rng.Randf());
                foreach (var dir in directions)
                {
                    if (!occupied.Contains(prevCell.Position + dir))
                    {
                        newDirection = dir;
                        break;
                    }
                }
            }
            return new LevelPathCell(prevCell.Position + prevDirection, newDirection);
        }

        private Vector2 ChooseDirection(Vector2 excludeDirection)
        {
            var i = _rng.RandiRange(0, 3);
            if (_fourDirections[i] == excludeDirection)
            {
                i = Mathf.Wrap(i + 1, 0, 3);
            }
            return _fourDirections[i];
        }

        private PathChunkArea GenerateAreaForLevelPathCell(LevelPathCell levelPathCell)
        {
            var area = new PathChunkArea();
            area.HorizontalChunkCount = _rng.RandiRange(_minChunkWidth, _maxChunkWidth);
            area.VerticalChunkCount = _rng.RandiRange(_minChunkHeight, _maxChunkHeight);
            area.LevelPathCell = levelPathCell;
            return area;
        }

        private void OffsetAreas(List<PathChunkArea> areas)
        {
            for (int i = 0; i < areas.Count; i++)
            {
                if (i == 0) continue;
                var area = areas[i];
                var rootArea = areas[i - 1];

                if (rootArea.LevelPathCell.Direction == Vector2.Up)
                {
                    AlignAreasX(rootArea, area);
                    area.ChunkOffset.y = rootArea.ChunkOffset.y - area.VerticalChunkCount;
                }
                else if (rootArea.LevelPathCell.Direction == Vector2.Down)
                {
                    AlignAreasX(rootArea, area);
                    area.ChunkOffset.y = rootArea.ChunkOffset.y + rootArea.VerticalChunkCount;
                }
                else if (rootArea.LevelPathCell.Direction == Vector2.Left)
                {
                    AlignAreasY(rootArea, area);
                    area.ChunkOffset.x = rootArea.ChunkOffset.x - area.HorizontalChunkCount;
                }
                else if (rootArea.LevelPathCell.Direction == Vector2.Right)
                {
                    AlignAreasY(rootArea, area);
                    area.ChunkOffset.x = rootArea.ChunkOffset.x + rootArea.HorizontalChunkCount;
                }
            }
        }

        private void AlignAreasX(PathChunkArea rootChunkArea, PathChunkArea toAlignChunkArea)
        {
            toAlignChunkArea.ChunkOffset.x = rootChunkArea.ChunkOffset.x;
            toAlignChunkArea.ChunkOffset.x += _rng.RandiRange(-(toAlignChunkArea.HorizontalChunkCount - 1), rootChunkArea.HorizontalChunkCount - 1);
        }

        private void AlignAreasY(PathChunkArea rootChunkArea, PathChunkArea toAlignChunkArea)
        {
            toAlignChunkArea.ChunkOffset.y = rootChunkArea.ChunkOffset.y;
            toAlignChunkArea.ChunkOffset.y += _rng.RandiRange(-(toAlignChunkArea.VerticalChunkCount - 1), rootChunkArea.VerticalChunkCount - 1);
        }

        private Rect2 GetBoundingArea(IEnumerable<PathChunkArea> areas)
        {
            var firstArea = areas.ElementAt(0);
            var boundingRect = new Rect2(firstArea.ChunkOffset, new Vector2(firstArea.HorizontalChunkCount, firstArea.VerticalChunkCount));
            foreach (var area in areas)
            {
                boundingRect = boundingRect.Merge(new Rect2(area.ChunkOffset, new Vector2(area.HorizontalChunkCount, area.VerticalChunkCount)));
            }
            // boundingRect = boundingRect.Grow(1f);
            return boundingRect;
        }

        private void FillAreas(IEnumerable<PathChunkArea> areas)
        {
            foreach (var area in areas)
            {
                FillArea(area);
            }
        }

        private void FillArea(PathChunkArea area)
        {
            for (int x = (int) area.ChunkOffset.x; x < area.ChunkOffset.x + area.HorizontalChunkCount; x++)
            {
                for (int y = (int) area.ChunkOffset.y; y < area.ChunkOffset.y + area.VerticalChunkCount; y++)
                {
                    var chunkPos = new Vector2(x, y);
                    FillChunk(chunkPos);
                }
            }
        }

        private void FillBoundingArea(Rect2 boundingArea)
        {
            for (int x = (int) boundingArea.Position.x; x < boundingArea.Position.x + boundingArea.Size.x; x++)
            {
                for (int y = (int) boundingArea.Position.y; y < boundingArea.Position.y + boundingArea.Size.y; y++)
                {
                    var chunkPos = new Vector2(x, y);
                    FillChunk(chunkPos);
                }
            }
        }

        private void FillChunk(Vector2 chunkPosition)
        {
            var offset = chunkPosition * CHUNK_TILE_COUNT;
            for (int x = 0; x < CHUNK_TILE_COUNT; x++)
            {
                for (int y = 0; y < CHUNK_TILE_COUNT; y++)
                {
                    var tilePos = new Vector2(x, y) + offset;
                    if (GetAverageValue(tilePos, NOISE_XSCALE, NOISE_YSCALE) > 0.06f)
                    {
                        FillVoxel(tilePos);
                    }
                    else if (PlayerSpawnPosition == Vector2.Zero)
                    {
                        PlayerSpawnPosition = tilePos * VOXEL_SIZE * TILE_SIZE;
                        if (PlayerSpawnPosition != Vector2.Zero)
                        {
                            PlayerSpawnPosition += Vector2.Down * 16f;
                        }
                    }
                }
            }
        }

        private float GetAverageValue(Vector2 tilePos, float xScale, float yScale)
        {
            var sum = 0f;
            var scale = new Vector2(xScale, yScale);
            tilePos *= scale;
            foreach (var dir in _fourDirections)
            {
                sum += _noise.GetNoise2dv(tilePos + dir * scale) * .1f;
            }
            sum += _noise.GetNoise2dv(tilePos);
            return sum / (_fourDirections.Length + 1);
        }

        private void FillVoxel(Vector2 tilePos)
        {
            var scaled = tilePos * VOXEL_SIZE;
            Zone.Current.TileMap.SetCellv(scaled, 0);
            Zone.Current.TileMap.UpdateBitmaskArea(scaled);

            Zone.Current.TileMap.SetCellv(scaled + Vector2.Right, 0);
            Zone.Current.TileMap.UpdateBitmaskArea(scaled + Vector2.Right);

            Zone.Current.TileMap.SetCellv(scaled + Vector2.Down, 0);
            Zone.Current.TileMap.UpdateBitmaskArea(scaled + Vector2.Down);

            Zone.Current.TileMap.SetCellv(scaled + Vector2.Down + Vector2.Right, 0);
            Zone.Current.TileMap.UpdateBitmaskArea(scaled + Vector2.Down + Vector2.Right);
        }
    }
}