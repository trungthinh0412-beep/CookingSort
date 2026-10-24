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

        [Header("Customers - each lane is stored front to back")]
        [SerializeField] private List<CustomerLaneData> customerLanes = new List<CustomerLaneData>();

        public BoardController Board => board;
        public SeatBar SeatBar => seatBar;
        public CustomerQueue CustomerQueue => customerQueue;
        
public int LevelNumber => levelNumber;
        public int SeatCount => seatCount;
        public List<BoardColumnData> BoardColumns => boardColumns;
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
                        total += column.bottomToTop.Count;
                }
                return total;
            }
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
                    FoodColor color = column.bottomToTop[r];
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
