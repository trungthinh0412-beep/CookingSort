using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CookingSort2D
{
    public class BoardController : MonoBehaviour
    {
        [SerializeField] private FoodTileView tilePrefab;
        [SerializeField] private Transform tileRoot;
        [SerializeField] private float cellSize = 0.48f;
        [SerializeField] private float gap = 0.04f;
[SerializeField] private float serveInterval = 0.025f;
        [SerializeField, Min(0.01f)] private float dropDuration = 0.13f;

        
        private readonly List<CustomerView> _customerBuffer = new List<CustomerView>();
        private readonly List<List<FoodTileView>> _columns = new List<List<FoodTileView>>();
        private readonly List<bool> _columnBusy = new List<bool>();
        private readonly Dictionary<CustomerView, int> _customerFlights = new Dictionary<CustomerView, int>();
        private CookingSortGameController _game;
        private bool _serving;
        private int _maxRows;

        public bool IsServing => _serving;
        public int RemainingTileCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _columns.Count; i++) total += _columns[i].Count;
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

public void Initialize(CookingSortGameController game, CookingBlastLevel data)
        {
            _game = game;
            if (tileRoot == null)
            {
                Transform t = transform.Find("Tiles");
                if (t != null) tileRoot = t;
            }
            ClearBoard();
            if (data == null || tilePrefab == null || tileRoot == null) return;

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
                    FoodTileView tile = Instantiate(tilePrefab, tileRoot);
                    tile.name = $"Tile_C{col}_R{row}_{columnData.bottomToTop[row]}";
                    tile.Setup(columnData.bottomToTop[row]);
                    column.Add(tile);
                }
            }
            LayoutAll(false);
        }

        public void BeginServeLoop(SeatBar seats)
        {
            if (_serving || seats == null || !isActiveAndEnabled) return;
            StartCoroutine(ServeRoutine(seats));
        }

        public bool HasServiceableTile(SeatBar seats)
        {
            if (seats == null) return false;
            for (int col = 0; col < _columns.Count; col++)
            {
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
            _columnBusy.Clear();
            _customerFlights.Clear();
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

                for (int col = 0; col < _columns.Count; col++)
                {
                    if (col >= _columnBusy.Count) _columnBusy.Add(false);
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
                        removedMissingTile = true;
                    }
                    if (removedMissingTile)
                    {
                        _columnBusy[col] = true;
                        anyColumnBusy = true;
                        StartCoroutine(WaitForColumnFall(col));
                        continue;
                    }
                    if (column.Count == 0) continue;

                    FoodTileView tile = column[0];
                    CustomerView customer = FindFreeCustomer(seats, tile.ColorType);
                    if (customer == null) continue;

                    // One flight at a time per seated customer/color chain.
                    // Blue and Pink chains can run simultaneously, but Blue tiles themselves still arrive one-by-one.
                    _customerFlights[customer] = 1;
                    column.RemoveAt(0);
                    _columnBusy[col] = true;
                    anyColumnBusy = true;
                    startedAny = true;
                    StartCoroutine(ServeSingleTile(col, tile, customer, seats));
                }

                if (!anyColumnBusy && !startedAny)
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

private void LayoutColumn(int col, bool animate, List<Coroutine> activeFalls = null)
        {
            if (col < 0 || col >= _columns.Count || tileRoot == null) return;
            int columnCount = Mathf.Max(1, _columns.Count);
            float step = cellSize + gap;
            float totalWidth = columnCount * cellSize + (columnCount - 1) * gap;
            float startX = -totalWidth * 0.5f + cellSize * 0.5f;
            float totalHeight = Mathf.Max(1, _maxRows) * cellSize + Mathf.Max(0, _maxRows - 1) * gap;
            float bottomY = -totalHeight * 0.5f + cellSize * 0.5f;

            List<FoodTileView> column = _columns[col];
            for (int row = 0; row < column.Count; row++)
            {
                FoodTileView tile = column[row];
                if (tile == null) continue;
                Vector3 target = new Vector3(startX + col * step, bottomY + row * step, 0f);
                tile.SetSortingOrder(10 + row);
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
                if (customer == null || customer.Remaining <= 0 || seats.IsMergingCustomer(customer)) continue;
                if (_customerFlights.TryGetValue(customer, out int flights) && flights > 0) continue;
                return customer;
            }
            return null;
        }

private IEnumerator ServeSingleTile(int col, FoodTileView tile, CustomerView customer, SeatBar seats)
        {
            bool flightDone = false;
            FoodColor servedColor = tile != null ? tile.ColorType : default;

            // The column starts falling as soon as its bottom tile leaves.
            // Other columns/colors remain independent.
            var fallingTiles = new List<Coroutine>();
            LayoutColumn(col, true, fallingTiles);

            // Flight duration stays constant; seat occupancy never accelerates it.
            if (tile != null && customer != null)
                tile.ConsumeTo(customer.transform, () => flightDone = true);
            else
                flightDone = true;

            while (!flightDone) yield return null;

            // Never dispatch the newly exposed tile while it is still falling.
            for (int i = 0; i < fallingTiles.Count; i++)
                if (fallingTiles[i] != null) yield return fallingTiles[i];

            bool completed = customer != null && customer.ServeOne();
            if (_game != null) _game.OnTileServed(servedColor);
            if (tile != null) Destroy(tile.gameObject);

            if (customer != null)
                _customerFlights.Remove(customer);

            if (completed && customer != null)
                seats.CompleteCustomer(customer);

            if (serveInterval > 0f)
                yield return new WaitForSecondsRealtime(serveInterval);

            if (col >= 0 && col < _columnBusy.Count)
                _columnBusy[col] = false;
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
}
}
