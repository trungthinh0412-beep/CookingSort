using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CookingSort2D
{
    public class SeatBar : MonoBehaviour
    {
        private readonly List<SeatSlotView> _slots = new List<SeatSlotView>();
        private CookingSortGameController _game;
        [SerializeField, Min(0.01f)] private float mergeDuration = 0.22f;
        private readonly HashSet<CustomerView> _mergingCustomers = new HashSet<CustomerView>();
        private bool _mergeInProgress;
        public bool IsMerging => _mergeInProgress;

        public bool IsFull
        {
            get
            {
                for (int i = 0; i < _slots.Count; i++)
                    if (_slots[i].gameObject.activeSelf && _slots[i].IsEmpty) return false;
                return true;
            }
        }

        public int OccupiedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Count; i++)
                    if (_slots[i].gameObject.activeSelf && !_slots[i].IsEmpty) count++;
                return count;
            }
        }

        private void Awake()
        {
            CacheSlots();
        }

public void Initialize(CookingSortGameController game, int seatCount)
        {
            StopAllCoroutines();
            _mergingCustomers.Clear();
            _mergeInProgress = false;
            _game = game;
            CacheSlots();
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].gameObject.SetActive(i < seatCount);
        }

public bool TrySeat(CustomerView customer)
        {
            if (customer == null) return false;
            for (int i = 0; i < _slots.Count; i++)
            {
                SeatSlotView slot = _slots[i];
                if (!slot.gameObject.activeSelf || !slot.IsEmpty) continue;
                slot.Attach(customer);
                TryBeginMerge();
                return true;
            }
            return false;
        }

        public CustomerView FindCustomerForColor(FoodColor color)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                SeatSlotView slot = _slots[i];
                if (!slot.gameObject.activeSelf || slot.IsEmpty) continue;
                CustomerView customer = slot.Occupant;
                if (customer != null && customer.ColorType == color && customer.Remaining > 0)
                    return customer;
            }
            return null;
        }

public void GetCustomersForColor(FoodColor color, List<CustomerView> results)
        {
            if (results == null) return;
            results.Clear();
            for (int i = 0; i < _slots.Count; i++)
            {
                SeatSlotView slot = _slots[i];
                if (!slot.gameObject.activeSelf || slot.IsEmpty) continue;
                CustomerView customer = slot.Occupant;
                if (customer != null && customer.ColorType == color && customer.Remaining > 0)
                    results.Add(customer);
            }
        }


        public bool HasCustomerForColor(FoodColor color)
        {
            return FindCustomerForColor(color) != null;
        }

        public void CompleteCustomer(CustomerView customer)
        {
            if (customer == null || IsMergingCustomer(customer)) return;
            for (int i = 0; i < _slots.Count; i++)
            {
                SeatSlotView slot = _slots[i];
                if (slot.Occupant != customer) continue;
                slot.Release();
                StartCoroutine(LeaveRoutine(customer));
                return;
            }
        }

public void ClearAllImmediate()
        {
            StopAllCoroutines();
            _mergingCustomers.Clear();
            _mergeInProgress = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                CustomerView c = _slots[i].Release();
                if (c != null) Destroy(c.gameObject);
            }
        }

        private void CacheSlots()
        {
            _slots.Clear();
            SeatSlotView[] found = GetComponentsInChildren<SeatSlotView>(true);
            for (int i = 0; i < found.Length; i++) _slots.Add(found[i]);
            _slots.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        }

        private IEnumerator LeaveRoutine(CustomerView customer)
        {
            if (customer == null) yield break;
            customer.transform.SetParent(transform, true);
            Vector3 start = customer.transform.position;
            Vector3 end = start + Vector3.up * 1.3f;
            float elapsed = 0f;
            const float duration = 0.22f;
            while (elapsed < duration && customer != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                customer.transform.position = Vector3.Lerp(start, end, t);
                customer.transform.localScale = Vector3.one * (1f - t);
                yield return null;
            }
            if (customer != null) Destroy(customer.gameObject);
            if (_game != null) _game.OnCustomerLeftSeat();
        }
    

