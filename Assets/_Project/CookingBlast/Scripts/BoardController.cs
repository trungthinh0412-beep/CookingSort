using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CookingSort2D
{
    public class BoardController : MonoBehaviour
    {
        private enum TileSelectionMode
        {
            RowByRowLeftToRight,
            WeightedFarther
        }

        private const int DominoRowComplete = -2;
        
        [Header("Tile Prefabs By Match Type")]
        [Tooltip("Prefab list order is arbitrary. Matching uses each Tile prefab's Type; variants may share a Type.")]
        [FormerlySerializedAs("tilePrefabsByColor")]
        [SerializeField] private FoodTileView[] tilePrefabsByType = new FoodTileView[10];
[SerializeField] private FoodTileView tilePrefab;
        [SerializeField] private Transform tileRoot;
        [Tooltip("Fallback size used only when a Tile has no SpriteRenderer sprite.")]
        [SerializeField, Min(0.01f)] private float cellSize = 0.8f;
        [FormerlySerializedAs("gap")]
        [Tooltip("Horizontal space added to the measured sprite width. Use a negative value to overlap Tiles.")]
        [SerializeField, Range(-0.5f, 0.5f)] private float gapX = 0f;
        [Tooltip("Vertical space added to the measured sprite height. Use a negative value to overlap rows.")]
        [SerializeField, Range(-1f, 0.5f)] private float gapY = -0.28f;

        [Header("Front row shadow")]
        [Tooltip("Shadow displayed beneath the currently exposed Tile in each column.")]
        [SerializeField] private Sprite frontRowShadowSprite;
        [Tooltip("How far the shadow overlaps the bottom edge of its Tile.")]
        [SerializeField, Min(0f)] private float frontRowShadowOverlap = 0.08f;

        [Header("Tile flow")]
        [Tooltip("Extra pause after a column finishes sliding down. Keep at 0 for an immediate drop-to-flight handoff.")]
        [SerializeField, Min(0f)] private float serveInterval = 0f;
        [SerializeField, Min(0.01f)] private float dropDuration = 0.13f;

        [Header("Level intro")]
        [Tooltip("Drop the Board into place in alternating left-to-right / right-to-left rows when a level starts.")]
        [SerializeField] private bool playIntroAnimation = true;
        [Tooltip("Only this many rows from the bottom play the level intro. Higher rows start in place.")]
        [SerializeField, Min(1)] private int introAnimatedRows = 18;
        [Tooltip("How far above the highest Board row each block starts.")]
        [SerializeField, Min(0f)] private float introDropHeight = 4f;
        [Tooltip("Seconds each block takes to reach its authored Board position.")]
        [SerializeField, Min(0.01f)] private float introDropDuration = 0.06f;
        [Tooltip("Delay between consecutive blocks. Keep this small for a fast domino effect.")]
        [SerializeField, Min(0f)] private float introStagger = 0.008f;
        [Tooltip("How far an intro block drops below its final position before bouncing back.")]
        [SerializeField, Min(0f)] private float introOvershootDistance = 0.1f;
        [Tooltip("Seconds each block gently settles after landing during the level intro.")]
        [SerializeField, Min(0f)] private float introElasticityDuration = 0.09f;
        [Tooltip("Small squash and stretch amount when an intro block lands.")]
        [SerializeField, Range(0f, 0.25f)] private float introElasticityStrength = 0.055f;

        [Header("Drop elasticity")]
        [Tooltip("Seconds blocks keep bouncing after they fall to fill an eaten Tile's space.")]
        [SerializeField, Min(0f)] private float dropElasticityDuration = 0.06f;
        [Tooltip("Squash and stretch amount for blocks falling after an Animal eats a Tile.")]
        [SerializeField, Range(0f, 0.25f)] private float dropElasticityStrength = 0.012f;

        [Header("Tile scanning")]
        [Tooltip("Row By Row finishes the current front row from left to right before returning to the left side for the next row. Weighted Farther preserves the previous random distance-based behavior.")]
        [SerializeField] private TileSelectionMode tileSelectionMode =
            TileSelectionMode.RowByRowLeftToRight;
        [Tooltip("Minimum delay between consecutive launches while using Domino Left To Right.")]
        [SerializeField, Min(0f)] private float dominoLaunchInterval = 0.04f;
        [Tooltip("Starting delay between tile launches for each customer")]
        [SerializeField, Min(0f)] private float scanInterval = 0.14f;
        [Tooltip("Fastest interval for a customer after building combo")]
        [SerializeField, Min(0f)] private float minimumScanInterval = 0.06f;
        [Tooltip("Seconds removed from a customer's scan interval per delivered tile. Smaller values make the launch cadence accelerate more gradually.")]
        [SerializeField, Min(0f)] private float scanAccelerationPerTile = 0.003f;
        [Tooltip("Selection weight for the nearest available Tile/Cluster source.")]
        [SerializeField, Min(0.01f)] private float nearSourceWeight = 1f;
        [Tooltip("Selection weight for the farthest source. 2 versus 1 gives roughly 66% far / 33% near.")]
        [SerializeField, Min(0.01f)] private float farSourceWeight = 2f;

        
        private readonly List<CustomerView> _customerBuffer = new List<CustomerView>();
        private readonly List<List<FoodTileView>> _columns = new List<List<FoodTileView>>();
        private sealed class RuntimeCluster
        {
            public BoardClusterData Data;
            public FoodTileView View;
            public int Remaining;
            public int InFlight;
            public bool Cleared;
            public int[] TilesBelowByColumn;
            public int[] RemovedTilesBelowByColumn;
        }
        private sealed class ServeCandidate
        {
            public RuntimeCluster Cluster;
            public FoodTileView Tile;
            public int Column = -1;
            public FoodColor Type;
            public Vector3 Position;
            public bool IsCluster => Cluster != null;
        }
        private sealed class IntroBlock
        {
            public Transform Transform;
            public Vector3 Target;
            public int Row;
            public float Column;
        }
        private readonly List<ServeCandidate> _serveCandidates = new List<ServeCandidate>();
        private readonly List<RuntimeCluster> _clusters = new List<RuntimeCluster>();
        private readonly List<IntroBlock> _introBlocks = new List<IntroBlock>();
        private readonly Dictionary<Transform, Coroutine> _clusterMoveRoutines =
            new Dictionary<Transform, Coroutine>();
private readonly List<bool> _columnBusy = new List<bool>();
        private readonly Dictionary<CustomerView, int> _customerFlights = new Dictionary<CustomerView, int>();
        private readonly Dictionary<CustomerView, int> _customerDeliveredCount = new Dictionary<CustomerView, int>();
        private readonly Dictionary<CustomerView, float> _customerNextScanAt = new Dictionary<CustomerView, float>();
        private readonly Dictionary<FoodColor, int> _typeRoundRobinCursor = new Dictionary<FoodColor, int>();
        private float _nextDominoLaunchAt;
        private int _dominoNextColumn;
        private CookingSortGameController _game;
        private bool _serving;
        private bool _introAnimating;
        private int _maxRows;
        private Vector2 _layoutCellSize;

        public bool IsServing => _serving;
        public bool IsIntroAnimating => _introAnimating;
        public int ConfiguredTilePrefabCount =>
            tilePrefabsByType != null ? tilePrefabsByType.Length : 0;

        /// <summary>Returns only the prefab explicitly stored in the list; never uses TileBase.</summary>
public FoodTileView GetConfiguredTilePrefab(int index)
        {
            // The array is a prefab list, NOT an array indexed by FoodColor.
            // Multiple prefabs can share one match type (e.g. 1x1 and 2x1 Black).
            return tilePrefabsByType != null && index >= 0 &&
                index < tilePrefabsByType.Length ? tilePrefabsByType[index] : null;
        }

        public int RemainingTileCount
        {
            get
            {
                int total = 0;
                for (int col = 0; col < _columns.Count; col++)
                    for (int row = 0; row < _columns[col].Count; row++)
                        if (_columns[col][row] != null &&
                            _columns[col][row].Type != FoodColor.Neutral)
                            total++;
                for (int i = 0; i < _clusters.Count; i++) total += Mathf.Max(0, _clusters[i].Remaining);
                return total;
            }
        }
        public bool IsEmpty => RemainingTileCount == 0;

        public void GetBottomTypeWeights(Dictionary<FoodColor, int> results)
        {
            if (results == null) return;
            results.Clear();
            for (int col = 0; col < _columns.Count; col++)
            {
                if (IsColumnBlockedByCluster(col) || _columns[col].Count == 0) continue;
                FoodTileView tile = _columns[col][0];
                if (tile == null) continue;
                results.TryGetValue(tile.Type, out int count);
                results[tile.Type] = count + 1;
            }
            for (int i = 0; i < _clusters.Count; i++)
            {
                RuntimeCluster cluster = _clusters[i];
                if (!IsClusterExposed(cluster) || cluster.Data == null) continue;
                results.TryGetValue(cluster.Data.color, out int count);
                results[cluster.Data.color] = count + Mathf.Max(1, cluster.Data.width);
            }
        }

        public void HandleTileClick(FoodTileView tile)
        {
            if (_game != null) _game.HandleBoosterTileClick(tile);
        }

        public bool TryTrashConnected(FoodTileView selected)
        {
            if (selected == null || _introAnimating) return false;

            // Build the current logical board surface. A large footprint owns
            // every cell covered by its width and height.
            var cells = new Dictionary<Vector2Int, object>();
            object selectedSource = null;
            FoodColor selectedType = default;
            for (int col = 0; col < _columns.Count; col++)
            {
                for (int row = 0; row < _columns[col].Count; row++)
                {
                    FoodTileView tile = _columns[col][row];
                    if (tile == null) continue;
                    cells[new Vector2Int(col, GetTileDisplayRow(col, row))] = tile;
                    if (tile == selected)
                    {
                        selectedSource = tile;
                        selectedType = tile.Type;
                    }
                }
            }
            for (int i = 0; i < _clusters.Count; i++)
            {
                RuntimeCluster cluster = _clusters[i];
                if (cluster == null || cluster.Remaining <= 0 || cluster.Data == null) continue;
                int start = Mathf.Max(0, cluster.Data.anchorColumn);
                int end = Mathf.Min(_columns.Count, start + Mathf.Max(1, cluster.Data.width));
                int bottom = GetClusterBottomRow(cluster);
                for (int col = start; col < end; col++)
                    for (int row = bottom;
                         row < bottom + Mathf.Max(1, cluster.Data.height); row++)
                        cells[new Vector2Int(col, row)] = cluster;
                if (cluster.View == selected)
                {
                    selectedSource = cluster;
                    selectedType = cluster.Data.color;
                }
            }
            if (selectedSource == null) return false;

            var cellsToVisit = new Queue<Vector2Int>();
            var visitedCells = new HashSet<Vector2Int>();
            var sources = new HashSet<object>();
            foreach (KeyValuePair<Vector2Int, object> pair in cells)
                if (ReferenceEquals(pair.Value, selectedSource)) cellsToVisit.Enqueue(pair.Key);

            while (cellsToVisit.Count > 0)
            {
                Vector2Int cell = cellsToVisit.Dequeue();
                if (!visitedCells.Add(cell) || !cells.TryGetValue(cell, out object source))
                    continue;
                FoodColor type = source is FoodTileView tile
                    ? tile.Type : ((RuntimeCluster)source).Data.color;
                if (type != selectedType) continue;
                sources.Add(source);
                cellsToVisit.Enqueue(cell + Vector2Int.left);
                cellsToVisit.Enqueue(cell + Vector2Int.right);
                cellsToVisit.Enqueue(cell + Vector2Int.up);
                cellsToVisit.Enqueue(cell + Vector2Int.down);
            }
            if (sources.Count == 0) return false;

            int removed = 0;
            var affectedColumns = new HashSet<int>();
            foreach (object source in sources)
            {
                if (source is FoodTileView tile)
                {
                    for (int col = 0; col < _columns.Count; col++)
                    {
                        int row = _columns[col].IndexOf(tile);
                        if (row < 0) continue;
                        RegisterRegularTileRemoved(col, row);
                        _columns[col].RemoveAt(row);
                        affectedColumns.Add(col);
                        removed++;
                        Destroy(tile.gameObject);
                        break;
                    }
                }
                else
                {
                    RuntimeCluster cluster = (RuntimeCluster)source;
                    int value = Mathf.Max(0, cluster.Remaining);
                    removed += value;
                    cluster.Remaining = 0;
                    cluster.Cleared = true;
                    if (cluster.View != null) Destroy(cluster.View.gameObject);
                    for (int col = cluster.Data.anchorColumn;
                         col < cluster.Data.anchorColumn + cluster.Data.width; col++)
                        if (col >= 0 && col < _columns.Count) affectedColumns.Add(col);
                }
            }

            var falls = new List<Coroutine>();
            foreach (int col in affectedColumns) LayoutColumn(col, true, falls);
            LayoutClusters(true, falls);
            StartCoroutine(FinishTrashRoutine(falls, removed, selectedType));
            return removed > 0;
        }

        private IEnumerator FinishTrashRoutine(List<Coroutine> falls, int removed, FoodColor type)
        {
            if (falls != null)
                for (int i = 0; i < falls.Count; i++)
                    if (falls[i] != null) yield return falls[i];
            if (_game != null) _game.OnTrashAwayFinished(removed, type);
        }

        private void Awake()
        {
            if (tileRoot == null)
            {
                Transform t = transform.Find("Tiles");
                if (t != null) tileRoot = t;
            }
        }

public FoodTileView GetTilePrefab(FoodColor color)
        {
            if (tilePrefabsByType != null)
                for (int i = 0; i < tilePrefabsByType.Length; i++)
                    if (tilePrefabsByType[i] != null && tilePrefabsByType[i].Type == color)
                        return tilePrefabsByType[i];
            // Backwards compatibility for colors without custom sprites.
            return tilePrefab != null && tilePrefab.Type == color ? tilePrefab : null;
        }

public FoodTileView GetClusterPrefab(BoardClusterData cluster)
        {
            if (cluster == null) return null;
            FoodTileView exact = cluster.tilePrefab;
            if (exact != null && exact.Type == cluster.color &&
                exact.FootprintWidth == cluster.width &&
                exact.FootprintHeight == cluster.height &&
                exact.FootprintValue == cluster.value &&
                tilePrefabsByType != null)
            {
                for (int i = 0; i < tilePrefabsByType.Length; i++)
                    if (tilePrefabsByType[i] == exact) return exact;
            }
            return GetTilePrefab(cluster.color, cluster.width,
                cluster.height, cluster.value);
        }


public FoodTileView GetTilePrefab(FoodColor color,
            int footprintWidth, int footprintHeight, int footprintValue)
        {
            // Prefer the actual authored prefab variant for an Editor footprint.
            // Several prefabs may share a match type but have different shapes.
            if (tilePrefabsByType != null)
                for (int i = 0; i < tilePrefabsByType.Length; i++)
                {
                    FoodTileView candidate = tilePrefabsByType[i];
                    if (candidate != null && candidate.Type == color &&
                        candidate.FootprintWidth == footprintWidth &&
                        candidate.FootprintHeight == footprintHeight &&
                        candidate.FootprintValue == footprintValue)
                        return candidate;
                }
            return GetTilePrefab(color);
        }



public void Initialize(CookingSortGameController game, CookingBlastLevel data)
        {
            _game = game;
            if (tileRoot == null)
            {
                Transform t = transform.Find("Tiles");
                if (t != null) tileRoot = t;
            }
            ClearBoard();
            if (data == null || tileRoot == null) return;

            _maxRows = 0;
            for (int col = 0; col < data.BoardColumns.Count; col++)
            {
                BoardColumnData columnData = data.BoardColumns[col];
                int count = columnData != null && columnData.bottomToTop != null ? columnData.bottomToTop.Count : 0;
                _maxRows = Mathf.Max(_maxRows, count);
                var column = new List<FoodTileView>();
                _columns.Add(column);
                
_columnBusy.Add(false);
                for (int row = 0; row < count; row++)
                {
                    if (data.IsClusterCell(col, row, out _)) continue;
                    FoodColor kind = columnData.bottomToTop[row];
                    // Neutral is editor-only padding. It has no runtime view
                    // and all playable Tiles compact around it.
                    if (kind == FoodColor.Neutral) continue;
                    FoodTileView prefab = GetTilePrefab(kind);
                    if (prefab == null)
                    {
                        Debug.LogError("[CookingBlast] Missing Tile prefab for " + kind + " on " + name);
                        continue;
                    }
                    FoodTileView tile = Instantiate(prefab, tileRoot);
                    // Keep the prefab's authored scale so every tile uses the
                    // same visual size as its source prefab (Tile 8 included).
                    tile.Setup(kind);
                    column.Add(tile);
                }
            }
            if (data.BoardClusters != null)
            {
                for (int i = 0; i < data.BoardClusters.Count; i++)
                {
                    BoardClusterData cluster = data.BoardClusters[i];
                    if (cluster == null) continue;
                    FoodTileView prefab = GetClusterPrefab(cluster);
                    if (prefab == null) continue;
                    FoodTileView view = Instantiate(prefab, tileRoot);
                    view.SetupCluster(cluster.color, cluster.value, cluster.width, cluster.height);
                    _clusters.Add(new RuntimeCluster
                    {
                        Data = cluster,
                        View = view,
                        Remaining = cluster.value,
                        TilesBelowByColumn = CountPlayableTilesBelow(data, cluster),
                        RemovedTilesBelowByColumn = new int[Mathf.Max(0, cluster.width)]
                    });
                }
            }
            MeasureLayoutCellSize();
            LayoutAll(false);
            LayoutClusters();
            if (playIntroAnimation)
                StartIntroAnimation();
        }

        public void BeginServeLoop(SeatBar seats)
        {
            if (_serving || _introAnimating || seats == null || !isActiveAndEnabled) return;
            StartCoroutine(ServeRoutine(seats));
        }

        public bool HasServiceableTile(SeatBar seats)
        {
            if (seats == null) return false;
            for (int i = 0; i < _clusters.Count; i++)
            {
                RuntimeCluster cluster = _clusters[i];
                if (cluster.Remaining > 0 && IsClusterExposed(cluster) &&
                    FindFreeCustomer(seats, cluster.Data.color) != null)
                    return true;
            }
            for (int col = 0; col < _columns.Count; col++)
            {
                if (IsColumnBlockedByCluster(col)) continue;
                if (_columns[col].Count == 0) continue;
                FoodTileView bottom = _columns[col][0];
                if (bottom != null && seats.HasCustomerForType(bottom.Type)) return true;
            }
            return false;
        }

public void ClearBoard()
        {
            _serving = false;
            _introAnimating = false;
            StopAllCoroutines();
            _columns.Clear();
            _clusters.Clear();
            _introBlocks.Clear();
            _clusterMoveRoutines.Clear();
_columnBusy.Clear();
            _customerFlights.Clear();
            _customerDeliveredCount.Clear();
            _customerNextScanAt.Clear();
            _typeRoundRobinCursor.Clear();
            _nextDominoLaunchAt = 0f;
            _dominoNextColumn = 0;
            if (tileRoot == null) return;
            for (int i = tileRoot.childCount - 1; i >= 0; i--)
                Destroy(tileRoot.GetChild(i).gameObject);
        }

private IEnumerator ServeRoutine(SeatBar seats)
        {
            _serving = true;

            while (true)
            {
                bool anyColumnBusy = false;
                bool startedAny = false;

                // Repair missing tiles before selecting the next exposed layer.
                for (int col = 0; col < _columns.Count; col++)
                {
                    if (IsColumnBlockedByCluster(col)) continue;
                    if (_columnBusy[col])
                    {
                        anyColumnBusy = true;
                        continue;
                    }

                    List<FoodTileView> column = _columns[col];
                    bool removedMissingTile = false;
                    while (column.Count > 0 && column[0] == null)
                    {
                        RegisterRegularTileRemoved(col, 0);
                        column.RemoveAt(0);
                        removedMissingTile = true;
                    }
                    if (removedMissingTile)
                    {
                        _columnBusy[col] = true;
                        anyColumnBusy = true;
                        StartCoroutine(WaitForColumnFall(col));
                    }
                }

                _serveCandidates.Clear();
                for (int i = 0; i < _clusters.Count; i++)
                {
                    RuntimeCluster cluster = _clusters[i];
                    if (cluster == null || cluster.Remaining <= 0 || !IsClusterExposed(cluster)) continue;
                    _serveCandidates.Add(new ServeCandidate
                    {
                        Cluster = cluster,
                        Type = cluster.Data.color,
                        Position = cluster.View != null ? cluster.View.transform.position : transform.position
                    });
                    if (cluster.InFlight > 0) anyColumnBusy = true;
                }

                for (int col = 0; col < _columns.Count; col++)
                {
                    if (_columnBusy[col] || IsColumnBlockedByCluster(col)) continue;
                    List<FoodTileView> column = _columns[col];
                    if (column.Count == 0 || column[0] == null) continue;
                    FoodTileView tile = column[0];
                    _serveCandidates.Add(new ServeCandidate
                    {
                        Tile = tile,
                        Column = col,
                        Type = tile.Type,
                        Position = tile.transform.position
                    });
                }

                // Tile and Cluster sources share one scheduler. Domino mode
                // launches one serviceable source at a time from left to right.
                while (_serveCandidates.Count > 0)
                {
                    CustomerView customer = null;
                    int chosenIndex;
                    if (tileSelectionMode == TileSelectionMode.RowByRowLeftToRight)
                    {
                        if (Time.unscaledTime < _nextDominoLaunchAt) break;
                        chosenIndex = FindNextDominoCandidate(seats, out customer);
                        if (chosenIndex == DominoRowComplete)
                        {
                            if (!anyColumnBusy) _dominoNextColumn = 0;
                            break;
                        }
                    }
                    else
                        chosenIndex = FindWeightedServiceableCandidate(seats, out customer);
                    if (chosenIndex < 0 || customer == null) break;
                    ServeCandidate chosen = _serveCandidates[chosenIndex];
                    _serveCandidates.RemoveAt(chosenIndex);

                    _customerFlights.TryGetValue(customer, out int inFlight);
                    _customerFlights[customer] = inFlight + 1;
                    _customerNextScanAt[customer] = Time.unscaledTime + GetCustomerScanInterval(customer);

                    if (chosen.IsCluster)
                    {
                        chosen.Cluster.Remaining--;
                        chosen.Cluster.InFlight++;
                        if (chosen.Cluster.View != null)
                            chosen.Cluster.View.SetClusterValue(chosen.Cluster.Remaining);
                        if (_game != null) _game.OnTileLaunched();
                        StartCoroutine(ServeClusterUnit(chosen.Cluster, customer, seats));
                    }
                    else
                    {
                        List<FoodTileView> column = _columns[chosen.Column];
                        if (column.Count == 0 || column[0] != chosen.Tile) continue;
                        RegisterRegularTileRemoved(chosen.Column, 0);
                        column.RemoveAt(0);
                        _columnBusy[chosen.Column] = true;
                        anyColumnBusy = true;
                        if (_game != null) _game.OnTileLaunched();
                        StartCoroutine(ServeSingleTile(chosen.Column, chosen.Tile, customer, seats));
                    }
                    startedAny = true;
                    if (tileSelectionMode == TileSelectionMode.RowByRowLeftToRight)
                    {
                        int sourceStartColumn = GetCandidateStartColumn(chosen);
                        int sourceEndColumn = GetCandidateEndColumn(chosen);
                        bool sourceFinished = !chosen.IsCluster ||
                                              chosen.Cluster.Remaining <= 0;
                        _dominoNextColumn = sourceFinished
                            ? sourceEndColumn + 1
                            : sourceStartColumn;
                        _nextDominoLaunchAt = Time.unscaledTime +
                            Mathf.Max(0f, dominoLaunchInterval);
                        break;
                    }
                }

                // Keep scanning while any customer is cooling down or while
                // outstanding flights are still moving towards seats.
                bool hasPotentialMatch = false;
                for (int i = 0; i < _serveCandidates.Count && !hasPotentialMatch; i++)
                    hasPotentialMatch = seats.HasCustomerForType(_serveCandidates[i].Type);
                if (!anyColumnBusy && !startedAny && _customerFlights.Count == 0 &&
                    !hasPotentialMatch && !HasServiceableTile(seats))
                    break;

                yield return null;
            }

            _serving = false;
            if (_game != null) _game.OnServeLoopFinished();
        }

        private int FindNextDominoCandidate(
            SeatBar seats, out CustomerView customer)
        {
            customer = null;
            int chosenIndex = -1;
            int chosenColumn = int.MaxValue;
            for (int i = 0; i < _serveCandidates.Count; i++)
            {
                int candidateColumn = GetCandidateStartColumn(_serveCandidates[i]);
                if (candidateColumn < _dominoNextColumn ||
                    candidateColumn >= chosenColumn)
                    continue;
                chosenIndex = i;
                chosenColumn = candidateColumn;
            }

            if (chosenIndex < 0) return DominoRowComplete;

            ServeCandidate candidate = _serveCandidates[chosenIndex];
            if (!seats.HasCustomerForType(candidate.Type))
            {
                _dominoNextColumn = GetCandidateEndColumn(candidate) + 1;
                return FindNextDominoCandidate(seats, out customer);
            }

            customer = FindFreeCustomer(seats, candidate.Type);
            return customer != null ? chosenIndex : -1;
        }

        private static int GetCandidateStartColumn(ServeCandidate candidate)
        {
            return candidate != null && candidate.IsCluster &&
                   candidate.Cluster.Data != null
                ? candidate.Cluster.Data.anchorColumn
                : candidate != null ? candidate.Column : int.MaxValue;
        }

        private static int GetCandidateEndColumn(ServeCandidate candidate)
        {
            if (candidate == null) return int.MaxValue;
            if (!candidate.IsCluster || candidate.Cluster.Data == null)
                return candidate.Column;
            return candidate.Cluster.Data.anchorColumn +
                   Mathf.Max(1, candidate.Cluster.Data.width) - 1;
        }

        private int FindWeightedServiceableCandidate(
            SeatBar seats, out CustomerView customer)
        {
            customer = null;
            FoodColor selectedType = default;
            for (int i = 0; i < _serveCandidates.Count; i++)
            {
                selectedType = _serveCandidates[i].Type;
                customer = FindFreeCustomer(seats, selectedType);
                if (customer != null) break;
            }
            return customer != null
                ? ChooseWeightedSourceIndex(selectedType, customer)
                : -1;
        }

        private int ChooseWeightedSourceIndex(FoodColor type, CustomerView customer)
        {
            float minDistance = float.MaxValue;
            float maxDistance = 0f;
            for (int i = 0; i < _serveCandidates.Count; i++)
            {
                if (_serveCandidates[i].Type != type) continue;
                float distance = Vector2.Distance(_serveCandidates[i].Position, customer.transform.position);
                minDistance = Mathf.Min(minDistance, distance);
                maxDistance = Mathf.Max(maxDistance, distance);
            }

            float range = maxDistance - minDistance;
            float totalWeight = 0f;
            for (int i = 0; i < _serveCandidates.Count; i++)
            {
                if (_serveCandidates[i].Type != type) continue;
                float distance = Vector2.Distance(_serveCandidates[i].Position, customer.transform.position);
                totalWeight += range > 0.0001f
                    ? Mathf.Lerp(nearSourceWeight, farSourceWeight,
                        (distance - minDistance) / range)
                    : nearSourceWeight;
            }
            if (totalWeight <= 0f) return -1;

            float pick = Random.value * totalWeight;
            int fallback = -1;
            for (int i = 0; i < _serveCandidates.Count; i++)
            {
                if (_serveCandidates[i].Type != type) continue;
                fallback = i;
                float distance = Vector2.Distance(_serveCandidates[i].Position, customer.transform.position);
                pick -= range > 0.0001f
                    ? Mathf.Lerp(nearSourceWeight, farSourceWeight,
                        (distance - minDistance) / range)
                    : nearSourceWeight;
                if (pick <= 0f) return i;
            }
            return fallback;
        }

        private void LayoutAll(bool animate)
        {
            for (int col = 0; col < _columns.Count; col++) LayoutColumn(col, animate);
        }

        private void StartIntroAnimation()
        {
            _introBlocks.Clear();
            float highestTargetY = float.MinValue;
            int bottomRow = int.MaxValue;

            for (int col = 0; col < _columns.Count; col++)
            {
                List<FoodTileView> column = _columns[col];
                for (int row = 0; row < column.Count; row++)
                {
                    FoodTileView tile = column[row];
                    if (tile == null) continue;
                    int displayRow = GetTileDisplayRow(col, row);
                    Vector3 target = tile.transform.localPosition;
                    _introBlocks.Add(new IntroBlock
                    {
                        Transform = tile.transform,
                        Target = target,
                        Row = displayRow,
                        Column = col
                    });
                    highestTargetY = Mathf.Max(highestTargetY, target.y);
                    bottomRow = Mathf.Min(bottomRow, displayRow);
                }
            }

            for (int i = 0; i < _clusters.Count; i++)
            {
                RuntimeCluster cluster = _clusters[i];
                if (cluster == null || cluster.View == null || cluster.Data == null) continue;
                BoardClusterData data = cluster.Data;
                int displayRow = GetClusterBottomRow(cluster) + Mathf.Max(1, data.height) - 1;
                Vector3 target = cluster.View.transform.localPosition;
                _introBlocks.Add(new IntroBlock
                {
                    Transform = cluster.View.transform,
                    Target = target,
                    Row = displayRow,
                    Column = data.anchorColumn + (Mathf.Max(1, data.width) - 1) * 0.5f
                });
                highestTargetY = Mathf.Max(highestTargetY, target.y);
                bottomRow = Mathf.Min(bottomRow, displayRow);
            }

            if (_introBlocks.Count == 0) return;

            int firstRow = bottomRow;
            int rowLimit = firstRow + Mathf.Max(1, introAnimatedRows);
            _introBlocks.RemoveAll(block => block.Row >= rowLimit);
            highestTargetY = float.MinValue;
            for (int i = 0; i < _introBlocks.Count; i++)
                highestTargetY = Mathf.Max(highestTargetY, _introBlocks[i].Target.y);
            if (_introBlocks.Count == 0) return;
            _introBlocks.Sort((a, b) =>
            {
                // Build the Board upward: the bottom row lands first, while
                // every individual block still slides down from above.
                int rowOrder = a.Row.CompareTo(b.Row);
                if (rowOrder != 0) return rowOrder;
                bool leftToRight = ((a.Row - firstRow) & 1) == 0;
                return leftToRight
                    ? a.Column.CompareTo(b.Column)
                    : b.Column.CompareTo(a.Column);
            });

            float spawnY = highestTargetY + Mathf.Max(0f, introDropHeight);
            for (int i = 0; i < _introBlocks.Count; i++)
            {
                IntroBlock block = _introBlocks[i];
                if (block.Transform == null) continue;
                Vector3 start = block.Target;
                start.y = spawnY;
                block.Transform.localPosition = start;
                block.Transform.gameObject.SetActive(false);
            }

            _introAnimating = true;
            StartCoroutine(PlayIntroRoutine());
        }

        private IEnumerator PlayIntroRoutine()
        {
            float stagger = Mathf.Max(0f, introStagger);
            for (int i = 0; i < _introBlocks.Count; i++)
            {
                IntroBlock block = _introBlocks[i];
                if (block.Transform != null)
                {
                    block.Transform.gameObject.SetActive(true);
                    StartCoroutine(MoveIntroBlockRoutine(block.Transform, block.Target));
                }
                if (stagger > 0f && i < _introBlocks.Count - 1)
                    yield return new WaitForSecondsRealtime(stagger);
            }

            yield return new WaitForSecondsRealtime(
                Mathf.Max(0.01f, introDropDuration) + Mathf.Max(0f, introElasticityDuration));
            _introAnimating = false;
            _introBlocks.Clear();
        }

        private IEnumerator MoveIntroBlockRoutine(Transform targetTransform, Vector3 target)
        {
            if (targetTransform == null) yield break;
            Vector3 start = targetTransform.localPosition;
            Vector3 authoredScale = targetTransform.localScale;
            Vector3 overshootTarget = target + Vector3.down * Mathf.Max(0f, introOvershootDistance);
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, introDropDuration);
            while (elapsed < duration && targetTransform != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                targetTransform.localPosition = Vector3.LerpUnclamped(start, overshootTarget, eased);

                float strength = Mathf.Max(0f, introElasticityStrength);
                float airStretch = Mathf.Sin(t * Mathf.PI) * strength * 0.25f;
                float landingSquash = t * t * strength * 0.55f;
                targetTransform.localScale = new Vector3(
                    authoredScale.x * (1f - airStretch + landingSquash),
                    authoredScale.y * (1f + airStretch - landingSquash),
                    authoredScale.z);
                yield return null;
            }
            if (targetTransform == null) yield break;

            targetTransform.localPosition = overshootTarget;
            elapsed = 0f;
            float elasticityDuration = Mathf.Max(0f, introElasticityDuration);
            while (elapsed < elasticityDuration && targetTransform != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, elasticityDuration));
                float returnEased = 1f - Mathf.Pow(1f - t, 3f);
                targetTransform.localPosition =
                    Vector3.LerpUnclamped(overshootTarget, target, returnEased);

                float wobble = Mathf.Cos(t * Mathf.PI * 3f) * (1f - t) *
                               Mathf.Max(0f, introElasticityStrength) * 0.55f;
                targetTransform.localScale = new Vector3(
                    authoredScale.x * (1f + wobble),
                    authoredScale.y * (1f - wobble),
                    authoredScale.z);
                yield return null;
            }

            if (targetTransform != null)
            {
                targetTransform.localPosition = target;
                targetTransform.localScale = authoredScale;
            }
        }

        private int GetClusterBottomRow(RuntimeCluster cluster)
        {
            if (cluster == null || cluster.Data == null) return 0;
            int bottom = 0;
            int endColumn = Mathf.Min(_columns.Count,
                cluster.Data.anchorColumn + cluster.Data.width);
            for (int col = Mathf.Max(0, cluster.Data.anchorColumn); col < endColumn; col++)
            {
                int support = GetRemainingTilesBelow(cluster, col);
                for (int i = 0; i < _clusters.Count; i++)
                {
                    RuntimeCluster lower = _clusters[i];
                    if (lower == null || lower == cluster || lower.Remaining <= 0 ||
                        lower.Data == null || lower.Data.anchorRow >= cluster.Data.anchorRow)
                        continue;
                    BoardClusterData lowerData = lower.Data;
                    if (col >= lowerData.anchorColumn &&
                        col < lowerData.anchorColumn + lowerData.width)
                        support += Mathf.Max(1, lowerData.height);
                }
                bottom = Mathf.Max(bottom, support);
            }
            return Mathf.Max(0, bottom);
        }

        private static int[] CountPlayableTilesBelow(CookingBlastLevel level,
            BoardClusterData cluster)
        {
            int[] result = new int[Mathf.Max(0, cluster.width)];
            for (int offset = 0; offset < result.Length; offset++)
            {
                int column = cluster.anchorColumn + offset;
                if (column < 0 || column >= level.BoardColumns.Count) continue;
                BoardColumnData data = level.BoardColumns[column];
                if (data == null || data.bottomToTop == null) continue;
                int endRow = Mathf.Min(cluster.anchorRow, data.bottomToTop.Count);
                for (int row = 0; row < endRow; row++)
                    if (data.bottomToTop[row] != FoodColor.Neutral &&
                        !level.IsClusterCell(column, row, out _))
                        result[offset]++;
            }
            return result;
        }

        private static int GetTilesBelow(RuntimeCluster cluster, int column)
        {
            if (cluster == null || cluster.Data == null ||
                cluster.TilesBelowByColumn == null)
                return 0;
            int offset = column - cluster.Data.anchorColumn;
            return offset >= 0 && offset < cluster.TilesBelowByColumn.Length
                ? cluster.TilesBelowByColumn[offset]
                : 0;
        }

        private static int GetRemainingTilesBelow(RuntimeCluster cluster, int column)
        {
            int original = GetTilesBelow(cluster, column);
            if (cluster == null || cluster.Data == null ||
                cluster.RemovedTilesBelowByColumn == null) return original;
            int offset = column - cluster.Data.anchorColumn;
            int removed = offset >= 0 && offset < cluster.RemovedTilesBelowByColumn.Length
                ? cluster.RemovedTilesBelowByColumn[offset] : 0;
            return Mathf.Max(0, original - removed);
        }

        private void RegisterRegularTileRemoved(int column, int compactRow)
        {
            for (int i = 0; i < _clusters.Count; i++)
            {
                RuntimeCluster cluster = _clusters[i];
                if (cluster == null || cluster.Remaining <= 0 || cluster.Data == null ||
                    cluster.RemovedTilesBelowByColumn == null) continue;
                int offset = column - cluster.Data.anchorColumn;
                if (offset < 0 || offset >= cluster.RemovedTilesBelowByColumn.Length) continue;
                if (compactRow < GetRemainingTilesBelow(cluster, column))
                    cluster.RemovedTilesBelowByColumn[offset]++;
            }
        }

        private bool IsClusterExposed(RuntimeCluster cluster)
        {
            return cluster != null && cluster.Remaining > 0 &&
                   GetClusterBottomRow(cluster) == 0;
        }

        private bool IsColumnBlockedByCluster(int column)
        {
            if (column < 0 || column >= _columns.Count) return false;
            for (int i = 0; i < _clusters.Count; i++)
            {
                RuntimeCluster cluster = _clusters[i];
                if (cluster == null || cluster.Remaining <= 0 || cluster.Data == null) continue;
                BoardClusterData data = cluster.Data;
                if (column < data.anchorColumn || column >= data.anchorColumn + data.width) continue;
                if (GetRemainingTilesBelow(cluster, column) <= 0) return true;
            }
            return false;
        }

        private int GetTileDisplayRow(int column, int compactRow)
        {
            if (column < 0 || column >= _columns.Count) return compactRow;
            int displayRow = compactRow;
            for (int i = 0; i < _clusters.Count; i++)
            {
                RuntimeCluster cluster = _clusters[i];
                if (cluster == null || cluster.Remaining <= 0 || cluster.Data == null) continue;
                BoardClusterData data = cluster.Data;
                if (column < data.anchorColumn || column >= data.anchorColumn + data.width)
                    continue;
                int belowCount = GetRemainingTilesBelow(cluster, column);
                if (compactRow >= belowCount)
                {
                    // A footprint spanning several columns rests on the tallest
                    // support among them. Shorter columns therefore need an
                    // empty gap below the footprint as well as its own height.
                    // Using only `+ height` lets their upper 1x1 Tiles occupy
                    // the same display rows as a 2x1/2x2 footprint.
                    int clusterBottom = GetClusterBottomRow(cluster);
                    int rowAboveFootprint = clusterBottom +
                        Mathf.Max(1, data.height) +
                        (compactRow - belowCount);
                    displayRow = Mathf.Max(displayRow, rowAboveFootprint);
                }
            }
            return displayRow;
        }

        private void LayoutClusters(
            bool animate = false,
            List<Coroutine> activeFalls = null)
        {
            if (tileRoot == null || _columns.Count == 0) return;
            float tileWidth = _layoutCellSize.x > 0.0001f
                ? _layoutCellSize.x : Mathf.Max(0.01f, cellSize);
            float tileHeight = _layoutCellSize.y > 0.0001f
                ? _layoutCellSize.y : Mathf.Max(0.01f, cellSize);
            float safeGapX = Mathf.Max(-tileWidth + 0.01f, gapX);
            float safeGapY = Mathf.Max(-tileHeight + 0.01f, gapY);
            float stepX = tileWidth + safeGapX;
            float stepY = tileHeight + safeGapY;
            float totalWidth = _columns.Count * tileWidth +
                               (_columns.Count - 1) * safeGapX;
            float startX = -totalWidth * .5f + tileWidth * .5f;
            for (int i = 0; i < _clusters.Count; i++)
            {
                RuntimeCluster cluster = _clusters[i];
                if (cluster.Remaining <= 0 || cluster.View == null) continue;
                BoardClusterData data = cluster.Data;
                Vector2 centerOffset = cluster.View.GetVisualCenterOffset(tileRoot);
                int displayRow = GetClusterBottomRow(cluster);
                Vector3 target = new Vector3(
                    startX + (data.anchorColumn + (data.width - 1) * .5f) * stepX -
                    centerOffset.x,
                    (displayRow + (data.height - 1) * .5f) * stepY -
                    centerOffset.y,
                    0f);
                // Use the same bottom-to-top rule as regular Tiles. A footprint
                // at a higher row must render behind rows below it instead of
                // always being forced to the front.
                cluster.View.SetFrontRowShadow(frontRowShadowSprite,
                    displayRow == 0, frontRowShadowOverlap);
                cluster.View.SetSortingOrder(10 + _maxRows - displayRow);
                if (animate)
                {
                    Transform clusterTransform = cluster.View.transform;
                    if (_clusterMoveRoutines.TryGetValue(
                            clusterTransform, out Coroutine runningMove) &&
                        runningMove != null)
                    {
                        StopCoroutine(runningMove);
                    }

                    Coroutine fall = StartCoroutine(
                        MoveTileRoutine(clusterTransform, target));
                    _clusterMoveRoutines[clusterTransform] = fall;
                    if (activeFalls != null) activeFalls.Add(fall);
                }
                else
                {
                    cluster.View.transform.localPosition = target;
                }
            }
        }

        private void MeasureLayoutCellSize()
        {
            Vector2 measured = Vector2.zero;
            for (int col = 0; col < _columns.Count; col++)
            {
                List<FoodTileView> column = _columns[col];
                for (int row = 0; row < column.Count; row++)
                {
                    FoodTileView tile = column[row];
                    if (tile == null) continue;
                    Vector2 size = tile.GetVisualSize(tileRoot);
                    measured.x = Mathf.Max(measured.x, size.x);
                    measured.y = Mathf.Max(measured.y, size.y);
                }
            }

            float fallback = Mathf.Max(0.01f, cellSize);
            _layoutCellSize = new Vector2(
                measured.x > 0.0001f ? measured.x : fallback,
                measured.y > 0.0001f ? measured.y : fallback);
        }

