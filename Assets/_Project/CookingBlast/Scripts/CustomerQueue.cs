using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CookingSort2D
{
    public class CustomerQueue : MonoBehaviour
    {
        
        [Header("Animal Prefabs By Match Type")]
        [Tooltip("Prefab list order is arbitrary. Animal Type must match a Tile Type, not its list index.")]
        [FormerlySerializedAs("animalPrefabsByColor")]
        [SerializeField] private CustomerView[] animalPrefabsByType = new CustomerView[10];
[SerializeField] private CustomerView customerPrefab;
        
        [SerializeField] private Transform slotsRoot;
        [SerializeField] private float slotSpacing = 1.05f;
[SerializeField] private Transform customersRoot;
        [SerializeField] private float laneSpacing = 1.16f;
        [SerializeField] private float rowSpacing = 0.82f;

        [Header("Queue Game Feel")]
        [SerializeField, Min(0.01f)] private float frontRowScale = 1.1f;
        [Tooltip("Scale of the first row behind the selectable waiting row.")]
        [SerializeField, Min(0.01f)] private float backRowsScale = 0.9f;
        [Tooltip("Scale of the second and all deeper rows, so they remain readable without competing with the active row.")]
        [SerializeField, Min(0.01f)] private float farBackRowsScale = 0.74f;
        [SerializeField, Min(0.01f)] private float pushDuration = 0.2f;
        [SerializeField, Range(0f, 0.3f)] private float pushScalePunch = 0.08f;

        
        private readonly List<CustomerQueueSlotView> _slots = new List<CustomerQueueSlotView>();
private readonly List<List<CustomerView>> _lanes = new List<List<CustomerView>>();
        private CookingSortGameController _game;
        private bool _orderBellSelection;

        public bool HasWaitingCustomers
        {
            get
            {
                for (int i = 0; i < _lanes.Count; i++)
                    if (_lanes[i].Count > 0) return true;
                return false;
            }
        }

        /// <summary>
        /// Appends one queue row from left to right across all lanes.
        /// queueRowIndex 0 is booster row 2; index 1 is booster row 3.
        /// </summary>
        public void GetBoosterRow(int queueRowIndex, List<CustomerView> results)
        {
            if (results == null) return;
            results.Clear();
            if (queueRowIndex < 0) return;

            for (int lane = 0; lane < _lanes.Count; lane++)
            {
                List<CustomerView> customers = _lanes[lane];
                if (customers == null || queueRowIndex >= customers.Count) continue;
                CustomerView customer = customers[queueRowIndex];
                if (customer != null) results.Add(customer);
            }
        }

private void Awake()
        {
            AutoBindRoots();
            CacheSlots();
        }



public CustomerView GetAnimalPrefab(FoodColor type)
        {
            if (animalPrefabsByType != null)
                for (int i = 0; i < animalPrefabsByType.Length; i++)
                    if (animalPrefabsByType[i] != null && animalPrefabsByType[i].Type == type)
                        return animalPrefabsByType[i];
            return customerPrefab != null && customerPrefab.Type == type ? customerPrefab : null;
        }

public CustomerView GetAnimalPrefab(CustomerOrderData order)
        {
            if (order == null) return null;
            CustomerView overridePrefab = order.animalPrefab;
            if (overridePrefab != null && overridePrefab.Type == order.color &&
                animalPrefabsByType != null)
            {
                for (int i = 0; i < animalPrefabsByType.Length; i++)
                    if (animalPrefabsByType[i] == overridePrefab) return overridePrefab;
            }
            return GetAnimalPrefab(order.color);
        }



        public int ConfiguredAnimalPrefabCount =>
            animalPrefabsByType != null ? animalPrefabsByType.Length : 0;

        /// <summary>Returns only the Animal explicitly stored in the list; never uses AnimalBase.</summary>
public CustomerView GetConfiguredAnimalPrefab(int index)
        {
            // Return the actual list entry. Prefab order need not follow enum IDs.
            return animalPrefabsByType != null && index >= 0 &&
                index < animalPrefabsByType.Length ? animalPrefabsByType[index] : null;
        }


public void Initialize(CookingSortGameController game, CookingBlastLevel data)
        {
            _game = game;
            AutoBindRoots();
            CacheSlots();
            ClearCustomers();
            if (data == null || customersRoot == null) return;

            int laneCount = Mathf.Max(1, data.CustomerLanes.Count);
            EnsureSlotCount(laneCount);
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].SetActiveSlot(i < laneCount);

            for (int lane = 0; lane < laneCount; lane++)
            {
                var list = new List<CustomerView>();
                _lanes.Add(list);
                CustomerLaneData laneData = data.CustomerLanes[lane];
                if (laneData == null || laneData.frontToBack == null) continue;

                for (int row = 0; row < laneData.frontToBack.Count; row++)
                {
                    CustomerOrderData order = laneData.frontToBack[row];
                    if (order == null) continue;
                    CustomerView animalPrefab = GetAnimalPrefab(order);
                    if (animalPrefab == null)
                    {
                        Debug.LogError("[CookingBlast] Missing Animal prefab for " +
                            order.color + " on " + name);
                        continue;
                    }
                    CustomerView view = Instantiate(animalPrefab, customersRoot);
                    // Keep the prefab-authored name and scale.
                    view.BindQueue(this, lane, order);
                    list.Add(view);
                }
                RefreshLane(lane, false);
            }
        }

        public void TrySelect(CustomerView customer)
        {
            if (customer == null || _game == null) return;
            int laneIndex = FindLane(customer);
            if (laneIndex < 0) return;
            List<CustomerView> lane = _lanes[laneIndex];
            if (lane.Count == 0 || lane[0] != customer) return;
            if (!_game.TrySeatCustomer(customer, laneIndex, 0)) return;

            lane.RemoveAt(0);
            RefreshLane(laneIndex, true);
        }

        public void HandleCustomerClick(CustomerView customer)
        {
            if (customer == null || _game == null) return;
            if (_game.HandleBoosterCustomerClick(customer)) return;
            TrySelect(customer);
        }

        public bool TryPromoteWithOrderBell(CustomerView customer)
        {
            if (customer == null || _game == null) return false;
            for (int lane = 0; lane < _lanes.Count; lane++)
            {
                int row = _lanes[lane].IndexOf(customer);
                // Queue row 0 is gameplay row 2, so every waiting animal is valid.
                if (row < 0) continue;
                if (!_game.TrySeatCustomer(customer, lane, row)) return false;
                _lanes[lane].RemoveAt(row);
                RefreshLane(lane, true);
                return true;
            }
            return false;
        }

        public bool RestoreCustomer(CustomerView customer, int laneIndex, int rowIndex)
        {
            if (customer == null || customersRoot == null || _lanes.Count == 0) return false;
            laneIndex = Mathf.Clamp(laneIndex, 0, _lanes.Count - 1);
            List<CustomerView> lane = _lanes[laneIndex];
            rowIndex = Mathf.Clamp(rowIndex, 0, lane.Count);
            customer.ReturnToQueue(this, laneIndex, customersRoot);
            lane.Insert(rowIndex, customer);
            RefreshLane(laneIndex, true);
            return true;
        }

        public bool ShuffleWaitingCustomers(Dictionary<FoodColor, int> bottomTypeWeights)
        {
            var customers = new List<CustomerView>();
            var laneCounts = new List<int>(_lanes.Count);
            for (int lane = 0; lane < _lanes.Count; lane++)
            {
                laneCounts.Add(_lanes[lane].Count);
                customers.AddRange(_lanes[lane]);
            }
            if (customers.Count < 2) return false;

            // Shuffle everyone first. Then promote at most ONE animal for each
            // type exposed at the bottom; duplicate animals of that type stay
            // in the random remainder instead of filling the whole front row.
            for (int i = customers.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                CustomerView temp = customers[i];
                customers[i] = customers[swap];
                customers[swap] = temp;
            }

            var prioritized = new List<CustomerView>();
            var remainder = new List<CustomerView>();
            var prioritizedTypes = new HashSet<FoodColor>();
            for (int i = 0; i < customers.Count; i++)
            {
                CustomerView customer = customers[i];
                bool exposed = customer != null && bottomTypeWeights != null &&
                               bottomTypeWeights.ContainsKey(customer.Type);
                if (exposed && prioritizedTypes.Add(customer.Type))
                    prioritized.Add(customer);
                else
                    remainder.Add(customer);
            }
            customers.Clear();
            customers.AddRange(prioritized);
            customers.AddRange(remainder);

            for (int lane = 0; lane < _lanes.Count; lane++) _lanes[lane].Clear();
            int cursor = 0;
            int maxRows = 0;
            for (int lane = 0; lane < laneCounts.Count; lane++)
                maxRows = Mathf.Max(maxRows, laneCounts[lane]);
            for (int row = 0; row < maxRows; row++)
            {
                for (int lane = 0; lane < _lanes.Count; lane++)
                {
                    if (row >= laneCounts[lane] || cursor >= customers.Count) continue;
                    CustomerView customer = customers[cursor++];
                    _lanes[lane].Add(customer);
                    if (customer != null) customer.SetQueueLane(this, lane);
                }
            }
            for (int lane = 0; lane < _lanes.Count; lane++) RefreshLane(lane, true);
            return true;
        }

        public void SetOrderBellSelection(bool active)
        {
            if (_orderBellSelection == active) return;
            _orderBellSelection = active;
            for (int lane = 0; lane < _lanes.Count; lane++)
            {
                for (int row = 0; row < _lanes[lane].Count; row++)
                    if (_lanes[lane][row] != null)
                        _lanes[lane][row].SetBoosterSelectable(active);
                RefreshLane(lane, true);
            }
        }

        public bool SelectFrontOfLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= _lanes.Count || _lanes[laneIndex].Count == 0) return false;
            CustomerView front = _lanes[laneIndex][0];
            TrySelect(front);
            return front != null && front.IsSeated;
        }

        public int ReduceDemandFromBack(FoodColor type, int amount)
        {
            int deepest = 0;
            foreach (var lane in _lanes) deepest = Mathf.Max(deepest, lane.Count);
            var changedLanes = new HashSet<int>();
            for (int row = deepest - 1; row >= 0 && amount > 0; row--)
                for (int lane = 0; lane < _lanes.Count && amount > 0; lane++)
                {
                    if (row >= _lanes[lane].Count) continue;
                    var customer = _lanes[lane][row];
                    if (customer == null || customer.Type != type) continue;
                    amount -= customer.ReduceDemand(amount);
                    if (customer.Remaining > 0) continue;
                    _lanes[lane].RemoveAt(row);
                    customer.gameObject.SetActive(false);
                    Destroy(customer.gameObject);
                    changedLanes.Add(lane);
                }
            foreach (int lane in changedLanes) RefreshLane(lane, true);
            return amount;
        }

        public void ClearCustomers()
        {
            _orderBellSelection = false;
            _lanes.Clear();
            if (customersRoot == null) return;
            for (int i = customersRoot.childCount - 1; i >= 0; i--)
                Destroy(customersRoot.GetChild(i).gameObject);
        }

        private int FindLane(CustomerView customer)
        {
            for (int lane = 0; lane < _lanes.Count; lane++)
                if (_lanes[lane].Count > 0 && _lanes[lane][0] == customer) return lane;
            return -1;
        }

