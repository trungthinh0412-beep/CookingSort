using System.Collections.Generic;
using UnityEngine;

namespace CookingSort2D
{
    public class CustomerQueue : MonoBehaviour
    {
        [SerializeField] private CustomerView customerPrefab;
        
        [SerializeField] private Transform slotsRoot;
        [SerializeField] private float slotSpacing = 1.05f;
[SerializeField] private Transform customersRoot;
        [SerializeField] private float laneSpacing = 1.16f;
        [SerializeField] private float rowSpacing = 0.82f;

        
        private readonly List<CustomerQueueSlotView> _slots = new List<CustomerQueueSlotView>();
private readonly List<List<CustomerView>> _lanes = new List<List<CustomerView>>();
        private CookingSortGameController _game;

        public bool HasWaitingCustomers
        {
            get
            {
                for (int i = 0; i < _lanes.Count; i++)
                    if (_lanes[i].Count > 0) return true;
                return false;
            }
        }

private void Awake()
        {
            AutoBindRoots();
            CacheSlots();
        }

public void Initialize(CookingSortGameController game, CookingBlastLevel data)
        {
            _game = game;
            AutoBindRoots();
            CacheSlots();
            ClearCustomers();
            if (data == null || customerPrefab == null || customersRoot == null) return;

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
                    CustomerView view = Instantiate(customerPrefab, customersRoot);
                    view.name = $"Customer_L{lane}_R{row}_{order.color}";
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
            if (!_game.TrySeatCustomer(customer)) return;

            lane.RemoveAt(0);
            RefreshLane(laneIndex, true);
        }

        public bool SelectFrontOfLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= _lanes.Count || _lanes[laneIndex].Count == 0) return false;
            CustomerView front = _lanes[laneIndex][0];
            TrySelect(front);
            return front != null && front.IsSeated;
        }

        public void ClearCustomers()
        {
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
                if (animate) view.AnimateTo(target); else view.transform.localPosition = target;
                view.SetFront(row == 0);
                view.SetSortingOrder(24 - row * 2);
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