private void LayoutColumn(int col, bool animate, List<Coroutine> activeFalls = null)
        {
            if (col < 0 || col >= _columns.Count || tileRoot == null) return;
            int columnCount = Mathf.Max(1, _columns.Count);
            float tileWidth = _layoutCellSize.x > 0.0001f ? _layoutCellSize.x : Mathf.Max(0.01f, cellSize);
            float tileHeight = _layoutCellSize.y > 0.0001f ? _layoutCellSize.y : Mathf.Max(0.01f, cellSize);
            float safeGapX = Mathf.Max(-tileWidth + 0.01f, gapX);
            float safeGapY = Mathf.Max(-tileHeight + 0.01f, gapY);
            float stepX = tileWidth + safeGapX;
            float stepY = tileHeight + safeGapY;
            float totalWidth = columnCount * tileWidth + (columnCount - 1) * safeGapX;
            float startX = -totalWidth * 0.5f + tileWidth * 0.5f;
            // Tiles is the origin of the first (bottom) row.
            float bottomY = 0f;

            List<FoodTileView> column = _columns[col];
            for (int row = 0; row < column.Count; row++)
            {
                FoodTileView tile = column[row];
                if (tile == null) continue;
                int displayRow = GetTileDisplayRow(col, row);
                Vector2 centerOffset = tile.GetVisualCenterOffset(tileRoot);
                Vector3 target = new Vector3(
                    startX + col * stepX - centerOffset.x,
                    bottomY + displayRow * stepY - centerOffset.y,
                    0f);
                // Bottom tiles render in front of the rows above them.
                tile.SetFrontRowShadow(frontRowShadowSprite,
                    displayRow == 0, frontRowShadowOverlap);
                tile.SetSortingOrder(10 + _maxRows - displayRow);
                if (animate)
                {
                    // Start every tile's fall together; the caller may wait for all of them.
                    Coroutine fall = StartCoroutine(MoveTileRoutine(tile.transform, target));
                    if (activeFalls != null) activeFalls.Add(fall);
                }
                else tile.transform.localPosition = target;
            }
        }

        private IEnumerator MoveTileRoutine(Transform targetTransform, Vector3 target)
        {
            if (targetTransform == null) yield break;
            Vector3 start = targetTransform.localPosition;
            Vector3 authoredScale = targetTransform.localScale;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, dropDuration);
            while (elapsed < duration && targetTransform != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                targetTransform.localPosition = Vector3.LerpUnclamped(start, target, eased);

                float stretch = Mathf.Sin(t * Mathf.PI) *
                                Mathf.Max(0f, dropElasticityStrength) * 0.35f;
                targetTransform.localScale = new Vector3(
                    authoredScale.x * (1f - stretch),
                    authoredScale.y * (1f + stretch),
                    authoredScale.z);
                yield return null;
            }
            if (targetTransform == null) yield break;

            targetTransform.localPosition = target;
            elapsed = 0f;
            float elasticityDuration = Mathf.Max(0f, dropElasticityDuration);
            while (elapsed < elasticityDuration && targetTransform != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, elasticityDuration));
                float wobble = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) *
                               Mathf.Max(0f, dropElasticityStrength);
                targetTransform.localScale = new Vector3(
                    authoredScale.x * (1f + wobble),
                    authoredScale.y * (1f - wobble),
                    authoredScale.z);
                yield return null;
            }

            if (targetTransform != null)
            {
                targetTransform.localPosition = target;
                targetTransform.localScale = authoredScale;
            }
        }
    

