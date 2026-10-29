using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CookingSort2D
{
    public class BoardController : MonoBehaviour
    {
        
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

        [Header("Tile flow")]
        [Tooltip("Extra pause after a column finishes sliding down. Keep at 0 for an immediate drop-to-flight handoff.")]
        [SerializeField, Min(0f)] private float serveInterval = 0f;
        [SerializeField, Min(0.01f)] private float dropDuration = 0.13f;
        [Header("Tile scanning")]
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
        private readonly List<ServeCandidate> _serveCandidates = new List<ServeCandidate>();
        private readonly List<RuntimeCluster> _clusters = new List<RuntimeCluster>();
        private readonly Dictionary<Transform, Coroutine> _clusterMoveRoutines =
            new Dictionary<Transform, Coroutine>();
        private readonly List<int> _columnFrontRows = new List<int>();
private readonly List<bool> _columnBusy = new List<bool>();
        private readonly Dictionary<CustomerView, int> _customerFlights = new Dictionary<CustomerView, int>();
        private readonly Dictionary<CustomerView, int> _customerDeliveredCount = new Dictionary<CustomerView, int>();
        private readonly Dictionary<CustomerView, float> _customerNextScanAt = new Dictionary<CustomerView, float>();
        private readonly Dictionary<FoodColor, int> _typeRoundRobinCursor = new Dictionary<FoodColor, int>();
        private CookingSortGameController _game;
        private bool _serving;
        private int _maxRows;
        private Vector2 _layoutCellSize;

        public bool IsServing => _serving;
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
                
                _columnFrontRows.Add(0);
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
                        TilesBelowByColumn = CountPlayableTilesBelow(data, cluster)
                    });
                }
            }
            MeasureLayoutCellSize();
            LayoutAll(false);
            LayoutClusters();
        }

        public void BeginServeLoop(SeatBar seats)
        {
            if (_serving || seats == null || !isActiveAndEnabled) return;
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
            StopAllCoroutines();
            _columns.Clear();
            _clusters.Clear();
            _clusterMoveRoutines.Clear();
            _columnFrontRows.Clear();
_columnBusy.Clear();
            _customerFlights.Clear();
            _customerDeliveredCount.Clear();
            _customerNextScanAt.Clear();
            _typeRoundRobinCursor.Clear();
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
                        column.RemoveAt(0);
                        _columnFrontRows[col]++;
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

                // Tile and Cluster sources share one scheduler. Pick an Animal
                // fairly, then prefer farther sources with a 2:1 max/min weight.
                while (_serveCandidates.Count > 0)
                {
                    CustomerView customer = null;
                    FoodColor selectedType = default;
                    for (int i = 0; i < _serveCandidates.Count; i++)
                    {
                        selectedType = _serveCandidates[i].Type;
                        customer = FindFreeCustomer(seats, selectedType);
                        if (customer != null) break;
                    }
                    if (customer == null) break;

                    int chosenIndex = ChooseWeightedSourceIndex(selectedType, customer);
                    if (chosenIndex < 0) break;
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
                        StartCoroutine(ServeClusterUnit(chosen.Cluster, customer, seats));
                    }
                    else
                    {
                        List<FoodTileView> column = _columns[chosen.Column];
                        if (column.Count == 0 || column[0] != chosen.Tile) continue;
                        column.RemoveAt(0);
                        _columnFrontRows[chosen.Column]++;
                        _columnBusy[chosen.Column] = true;
                        anyColumnBusy = true;
                        StartCoroutine(ServeSingleTile(chosen.Column, chosen.Tile, customer, seats));
                    }
                    startedAny = true;
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

        private int GetClusterBottomRow(RuntimeCluster cluster)
        {
            if (cluster == null || cluster.Data == null) return 0;
            int bottom = 0;
            int endColumn = Mathf.Min(_columnFrontRows.Count,
                cluster.Data.anchorColumn + cluster.Data.width);
            for (int col = Mathf.Max(0, cluster.Data.anchorColumn); col < endColumn; col++)
            {
                int support = GetTilesBelow(cluster, col) - _columnFrontRows[col];
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

        private bool IsClusterExposed(RuntimeCluster cluster)
        {
            return cluster != null && cluster.Remaining > 0 &&
                   GetClusterBottomRow(cluster) == 0;
        }

        private bool IsColumnBlockedByCluster(int column)
        {
            if (column < 0 || column >= _columnFrontRows.Count) return false;
            for (int i = 0; i < _clusters.Count; i++)
            {
                RuntimeCluster cluster = _clusters[i];
                if (cluster == null || cluster.Remaining <= 0 || cluster.Data == null) continue;
                BoardClusterData data = cluster.Data;
                if (column < data.anchorColumn || column >= data.anchorColumn + data.width) continue;
                if (_columnFrontRows[column] >= GetTilesBelow(cluster, column)) return true;
            }
            return false;
        }

        private int GetTileDisplayRow(int column, int compactRow)
        {
            if (column < 0 || column >= _columnFrontRows.Count) return compactRow;
            int frontRow = _columnFrontRows[column];
            int displayRow = compactRow;
            for (int i = 0; i < _clusters.Count; i++)
            {
                RuntimeCluster cluster = _clusters[i];
                if (cluster == null || cluster.Remaining <= 0 || cluster.Data == null) continue;
                BoardClusterData data = cluster.Data;
                if (column < data.anchorColumn || column >= data.anchorColumn + data.width)
                    continue;
                int belowCount = Mathf.Max(0, GetTilesBelow(cluster, column) - frontRow);
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
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, dropDuration);
            while (elapsed < duration && targetTransform != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                targetTransform.localPosition = Vector3.LerpUnclamped(start, target, t);
                yield return null;
            }
            if (targetTransform != null) targetTransform.localPosition = target;
        }
    

private CustomerView FindFreeCustomer(SeatBar seats, FoodColor color)
        {
            seats.GetCustomersForType(color, _customerBuffer);
            if (_customerBuffer.Count == 0) return null;

            _typeRoundRobinCursor.TryGetValue(color, out int cursor);
            cursor = Mathf.Abs(cursor) % _customerBuffer.Count;
            CustomerView best = null;
            int bestScore = int.MaxValue;
            int bestIndex = -1;
            for (int offset = 0; offset < _customerBuffer.Count; offset++)
            {
                int i = (cursor + offset) % _customerBuffer.Count;
                CustomerView customer = _customerBuffer[i];
                if (customer == null || customer.Remaining <= 0 || !seats.CanReceiveTiles(customer))
                    continue;

                _customerFlights.TryGetValue(customer, out int inFlight);
                if (inFlight >= customer.Remaining) continue;

                if (_customerNextScanAt.TryGetValue(customer, out float nextScan) &&
                    Time.unscaledTime < nextScan)
                    continue;

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
