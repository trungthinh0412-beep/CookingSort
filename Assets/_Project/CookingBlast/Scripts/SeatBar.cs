using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CookingSort2D
{
    public class SeatBar : MonoBehaviour
    {
        private readonly List<SeatSlotView> _slots = new List<SeatSlotView>();
        private CookingSortGameController _game;
        [Header("Animal on Seat")]
        [SerializeField, Min(0.01f)] private float seatedAnimalScale = 1f;
        [SerializeField, Min(0.01f)] private float seatFlyDuration = 0.32f;
        [SerializeField, Min(0f)] private float seatFlyArcHeight = 0.55f;
        [SerializeField, Range(0f, 0.3f)] private float seatFlyScalePunch = 0.12f;

        [Header("Jelly merge animation")]
        [SerializeField, Min(0.01f)] private float mergeDuration = 0.22f;
        [SerializeField, Min(0.01f)] private float mergePopDuration = 0.16f;
        [SerializeField, Min(0.01f)] private float mergeFuseDuration = 0.14f;
        [SerializeField, Min(0.01f)] private float mergeDropDuration = 0.24f;
        [SerializeField, Min(0f)] private float mergeLift = 0.25f;
        [SerializeField, Min(0f)] private float mergeReboundHeight = 0.055f;
        [Header("Customer exit animation")]
        [SerializeField, Min(0.01f)] private float exitFlyDuration = 0.48f;
        [SerializeField, Min(0f)] private float exitSpinDegrees = 450f;
        [SerializeField, Min(0f)] private float exitThrowHeight = 1f;
        [SerializeField, Min(0f)] private float exitDropDistance = 0.28f;
        [SerializeField, Min(0f)] private float exitOffscreenMargin = 0.2f;
        private readonly HashSet<CustomerView> _mergingCustomers = new HashSet<CustomerView>();
        private readonly HashSet<CustomerView> _departingCustomers = new HashSet<CustomerView>();
        private int _departingClusterEffects;
        private bool _mergeInProgress;
        private int _seatingCustomers;
        public bool IsMerging => _mergeInProgress;
        public bool IsSeating => _seatingCustomers > 0;
        public bool HasDepartingCustomers =>
            _departingCustomers.Count > 0 || _departingClusterEffects > 0;

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
            ClearDepartingCustomers();
            _mergingCustomers.Clear();
            _mergeInProgress = false;
            _seatingCustomers = 0;
            _departingClusterEffects = 0;
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
                slot.Attach(
                    customer,
                    seatedAnimalScale,
                    seatFlyDuration,
                    seatFlyArcHeight,
                    seatFlyScalePunch);
                _seatingCustomers++;
                StartCoroutine(FinishSeatFlightRoutine(customer));
                return true;
            }
            return false;
        }

        private IEnumerator FinishSeatFlightRoutine(CustomerView customer)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, seatFlyDuration));

            // CustomerView owns the actual seat-flight animation. Its coroutine
            // can finish one frame after this timer depending on update order.
            // Do not wake the Board until the animal is truly selectable.
            while (customer != null && customer.IsSeated && !customer.IsSeatReady)
                yield return null;

            _seatingCustomers = Mathf.Max(0, _seatingCustomers - 1);
            if (_seatingCustomers > 0) yield break;

            TryBeginMerge();
            if (_game != null) _game.OnCustomerSeatFlightFinished();
        }

        public CustomerView FindCustomerForColor(FoodColor color)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                SeatSlotView slot = _slots[i];
                if (!slot.gameObject.activeSelf || slot.IsEmpty) continue;
                CustomerView customer = slot.Occupant;
                if (customer != null && customer.IsSeatReady &&
                    customer.ColorType == color && customer.Remaining > 0)
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
                if (customer != null && customer.IsSeatReady &&
                    customer.ColorType == color && customer.Remaining > 0)
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
            ClearDepartingCustomers();
            _mergingCustomers.Clear();
            _mergeInProgress = false;
            _departingClusterEffects = 0;
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

            // The slot is free immediately. Animate the departing customer in world
            // space so a newly seated customer cannot pull this one back into place.
            _departingCustomers.Add(customer);
            customer.StopServePulse();
            customer.transform.SetParent(transform, true);
            customer.SetSortingOrder(270);

            Transform sprite = customer.transform;
            yield return ThrowOutsideRoutine(sprite);

            _departingCustomers.Remove(customer);
            if (customer != null) Destroy(customer.gameObject);
            if (_game != null) _game.OnCustomerLeftSeat();
        }

        public void ThrowClusterOutside(FoodTileView clusterView)
        {
            if (clusterView == null) return;
            clusterView.transform.SetParent(transform, true);
            // Keep the departing Cluster below seated/departing Animals.
            clusterView.SetSortingOrder(228);
            _departingClusterEffects++;
            StartCoroutine(ThrowClusterOutsideRoutine(clusterView));
        }

        private IEnumerator ThrowClusterOutsideRoutine(FoodTileView clusterView)
        {
            if (clusterView != null)
            {
                yield return ThrowOutsideRoutine(clusterView.transform);
                if (clusterView != null) Destroy(clusterView.gameObject);
            }
            _departingClusterEffects = Mathf.Max(0, _departingClusterEffects - 1);
            if (_game != null) _game.OnBoardEffectFinished();
        }

        private IEnumerator ThrowOutsideRoutine(Transform sprite)
        {
            if (sprite == null) yield break;
            Vector3 start = sprite.position;
            Quaternion startRotation = sprite.rotation;

            // Always throw left, past the camera boundary, rather than upwards.
            float targetX = start.x - 4f;
            Camera gameCamera = Camera.main;
            if (gameCamera != null)
            {
                float depth = Vector3.Dot(start - gameCamera.transform.position,
                    gameCamera.transform.forward);
                Vector3 outsideLeft = gameCamera.ViewportToWorldPoint(
                    new Vector3(-Mathf.Max(0f, exitOffscreenMargin), 0.5f, depth));
                targetX = Mathf.Min(start.x - 2f, outsideLeft.x);
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, exitFlyDuration);
            while (elapsed < duration && sprite != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Shared thrown-object arc for completed Animals and Clusters.
                float arc = 4f * t * (1f - t) * Mathf.Max(0f, exitThrowHeight);
                float fall = t * t * Mathf.Max(0f, exitDropDistance);
                sprite.position = new Vector3(
                    Mathf.LerpUnclamped(start.x, targetX, t),
                    start.y + arc - fall,
                    start.z);
                sprite.rotation = startRotation *
                    Quaternion.Euler(0f, 0f, Mathf.Max(0f, exitSpinDegrees) * t);
                yield return null;
            }
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

            // No tiles may land on a customer while it is turning into jelly.
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

            keep.PrepareForMerge();
            second.PrepareForMerge();
            third.PrepareForMerge();

            int combinedDemand = Mathf.Max(0, keep.Remaining) +
                                 Mathf.Max(0, second.Remaining) +
                                 Mathf.Max(0, third.Remaining);

            // Keep all three seats occupied until the entire merge/drop animation ends.
            // Fast taps must not put a new customer under a moving jelly blob.
            Vector3 keepSeatPosition = keep.transform.position;
            Vector3 secondSeatPosition = second.transform.position;
            Vector3 thirdSeatPosition = third.transform.position;
            Vector3 keepScale = keep.transform.localScale;
            Vector3 secondScale = second.transform.localScale;
            Vector3 thirdScale = third.transform.localScale;
            float lift = Mathf.Max(0f, mergeLift);
            Vector3 mergeCenter = keepSeatPosition + Vector3.up * lift;
            keep.SetSortingOrder(242);
            second.SetSortingOrder(240);
            third.SetSortingOrder(240);

            // 1. All three squash, stretch, and pop up like elastic jelly.
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, mergePopDuration);
            while (elapsed < duration && keep != null && second != null && third != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float height = lift * Mathf.Sin(t * Mathf.PI * 0.5f);
                float squish = Mathf.Sin(t * Mathf.PI * 2f);
                Vector3 jelly = new Vector3(1f + 0.14f * squish, 1f - 0.18f * squish, 1f);
                keep.transform.position = keepSeatPosition + Vector3.up * height;
                second.transform.position = secondSeatPosition + Vector3.up * height;
                third.transform.position = thirdSeatPosition + Vector3.up * height;
                keep.transform.localScale = Vector3.Scale(keepScale, jelly);
                second.transform.localScale = Vector3.Scale(secondScale, jelly);
                third.transform.localScale = Vector3.Scale(thirdScale, jelly);
                yield return null;
            }
            if (keep == null || second == null || third == null)
            {
                FinishMerge();
                yield break;
            }

            // 2. Slide towards the chosen seat, stretching as the blobs touch.
            elapsed = 0f;
            duration = Mathf.Max(0.01f, mergeDuration);
            while (elapsed < duration && keep != null && second != null && third != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                float wobble = Mathf.Sin(t * Mathf.PI);
                keep.transform.position = mergeCenter + Vector3.up * (0.025f * wobble);
                second.transform.position = Vector3.LerpUnclamped(secondSeatPosition + Vector3.up * lift, mergeCenter, eased);
                third.transform.position = Vector3.LerpUnclamped(thirdSeatPosition + Vector3.up * lift, mergeCenter, eased);
                keep.transform.localScale = Vector3.Scale(keepScale, new Vector3(1f + 0.11f * wobble, 1f - 0.09f * wobble, 1f));
                second.transform.localScale = Vector3.Scale(secondScale, new Vector3(1f - 0.27f * eased + 0.06f * wobble, 1f + 0.10f * eased, 1f));
                third.transform.localScale = Vector3.Scale(thirdScale, new Vector3(1f - 0.27f * eased + 0.06f * wobble, 1f + 0.10f * eased, 1f));
                yield return null;
            }
            if (keep == null || second == null || third == null)
            {
                FinishMerge();
                yield break;
            }

            // 3. Absorb the other two: swell wide, compress vertically, then recoil.
            second.transform.position = mergeCenter;
            third.transform.position = mergeCenter;
            elapsed = 0f;
            duration = Mathf.Max(0.01f, mergeFuseDuration);
            while (elapsed < duration && keep != null && second != null && third != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                float impact = Mathf.Sin(t * Mathf.PI);
                keep.transform.position = mergeCenter + Vector3.up * (0.04f * impact);
                keep.transform.localScale = Vector3.Scale(keepScale, new Vector3(1f + 0.30f * impact, 1f - 0.22f * impact, 1f));
                second.transform.localScale = Vector3.Scale(secondScale, new Vector3(0.73f * (1f - eased), 1.10f * (1f - eased), 1f));
                third.transform.localScale = Vector3.Scale(thirdScale, new Vector3(0.73f * (1f - eased), 1.10f * (1f - eased), 1f));
                yield return null;
            }
            if (keep == null || second == null || third == null)
            {
                FinishMerge();
                yield break;
            }

            second.transform.localScale = Vector3.zero;
            third.transform.localScale = Vector3.zero;
            keep.SetRemaining(combinedDemand);
            keep.transform.position = mergeCenter;
            keep.transform.localScale = keepScale;

            // 4. The merged blob falls onto its seat, squashes on impact,
            // then wobbles and bounces to a stable, full-size shape.
            elapsed = 0f;
            duration = Mathf.Max(0.01f, mergeDropDuration);
            while (elapsed < duration && keep != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (t < 0.70f)
                {
                    float falling = t / 0.70f;
                    float stretch = Mathf.Sin(falling * Mathf.PI * 0.9f);
                    keep.transform.position = Vector3.LerpUnclamped(mergeCenter, keepSeatPosition, Mathf.Pow(falling, 1.4f));
                    keep.transform.localScale = Vector3.Scale(keepScale, new Vector3(1f - 0.16f * stretch, 1f + 0.22f * stretch, 1f));
                }
                else
                {
                    float settle = (t - 0.70f) / 0.30f;
                    float damping = Mathf.Pow(1f - settle, 3f);
                    float rebound = Mathf.Abs(Mathf.Sin(settle * Mathf.PI * 2f)) * (1f - settle) * Mathf.Max(0f, mergeReboundHeight);
                    float jelly = Mathf.Cos(settle * Mathf.PI * 4f) * damping;
                    keep.transform.position = keepSeatPosition + Vector3.up * rebound;
                    keep.transform.localScale = Vector3.Scale(keepScale, new Vector3(1f + 0.32f * jelly, 1f - 0.25f * jelly, 1f));
                }
                yield return null;
            }

            if (keep != null)
            {
                keep.transform.position = keepSeatPosition;
                keep.transform.localScale = keepScale;
                keep.SetSortingOrder(230);
            }
            secondSlot.Release();
            thirdSlot.Release();
            if (second != null) Destroy(second.gameObject);
            if (third != null) Destroy(third.gameObject);

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


private void ClearDepartingCustomers()
        {
            foreach (CustomerView customer in _departingCustomers)
                if (customer != null) Destroy(customer.gameObject);
            _departingCustomers.Clear();
        }
}
}