private CustomerView FindFreeCustomer(SeatBar seats, FoodColor color)
        {
            seats.GetCustomersForType(color, _customerBuffer);
            if (_customerBuffer.Count == 0) return null;

            if (tileSelectionMode == TileSelectionMode.RowByRowLeftToRight)
            {
                // The reference game finishes the earliest seated Animal of a
                // given type before serving the next Animal of that same type.
                for (int i = 0; i < _customerBuffer.Count; i++)
                {
                    CustomerView customer = _customerBuffer[i];
                    if (!CanScheduleCustomer(seats, customer)) continue;
                    return customer;
                }
                return null;
            }

            _typeRoundRobinCursor.TryGetValue(color, out int cursor);
            cursor = Mathf.Abs(cursor) % _customerBuffer.Count;
            CustomerView best = null;
            int bestScore = int.MaxValue;
            int bestIndex = -1;
            for (int offset = 0; offset < _customerBuffer.Count; offset++)
            {
                int i = (cursor + offset) % _customerBuffer.Count;
                CustomerView customer = _customerBuffer[i];
                if (!CanScheduleCustomer(seats, customer)) continue;

                _customerFlights.TryGetValue(customer, out int inFlight);
                _customerDeliveredCount.TryGetValue(customer, out int delivered);
                int score = delivered + inFlight;
                if (score >= bestScore) continue;
                best = customer;
                bestScore = score;
                bestIndex = i;
            }
            if (bestIndex >= 0)
                _typeRoundRobinCursor[color] = (bestIndex + 1) % _customerBuffer.Count;
            return best;
        }

        private bool CanScheduleCustomer(SeatBar seats, CustomerView customer)
        {
            if (customer == null || customer.Remaining <= 0 ||
                !seats.CanReceiveTiles(customer))
                return false;

            _customerFlights.TryGetValue(customer, out int inFlight);
            if (inFlight >= customer.Remaining) return false;

            return !_customerNextScanAt.TryGetValue(customer, out float nextScan) ||
                   Time.unscaledTime >= nextScan;
        }