private void FinishMerge()
        {
            _mergingCustomers.Clear();
            _mergeInProgress = false;
            TryBeginMerge();
            if (_game != null) _game.OnSeatMergeFinished();
        }


private IEnumerator MergeThreeRoutine(SeatSlotView keepSlot, SeatSlotView secondSlot, SeatSlotView thirdSlot)
        {
            CustomerView keep = keepSlot.Occupant;
            CustomerView second = secondSlot.Occupant;
            CustomerView third = thirdSlot.Occupant;

            // Stop dispatching to this group, but let tiles already in flight arrive first.
            BoardController board = _game != null ? _game.Board : null;
            while (board != null &&
                   (board.HasTileInFlight(keep) || board.HasTileInFlight(second) || board.HasTileInFlight(third)))
                yield return null;

            if (keep == null || second == null || third == null ||
                keepSlot.Occupant != keep || secondSlot.Occupant != second || thirdSlot.Occupant != third)
            {
                FinishMerge();
                yield break;
            }

            int combinedDemand = Mathf.Max(0, keep.Remaining) +
                                 Mathf.Max(0, second.Remaining) +
                                 Mathf.Max(0, third.Remaining);
            secondSlot.Release();
            thirdSlot.Release();
            second.transform.SetParent(transform, true);
            third.transform.SetParent(transform, true);

            Vector3 secondStart = second.transform.position;
            Vector3 thirdStart = third.transform.position;
            float duration = Mathf.Max(.01f, mergeDuration);
            float elapsed = 0f;
            while (elapsed < duration && keep != null && second != null && third != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                Vector3 target = keep.transform.position;
                second.transform.position = Vector3.Lerp(secondStart, target, eased);
                third.transform.position = Vector3.Lerp(thirdStart, target, eased);
                second.transform.localScale = Vector3.one * (.9f * (1f - eased));
                third.transform.localScale = Vector3.one * (.9f * (1f - eased));
                keep.transform.localScale = Vector3.one * (.9f + Mathf.Sin(t * Mathf.PI) * .16f);
                yield return null;
            }

            if (second != null) Destroy(second.gameObject);
            if (third != null) Destroy(third.gameObject);
            if (keep != null)
            {
                keep.transform.localScale = Vector3.one * .9f;
                keep.SetRemaining(combinedDemand);
            }

            FinishMerge();
            if (keep != null && combinedDemand == 0)
                CompleteCustomer(keep);
        }


private void TryBeginMerge()
        {
            if (_mergeInProgress) return;

            // Any three occupied seats of the same color can merge, regardless of position.
            for (int i = 0; i < _slots.Count; i++)
            {
                SeatSlotView first = _slots[i];
                if (first == null || !first.gameObject.activeSelf || first.IsEmpty) continue;
                for (int j = i + 1; j < _slots.Count; j++)
                {
                    SeatSlotView second = _slots[j];
                    if (second == null || !second.gameObject.activeSelf || second.IsEmpty ||
                        second.Occupant.ColorType != first.Occupant.ColorType) continue;
                    for (int k = j + 1; k < _slots.Count; k++)
                    {
                        SeatSlotView third = _slots[k];
                        if (third == null || !third.gameObject.activeSelf || third.IsEmpty ||
                            third.Occupant.ColorType != first.Occupant.ColorType) continue;

                        _mergeInProgress = true;
                        _mergingCustomers.Add(first.Occupant);
                        _mergingCustomers.Add(second.Occupant);
                        _mergingCustomers.Add(third.Occupant);
                        StartCoroutine(MergeThreeRoutine(first, second, third));
                        return;
                    }
                }
            }
        }


public bool IsMergingCustomer(CustomerView customer)
        {
            return customer != null && _mergingCustomers.Contains(customer);
        }
}
}