private void RefreshLane(int laneIndex, bool animate)
        {
            if (laneIndex < 0 || laneIndex >= _lanes.Count) return;
            List<CustomerView> lane = _lanes[laneIndex];

            Vector3 slotPosition;
            if (laneIndex < _slots.Count && _slots[laneIndex] != null)
                slotPosition = customersRoot.InverseTransformPoint(_slots[laneIndex].transform.position);
            else
            {
                int laneCount = Mathf.Max(1, _lanes.Count);
                float startX = -(laneCount - 1) * laneSpacing * 0.5f;
                slotPosition = new Vector3(startX + laneIndex * laneSpacing, 0f, 0f);
            }

            for (int row = 0; row < lane.Count; row++)
            {
                CustomerView view = lane[row];
                if (view == null) continue;
                Vector3 target = slotPosition + new Vector3(0f, -row * rowSpacing, 0f);
                float rowScale = row == 0 || _orderBellSelection
                    ? frontRowScale
                    : row == 1 ? backRowsScale : farBackRowsScale;
                Vector3 targetScale = Vector3.one * rowScale;
                if (animate)
                    view.AnimateTo(
                        target,
                        targetScale,
                        pushDuration,
                        pushScalePunch,
                        row == 0 && !view.IsFront);
                else
                {
                    view.transform.localPosition = target;
                    view.transform.localScale = targetScale;
                }
                view.SetFront(row == 0);
                view.SetSortingOrder(224 - row * 2);
            }
        }
    

private void AutoBindRoots()
        {
            if (customersRoot == null)
            {
                Transform t = transform.Find("Customers");
                if (t != null) customersRoot = t;
            }
            if (slotsRoot == null)
            {
                Transform t = transform.Find("Slots");
                if (t != null) slotsRoot = t;
            }
        }

        private void CacheSlots()
        {
            _slots.Clear();
            if (slotsRoot == null) return;
            CustomerQueueSlotView[] found = slotsRoot.GetComponentsInChildren<CustomerQueueSlotView>(true);
            for (int i = 0; i < found.Length; i++) _slots.Add(found[i]);
            _slots.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        }

        private void EnsureSlotCount(int count)
        {
            if (slotsRoot == null || count <= 0) return;
            CacheSlots();
            if (_slots.Count == 0) return;

            float startX = -(count - 1) * slotSpacing * 0.5f;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] == null) continue;
                _slots[i].transform.localPosition = new Vector3(startX + i * slotSpacing, 0f, 0f);
            }
        }
}
}