private IEnumerator ServeSingleTile(int col, FoodTileView tile, CustomerView customer, SeatBar seats)
        {
            bool flightDone = false;
            FoodColor servedColor = tile != null ? tile.Type : default;

            // Falling and flying are independent; release the column after its fall.
            var fallingTiles = new List<Coroutine>();
            LayoutColumn(col, true, fallingTiles);
            // Footprints and 1x1 Tiles must move in the same animation pass.
            // Teleporting a 2x1/2x2 footprint while its column is falling makes
            // the regular Tiles appear to pass through it.
            LayoutClusters(true, fallingTiles);

            if (tile != null && customer != null)
                tile.ConsumeTo(customer.transform, () => flightDone = true);
            else
                flightDone = true;

            for (int i = 0; i < fallingTiles.Count; i++)
                if (fallingTiles[i] != null) yield return fallingTiles[i];

            if (serveInterval > 0f)
                yield return new WaitForSecondsRealtime(serveInterval);

            if (col >= 0 && col < _columnBusy.Count)
                _columnBusy[col] = false;

            while (!flightDone) yield return null;

            bool completed = customer != null && customer.ServeOne();
            if (_game != null) _game.OnTileServed(servedColor);
            if (tile != null) Destroy(tile.gameObject);

            if (customer != null)
            {
                if (_customerFlights.TryGetValue(customer, out int inFlight))
                {
                    if (inFlight <= 1)
                        _customerFlights.Remove(customer);
                    else
                        _customerFlights[customer] = inFlight - 1;
                }

                // Combo increases only when a tile actually reaches this customer.
                // Move the already scheduled launch earlier as the interval shrinks.
                int delivered = 0;
                _customerDeliveredCount.TryGetValue(customer, out delivered);
                float oldInterval = GetCustomerScanInterval(customer);
                _customerDeliveredCount[customer] = delivered + 1;
                float newInterval = GetCustomerScanInterval(customer);
                if (_customerNextScanAt.TryGetValue(customer, out float nextScan))
                    _customerNextScanAt[customer] = Mathf.Min(nextScan, nextScan - oldInterval + newInterval);

                if (completed)
                {
                    _customerDeliveredCount.Remove(customer);
                    _customerNextScanAt.Remove(customer);
                    seats.CompleteCustomer(customer);
                }
            }
        }

