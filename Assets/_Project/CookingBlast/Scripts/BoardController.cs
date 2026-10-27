using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CookingSort2D
{
    public class BoardController : MonoBehaviour
    {
        
        [Header("Prefab for each FoodColor (matches Customer logic)")]
        [Tooltip("Index = FoodColor enum value. Empty entries use TileBase fallback.")]
        [SerializeField] private FoodTileView[] tilePrefabsByColor = new FoodTileView[10];
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

        
        private readonly List<CustomerView> _customerBuffer = new List<CustomerView>();
        private readonly List<List<FoodTileView>> _columns = new List<List<FoodTileView>>();
        private sealed class RuntimeCluster
        {
            public BoardClusterData Data;
            public FoodTileView View;
            public int Remaining;
            public bool Busy;
            public int[] TilesBelowByColumn;
        }
        private readonly List<RuntimeCluster> _clusters = new List<RuntimeCluster>();
        private readonly List<int> _columnFrontRows = new List<int>();
private readonly List<bool> _columnBusy = new List<bool>();
        private readonly Dictionary<CustomerView, int> _customerFlights = new Dictionary<CustomerView, int>();
        private readonly Dictionary<CustomerView, int> _customerDeliveredCount = new Dictionary<CustomerView, int>();
        private readonly Dictionary<CustomerView, float> _customerNextScanAt = new Dictionary<CustomerView, float>();
        private CookingSortGameController _game;
        private bool _serving;
        private int _maxRows;
        private Vector2 _layoutCellSize;

        public bool IsServing => _serving;
        public int RemainingTileCount
        {
            get
            {
                int total = 0;
                for (int col = 0; col < _columns.Count; col++)
                    for (int row = 0; row < _columns[col].Count; row++)
                        if (_columns[col][row] != null &&
                            _columns[col][row].ColorType != FoodColor.Neutral)
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
            int index = (int)color;
            if (tilePrefabsByColor != null && index >= 0 &&
                index < tilePrefabsByColor.Length && tilePrefabsByColor[index] != null)
                return tilePrefabsByColor[index];
            // Backwards compatibility for colors without custom sprites.
            return tilePrefab;
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
                    FoodTileView prefab = GetTilePrefab(cluster.color);
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
                if (bottom != null && seats.HasCustomerForColor(bottom.ColorType)) return true;
            }
            return false;
        }

public void ClearBoard()
        {
            _serving = false;
            StopAllCoroutines();
            _columns.Clear();
            _clusters.Clear();
            _columnFrontRows.Clear();
_columnBusy.Clear();
            _customerFlights.Clear();
            _customerDeliveredCount.Clear();
            _customerNextScanAt.Clear();
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

                for (int i = 0; i < _clusters.Count; i++)
                {
                    RuntimeCluster cluster = _clusters[i];
                    if (cluster.Remaining <= 0 || !IsClusterExposed(cluster)) continue;
                    if (cluster.Busy)
                    {
                        anyColumnBusy = true;
                        continue;
                    }
                    CustomerView customer = FindFreeCustomer(seats, cluster.Data.color);
                    if (customer == null) continue;
                    _customerFlights.TryGetValue(customer, out int clusterFlights);
                    _customerFlights[customer] = clusterFlights + 1;
                    _customerNextScanAt[customer] = Time.unscaledTime + GetCustomerScanInterval(customer);
                    cluster.Busy = true;
                    cluster.Remaining--;
                    if (cluster.View != null) cluster.View.SetClusterValue(cluster.Remaining);
                    anyColumnBusy = true;
                    startedAny = true;
                    StartCoroutine(ServeClusterUnit(cluster, customer, seats));
                }

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

                // Visit visible tiles in bottom-row / left-to-right order.
                // Every seated customer has its own launch clock and combo speed.
                for (int row = 0; row < _maxRows; row++)
                {
                    for (int col = 0; col < _columns.Count; col++)
                    {
                        if (_columnBusy[col] || _columnFrontRows[col] != row) continue;

                        List<FoodTileView> column = _columns[col];
                        if (column.Count == 0) continue;

                        FoodTileView tile = column[0];
                        CustomerView customer = FindFreeCustomer(seats, tile.ColorType);
                        if (customer == null) continue;

                        // Reserve demand before the tile flies, so overlapping
                        // flights can never overfill a customer.
                        _customerFlights.TryGetValue(customer, out int inFlight);
                        _customerFlights[customer] = inFlight + 1;
                        _customerNextScanAt[customer] = Time.unscaledTime + GetCustomerScanInterval(customer);
                        column.RemoveAt(0);
                        _columnFrontRows[col]++;
                        _columnBusy[col] = true;
                        anyColumnBusy = true;
                        startedAny = true;
                        StartCoroutine(ServeSingleTile(col, tile, customer, seats));
                    }
                }

                // Keep scanning while any customer is cooling down or while
                // outstanding flights are still moving towards seats.
                if (!anyColumnBusy && !startedAny && _customerFlights.Count == 0 &&
                    !HasServiceableTile(seats))
                    break;

                yield return null;
            }

            _serving = false;
            if (_game != null) _game.OnServeLoopFinished();
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
                bottom = Mathf.Max(bottom,
                    GetTilesBelow(cluster, col) - _columnFrontRows[col]);
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
            RuntimeCluster nearest = null;
            for (int i = 0; i < _clusters.Count; i++)
            {
                RuntimeCluster cluster = _clusters[i];
                if (cluster == null || cluster.Remaining <= 0 || cluster.Data == null) continue;
                BoardClusterData data = cluster.Data;
                if (column < data.anchorColumn || column >= data.anchorColumn + data.width ||
                    GetTilesBelow(cluster, column) < frontRow)
                    continue;
                if (nearest == null || GetTilesBelow(cluster, column) <
                    GetTilesBelow(nearest, column))
                    nearest = cluster;
            }

            if (nearest == null) return compactRow;
            int belowCount = Mathf.Max(0, GetTilesBelow(nearest, column) - frontRow);
            if (compactRow < belowCount) return compactRow;
            return GetClusterBottomRow(nearest) + nearest.Data.height + compactRow - belowCount;
        }

        private void LayoutClusters()
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
                if (cluster.View == null) continue;
                BoardClusterData data = cluster.Data;
                Vector2 centerOffset = cluster.View.GetVisualCenterOffset(tileRoot);
                cluster.View.transform.localPosition = new Vector3(
                    startX + (data.anchorColumn + (data.width - 1) * .5f) * stepX -
                    centerOffset.x,
                    (GetClusterBottomRow(cluster) + (data.height - 1) * .5f) * stepY -
                    centerOffset.y,
                    0f);
                cluster.View.SetSortingOrder(20 + _maxRows);
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
                tile.SetSortingOrder(10 + _maxRows - row);
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
            seats.GetCustomersForColor(color, _customerBuffer);
            for (int i = 0; i < _customerBuffer.Count; i++)
            {
                CustomerView customer = _customerBuffer[i];
                if (customer == null || customer.Remaining <= 0 || seats.IsMergingCustomer(customer))
                    continue;

                _customerFlights.TryGetValue(customer, out int inFlight);
                if (inFlight >= customer.Remaining) continue;

                if (_customerNextScanAt.TryGetValue(customer, out float nextScan) &&
                    Time.unscaledTime < nextScan)
                    continue;

                return customer;
            }
            return null;
        }

private IEnumerator ServeSingleTile(int col, FoodTileView tile, CustomerView customer, SeatBar seats)
        {
            bool flightDone = false;
            FoodColor servedColor = tile != null ? tile.ColorType : default;

            // Falling and flying are independent; release the column after its fall.
            var fallingTiles = new List<Coroutine>();
            LayoutColumn(col, true, fallingTiles);
            LayoutClusters();

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

            cluster.Busy = false;
            if (cluster.Remaining <= 0)
            {
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
