using System.Collections.Generic;
using UnityEngine;

namespace CookingSort2D
{
    [DisallowMultipleComponent]
    public sealed class CookingBlastLevel : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField, Min(1)] private int levelNumber = 1;

        [Header("Rules")]
        [SerializeField, Range(1, 5)] private int seatCount = 5;

        [Header("Prefab scene pieces")]
        [SerializeField] private BoardController board;
        [SerializeField] private SeatBar seatBar;
        [SerializeField] private CustomerQueue customerQueue;

        
[Header("Board - each column is stored bottom to top")]
        [SerializeField] private List<BoardColumnData> boardColumns = new List<BoardColumnData>();

        [Header("Board footprint Tiles - replace covered cells with one valued piece")]
        [SerializeField] private List<BoardClusterData> boardClusters = new List<BoardClusterData>();

        [Header("Customers - each lane is stored front to back")]
        [SerializeField] private List<CustomerLaneData> customerLanes = new List<CustomerLaneData>();

        public BoardController Board => board;
        public SeatBar SeatBar => seatBar;
        public CustomerQueue CustomerQueue => customerQueue;
        
public int LevelNumber => levelNumber;
        public int SeatCount => seatCount;
        public List<BoardColumnData> BoardColumns => boardColumns;
        public List<BoardClusterData> BoardClusters => boardClusters;
        public List<CustomerLaneData> CustomerLanes => customerLanes;

        public int GetCustomerDemand(FoodColor color)
        {
            CustomerView prefab = customerQueue != null
                ? customerQueue.GetAnimalPrefab(color)
                : null;
            return prefab != null ? prefab.Demand : 20;
        }

        public int TotalTileCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < boardColumns.Count; i++)
                {
                    BoardColumnData column = boardColumns[i];
                    if (column != null && column.bottomToTop != null)
                    {
                        for (int row = 0; row < column.bottomToTop.Count; row++)
                            if (!IsClusterCell(i, row, out _) &&
                                column.bottomToTop[row] != FoodColor.Neutral)
                                total++;
                    }
                }
                if (boardClusters != null)
                    for (int i = 0; i < boardClusters.Count; i++)
                        if (boardClusters[i] != null) total += Mathf.Max(0, boardClusters[i].value);
                return total;
            }
        }

        public bool IsClusterCell(int column, int row, out BoardClusterData cluster)
        {
            if (boardClusters != null)
            {
                for (int i = 0; i < boardClusters.Count; i++)
                {
                    BoardClusterData candidate = boardClusters[i];
                    if (candidate != null && candidate.Contains(column, row))
                    {
                        cluster = candidate;
                        return true;
                    }
                }
            }
            cluster = null;
            return false;
        }

        public bool TryValidate(out string error)
        {
            if (board == null || seatBar == null || customerQueue == null)
            {
                error = "Level prefab must contain Board, SeatBar and CustomerQueue children and reference them on CookingBlastLevel.";
                return false;
            }

            
if (levelNumber < 1)
            {
                error = "Level number must be >= 1.";
                return false;
            }

            if (seatCount < 1 || seatCount > 5)
            {
                error = "Seat count must be between 1 and 5 because the current SeatBar prefab has five slots.";
                return false;
            }

            if (boardColumns == null || boardColumns.Count == 0)
            {
                error = "Add at least one board column.";
                return false;
            }

            var tileCounts = new Dictionary<FoodColor, int>();
            var occupiedClusterCells = new HashSet<Vector2Int>();
            if (boardClusters != null)
            {
                for (int i = 0; i < boardClusters.Count; i++)
                {
                    BoardClusterData cluster = boardClusters[i];
                    if (cluster == null)
                    {
                        error = $"Footprint {i + 1} is missing.";
                        return false;
                    }
                    if (cluster.value < 1 || cluster.width < 1 || cluster.height < 1)
                    {
                        error = $"Footprint {i + 1} must have Width, Height and Value >= 1.";
                        return false;
                    }
                    if (cluster.anchorRow < 0)
                    {
                        error = $"Footprint {i + 1} starts outside the board rows.";
                        return false;
                    }
                    if (cluster.color == FoodColor.Neutral)
                    {
                        error = $"Footprint {i + 1} cannot use Neutral because it requires Customers.";
                        return false;
                    }
                    if (cluster.anchorColumn < 0 || cluster.anchorColumn + cluster.width > boardColumns.Count)
                    {
                        error = $"Footprint {i + 1} is outside the board columns.";
                        return false;
                    }
                    for (int column = cluster.anchorColumn; column < cluster.anchorColumn + cluster.width; column++)
                    {
                        BoardColumnData boardColumn = boardColumns[column];
                        if (boardColumn == null || boardColumn.bottomToTop == null ||
                            cluster.anchorRow + cluster.height > boardColumn.bottomToTop.Count)
                        {
                            error = $"Footprint {i + 1} does not fit inside the board rows.";
                            return false;
                        }
                        for (int row = cluster.anchorRow;
                             row < cluster.anchorRow + cluster.height; row++)
                            if (!occupiedClusterCells.Add(new Vector2Int(column, row)))
                            {
                                error = $"Footprint {i + 1} overlaps another footprint.";
                                return false;
                            }
                    }
                    if (!tileCounts.ContainsKey(cluster.color)) tileCounts[cluster.color] = 0;
                    tileCounts[cluster.color] += cluster.value;
                }
            }
            for (int c = 0; c < boardColumns.Count; c++)
            {
                BoardColumnData column = boardColumns[c];
                if (column == null || column.bottomToTop == null || column.bottomToTop.Count == 0)
                {
                    error = $"Column {c + 1} is empty.";
                    return false;
                }

                for (int r = 0; r < column.bottomToTop.Count; r++)
                {
                    if (occupiedClusterCells.Contains(new Vector2Int(c, r))) continue;
                    FoodColor color = column.bottomToTop[r];
                    if (color == FoodColor.Neutral) continue;
                    if (!tileCounts.ContainsKey(color)) tileCounts[color] = 0;
                    tileCounts[color]++;
                }
            }

            if (customerLanes == null || customerLanes.Count == 0)
            {
                error = "Add at least one customer lane.";
                return false;
            }

            var demandCounts = new Dictionary<FoodColor, int>();
            int customerCount = 0;
            for (int lane = 0; lane < customerLanes.Count; lane++)
            {
                CustomerLaneData laneData = customerLanes[lane];
                if (laneData == null || laneData.frontToBack == null || laneData.frontToBack.Count == 0)
                {
                    error = $"Customer lane {lane + 1} is empty.";
                    return false;
                }

                for (int i = 0; i < laneData.frontToBack.Count; i++)
                {
                    CustomerOrderData order = laneData.frontToBack[i];
                    if (order == null)
                    {
                        error = $"Lane {lane + 1}, customer {i + 1} is missing.";
                        return false;
                    }
                    if (order.color == FoodColor.Neutral)
                    {
                        error = $"Lane {lane + 1}, customer {i + 1} cannot use Neutral.";
                        return false;
                    }
                    customerCount++;
                    if (!demandCounts.ContainsKey(order.color)) demandCounts[order.color] = 0;
                    demandCounts[order.color] += GetCustomerDemand(order.color);
                }
            }

            if (customerCount == 0)
            {
                error = "Add at least one customer.";
                return false;
            }

            foreach (var pair in tileCounts)
            {
                int demand = demandCounts.TryGetValue(pair.Key, out int value) ? value : 0;
                if (demand != pair.Value)
                {
                    error = $"Color {pair.Key}: board has {pair.Value} tiles but customers request {demand}.";
                    return false;
                }
            }

            foreach (var pair in demandCounts)
            {
                int tiles = tileCounts.TryGetValue(pair.Key, out int value) ? value : 0;
                if (tiles != pair.Value)
                {
                    error = $"Color {pair.Key}: customers request {pair.Value} but board has {tiles} tiles.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