private IEnumerator ServeClusterUnit(RuntimeCluster cluster, CustomerView customer, SeatBar seats)
        {
            bool flightDone = false;
            FoodColor servedColor = cluster.Data.color;
            FoodTileView projectile = null;
            FoodTileView prefab = GetTilePrefab(servedColor);
            if (prefab != null && tileRoot != null)
            {
                projectile = Instantiate(prefab, tileRoot);
                projectile.Setup(servedColor);
                if (cluster.View != null)
                    projectile.transform.position = cluster.View.transform.position;
                projectile.SetSortingOrder(100);
                projectile.ConsumeTo(customer != null ? customer.transform : null,
                    () => flightDone = true);
            }
            else flightDone = true;

            while (!flightDone) yield return null;

            bool completed = customer != null && customer.ServeOne();
            if (_game != null) _game.OnTileServed(servedColor);
            if (projectile != null) Destroy(projectile.gameObject);

            if (customer != null)
            {
                if (_customerFlights.TryGetValue(customer, out int inFlight))
                {
                    if (inFlight <= 1) _customerFlights.Remove(customer);
                    else _customerFlights[customer] = inFlight - 1;
                }

                int delivered = 0;
                _customerDeliveredCount.TryGetValue(customer, out delivered);
                float oldInterval = GetCustomerScanInterval(customer);
                _customerDeliveredCount[customer] = delivered + 1;
                float newInterval = GetCustomerScanInterval(customer);
                if (_customerNextScanAt.TryGetValue(customer, out float nextScan))
                    _customerNextScanAt[customer] = Mathf.Min(nextScan,
                        nextScan - oldInterval + newInterval);

                if (completed)
                {
                    _customerDeliveredCount.Remove(customer);
                    _customerNextScanAt.Remove(customer);
                    seats.CompleteCustomer(customer);
                }
            }

            cluster.InFlight = Mathf.Max(0, cluster.InFlight - 1);
            if (cluster.Remaining <= 0 && !cluster.Cleared)
            {
                cluster.Cleared = true;
                if (cluster.View != null) Destroy(cluster.View.gameObject);
                for (int col = cluster.Data.anchorColumn;
                     col < cluster.Data.anchorColumn + cluster.Data.width && col < _columns.Count; col++)
                {
                    StartCoroutine(WaitForColumnFall(col));
                }
            }
        }





public bool HasTileInFlight(CustomerView customer)
        {
            return customer != null && _customerFlights.TryGetValue(customer, out int flights) && flights > 0;
        }

        public int GetTileInFlightCount(CustomerView customer)
        {
            return customer != null && _customerFlights.TryGetValue(customer, out int flights)
                ? flights : 0;
        }


private IEnumerator WaitForColumnFall(int col)
        {
            var fallingTiles = new List<Coroutine>();
            LayoutColumn(col, true, fallingTiles);
            LayoutClusters(true, fallingTiles);
            for (int i = 0; i < fallingTiles.Count; i++)
                if (fallingTiles[i] != null) yield return fallingTiles[i];
            if (col >= 0 && col < _columnBusy.Count)
                _columnBusy[col] = false;
        }


private float GetCustomerScanInterval(CustomerView customer)
        {
            _customerDeliveredCount.TryGetValue(customer, out int delivered);
            float start = Mathf.Max(0f, scanInterval);
            float minimum = Mathf.Clamp(minimumScanInterval, 0f, start);
            return Mathf.Max(minimum, start - delivered * Mathf.Max(0f, scanAccelerationPerTile));
        }
}
}
