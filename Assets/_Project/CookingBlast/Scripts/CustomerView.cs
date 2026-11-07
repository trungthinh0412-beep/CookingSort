using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CookingSort2D
{
    [System.Serializable]
    public sealed class SeatFlightFeelSettings
    {
        [Tooltip("Short squash before the animal is thrown upwards.")]
        [Min(0f)] public float launchDuration = 0.04f;
        [Tooltip("Amount of squash while preparing to leave the queue. Lower values make the take-off feel softer without changing flight time.")]
        [Range(0f, 0.35f)] public float launchSquash = 0.035f;
        [Tooltip("Time for the impact squash and small landing rebound.")]
        [Min(0f)] public float landingDuration = 0.14f;
        [Range(0f, 35f)] public float tiltDegrees = 18f;
        [Range(0f, 0.35f)] public float landingSquash = 0.16f;
        [Min(0f)] public float reboundHeight = 0.07f;
    }

    public class CustomerView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Matching")]    
        [Tooltip("This Animal only eats Tiles with the same Match Type.")]
        [SerializeField] private FoodColor matchType = FoodColor.Blue;

        [SerializeField] private SkeletonAnimation body;
        [Header("Animal Outlines")]
        [Tooltip("Black outline object. Hidden during the seat flight. Assign manually.")]
        [SerializeField] private GameObject blackOutline;
        [Tooltip("White outline object, visible only in gameplay row 2. Assign manually.")]
        [SerializeField] private GameObject whiteOutline;
        [Header("Spine Animations")]
        [SerializeField, SpineAnimation(dataField: nameof(body))]
        private string queueAnimation;
        [SerializeField, SpineAnimation(dataField: nameof(body))]
        private string queueAnimation2;
        [SerializeField, SpineAnimation(dataField: nameof(body))]
        private string advanceTransitionAnimation;
        [SerializeField, SpineAnimation(dataField: nameof(body))]
        private string advanceAnimation;
        [SerializeField, SpineAnimation(dataField: nameof(body))]
        private string advanceAnimation2;
        [Tooltip("Random seconds spent in animation 1 before playing animation 2, independently per animal.")]
        [SerializeField] private Vector2 animation2DelayRange = new Vector2(2f, 6f);
        [SerializeField, SpineAnimation(dataField: nameof(body))]
        private string flyAnimation;
        [SerializeField, SpineAnimation(dataField: nameof(body))]
        private string seatAnimation;
        [SerializeField] private TextMeshProUGUI count;
        [SerializeField] private Image countBadge;
        [SerializeField] private Collider2D hitbox;
        [SerializeField] private Image demandFill;
        [SerializeField, Min(1)] private int demand = 20;

        private CustomerQueue _queue;
        private int _laneIndex = -1;
        private bool _front;
        private bool _seated;
        private bool _seatReady;
        private bool _boosterSelectable;
        private int _progressCapacity;
        private Coroutine _pulseRoutine;
        private Coroutine _animationSequence;
        private static readonly List<CustomerView> Animation2Waiting = new List<CustomerView>();
        private static readonly HashSet<CustomerView> Animation2Playing = new HashSet<CustomerView>();
        private static float _nextAnimation2Start;
        private float _animation2CooldownUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAnimation2Coordinator()
        {
            Animation2Waiting.Clear();
            Animation2Playing.Clear();
            _nextAnimation2Start = 0f;
        }

        private void OnDisable()
        {
            StopAnimationSequence();
        }

        private void ReleaseAnimation2()
        {
            Animation2Waiting.Remove(this);
            if (Animation2Playing.Remove(this))
                _animation2CooldownUntil = Time.time + Random.Range(5f, 10f);
        }

        private bool TryStartAnimation2()
        {
            if (!Animation2Waiting.Contains(this)) Animation2Waiting.Add(this);
            if (Time.timeScale <= 0f || Time.time < _nextAnimation2Start ||
                Animation2Playing.Count >= 2) return false;

            float totalWeight = 0f;
            foreach (var customer in Animation2Waiting)
                if (customer != null && customer.isActiveAndEnabled &&
                    Time.time >= customer._animation2CooldownUntil)
                    totalWeight += customer._front ? 3f : 1f;
            if (totalWeight <= 0f) return false;

            float pick = Random.Range(0f, totalWeight);
            CustomerView selected = null;
            foreach (var customer in Animation2Waiting)
            {
                if (customer == null || !customer.isActiveAndEnabled ||
                    Time.time < customer._animation2CooldownUntil) continue;
                pick -= customer._front ? 3f : 1f;
                selected = customer;
                if (pick <= 0f) break;
            }
            if (selected == null) return false;
            // Grant centrally, so update order cannot make the first animal win every turn.
            Animation2Waiting.Remove(selected);
            Animation2Playing.Add(selected);
            _nextAnimation2Start = Time.time + Random.Range(0.8f, 1.5f);
            return selected == this;
        }
        private string _sequenceFirst;
        private string _sequenceSecond;
        private bool _mergeOutlineHidden;
        private Vector3 _seatedBaseScale = Vector3.one;
        private Coroutine _moveRoutine;
        private bool _queueTransitionPlaying;

        // FoodColor is the legacy serialized name. Runtime matching is by Type.
        public FoodColor Type => matchType;
        public FoodColor ColorType => Type;
        public int Remaining { get; private set; }
        public int ProgressCapacity => Mathf.Max(1, _progressCapacity);
        public bool IsSeated => _seated;
        public bool IsSeatReady => _seated && _seatReady;
        public bool IsFront => _front;
        public bool HasMerged { get; private set; }
        public int Demand => Mathf.Max(1, demand);

        public void BindQueue(CustomerQueue queue, int laneIndex, CustomerOrderData order)
        {
            _queue = queue;
            _laneIndex = laneIndex;
            _seated = false;
            _seatReady = false;
            HasMerged = false;
            if (body != null) body.gameObject.SetActive(true);
            PlayAnimationSequence(queueAnimation, queueAnimation2);
            Remaining = Demand;
            _progressCapacity = Demand;
            RefreshVisual();
            SetFront(false);
            transform.localScale = Vector3.one;
        }




        public void SetFront(bool front)
        {
            _front = front && !_seated;
            RefreshWhiteOutline();
            if (hitbox != null) hitbox.enabled = !_seated && (_front || _boosterSelectable);
            if (!_queueTransitionPlaying && !_seated)
                PlayAnimationSequence(
                    _front ? advanceAnimation : queueAnimation,
                    _front ? advanceAnimation2 : queueAnimation2);
        }

        public void SetBoosterSelectable(bool selectable)
        {
            _boosterSelectable = selectable && !_seated;
            if (hitbox != null) hitbox.enabled = !_seated && (_front || _boosterSelectable);
        }

        private void RefreshWhiteOutline()
        {
            bool flyingToSeat = _seated && !_seatReady;
            if (blackOutline != null)
                blackOutline.SetActive(!flyingToSeat && !_mergeOutlineHidden);
            if (whiteOutline != null)
                whiteOutline.SetActive(_front && !_seated && !flyingToSeat);
        }

        public void SetQueueLane(CustomerQueue queue, int laneIndex)
        {
            _queue = queue;
            _laneIndex = laneIndex;
        }

        public void ReturnToQueue(CustomerQueue queue, int laneIndex, Transform parent)
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }
            StopServePulse();
            transform.SetParent(parent, true);
            transform.localRotation = Quaternion.identity;
            _queue = queue;
            _laneIndex = laneIndex;
            _seated = false;
            _seatReady = false;
            _front = false;
            _boosterSelectable = false;
            _queueTransitionPlaying = false;
            SetDemandBarVisible(false);
            RefreshWhiteOutline();
            if (hitbox != null) hitbox.enabled = false;
            PlayAnimationSequence(queueAnimation, queueAnimation2);
        }

        public void SetSeated(float seatedScale = 1f)
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }
            _seated = true;
            _seatReady = false;
            _front = false;
            _boosterSelectable = false;
            _queueTransitionPlaying = false;
            _queue = null;
            RefreshWhiteOutline();
            if (hitbox != null) hitbox.enabled = false;
            SetDemandBarVisible(false);

            _seatedBaseScale = Vector3.one * Mathf.Max(0.01f, seatedScale);
            transform.localScale = _seatedBaseScale;
            StopServePulse();
            RefreshCount();
        }

        public bool ServeOne()
        {
            if (Remaining <= 0) return true;
            // Never start a new pulse using the enlarged frame of an old pulse.
            StopServePulse();
            Remaining--;
            RefreshCount();
            RefreshDemandFill();
            _pulseRoutine = StartCoroutine(PulseRoutine());
            return Remaining <= 0;
        }

        public void PrepareForMerge()
        {
            // The jelly merge owns scale until its animation finishes.
            StopServePulse();
            _mergeOutlineHidden = true;
            RefreshWhiteOutline();
        }

        public void FinishMergeVisual()
        {
            _mergeOutlineHidden = false;
            RefreshWhiteOutline();
        }

        public void PrepareForExit()
        {
            StopServePulse();
            SetDemandBarVisible(false);
        }

        public void StopServePulse()
        {
            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }
            if (_seated) transform.localScale = _seatedBaseScale;
        }



        public void AnimateTo(
            Vector3 localPosition,
            Vector3 localScale,
            float duration = 0.16f,
            float scalePunch = 0f,
            bool playAdvanceTransition = false)
        {
            // A customer already moved into a seat must never receive queue movement again.
            if (_seated) return;
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _queueTransitionPlaying = playAdvanceTransition;
            if (playAdvanceTransition)
                PlayAnimation(advanceTransitionAnimation, false);
            _moveRoutine = StartCoroutine(
                MoveRoutine(localPosition, localScale, duration, scalePunch,
                    playAdvanceTransition));
        }

        public void AnimateToSeat(
            Vector3 localPosition,
            float duration,
            float arcHeight,
            float scalePunch,
            SeatFlightFeelSettings feel = null,
            int settledSortingOrder = 230)
        {
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _queueTransitionPlaying = false;
            _seatReady = false;
            if (blackOutline != null) blackOutline.SetActive(false);
            if (whiteOutline != null) whiteOutline.SetActive(false);
            // The demand bar belongs to the seated state: reveal it as soon as
            // the animal leaves CustomerQueue and starts its curved seat flight.
            SetDemandBarVisible(true);
            PlayAnimation(flyAnimation, false);
            _moveRoutine = StartCoroutine(SeatMoveRoutine(
                localPosition,
                _seatedBaseScale,
                duration,
                arcHeight,
                scalePunch,
                feel ?? new SeatFlightFeelSettings(),
                settledSortingOrder));
        }

        public void SetSortingOrder(int baseOrder)
        {
            if (body != null && body.TryGetComponent(out MeshRenderer bodyRenderer))
                bodyRenderer.sortingOrder = baseOrder;

            Canvas uiCanvas = countBadge != null
                ? countBadge.canvas
                : demandFill != null ? demandFill.canvas : null;
            if (uiCanvas != null)
            {
                uiCanvas.overrideSorting = true;
                uiCanvas.sortingOrder = baseOrder + 1;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_seated && _queue != null)
                _queue.HandleCustomerClick(this);
        }

        private void RefreshVisual()
        {
            SetDemandBarVisible(IsSeatReady);
            RefreshCount();
            RefreshDemandFill();
        }

        private void SetDemandBarVisible(bool visible)
        {
            Image barImage = countBadge != null ? countBadge : demandFill;
            if (barImage == null) return;

            // Fill and count are children of the Bar object. Toggle the complete
            // bar so it stays hidden in the queue and when the animal leaves.
            Transform bar = barImage.transform.parent;
            (bar != null ? bar.gameObject : barImage.gameObject).SetActive(visible);
        }

        private void RefreshCount()
        {
            if (count != null) count.text = Remaining.ToString();
        }

        private void RefreshDemandFill()
        {
            if (demandFill != null)
                demandFill.fillAmount = Mathf.Clamp01(Remaining / (float)ProgressCapacity);
        }

        private IEnumerator MoveRoutine(
            Vector3 target,
            Vector3 targetScale,
            float duration,
            float scalePunch,
            bool finishAdvanceTransition)
        {
            Vector3 start = transform.localPosition;
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < duration && !_seated)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - normalized, 3f);
                float punch = 1f + Mathf.Sin(normalized * Mathf.PI) * scalePunch;
                transform.localPosition = Vector3.LerpUnclamped(start, target, eased);
                transform.localScale =
                    Vector3.LerpUnclamped(startScale, targetScale, eased) * punch;
                yield return null;
            }
            if (!_seated)
            {
                transform.localPosition = target;
                transform.localScale = targetScale;
                if (finishAdvanceTransition)
                    PlayAnimationSequence(advanceAnimation, advanceAnimation2);
            }
            _queueTransitionPlaying = false;
            _moveRoutine = null;
        }

        private IEnumerator PulseRoutine()
        {
            Vector3 baseScale = _seatedBaseScale;
            float duration = 0.12f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI) * 0.12f;
                transform.localScale = baseScale * (1f + pulse);
                yield return null;
            }
            transform.localScale = baseScale;
            _pulseRoutine = null;
        }

        private IEnumerator SeatMoveRoutine(
            Vector3 target,
            Vector3 targetScale,
            float duration,
            float arcHeight,
            float scalePunch,
            SeatFlightFeelSettings feel,
            int settledSortingOrder)
        {
            Vector3 start = transform.localPosition;
            Vector3 startScale = transform.localScale;
            Quaternion startRotation = transform.localRotation;
            duration = Mathf.Max(0.01f, duration);
            float launchDuration = Mathf.Max(0f, feel.launchDuration);
            float landingDuration = Mathf.Max(0f, feel.landingDuration);
            float tiltDegrees = Mathf.Clamp(feel.tiltDegrees, 0f, 35f);
            float landingSquash = Mathf.Clamp(feel.landingSquash, 0f, 0.35f);
            float reboundHeight = Mathf.Max(0f, feel.reboundHeight);
            float horizontalDirection = Mathf.Abs(target.x - start.x) > 0.05f
                ? Mathf.Sign(target.x - start.x) : 0f;

            // Anticipation stays at the take-off point, then releases into the throw.
            float elapsed = 0f;
            while (elapsed < launchDuration)
            {
                float t = Mathf.Clamp01(elapsed / launchDuration);
                float squash = Mathf.Sin(t * Mathf.PI) *
                    Mathf.Clamp(feel.launchSquash, 0f, 0.35f);
                transform.localScale = Vector3.Scale(startScale,
                    new Vector3(1f + squash, 1f - squash, 1f));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            transform.localScale = startScale;

            // Place the apex above BOTH ends. Adding a fixed parabola to a lerp
            // can still be rising on arrival when the seat is much higher.
            float apexY = Mathf.Max(start.y, target.y) + Mathf.Max(0.01f, arcHeight);
            float rise = Mathf.Sqrt(apexY - start.y);
            float fall = Mathf.Sqrt(apexY - target.y);
            float heightSum = rise + fall;
            float initialVelocity = 2f * rise * heightSum;
            float gravity = 2f * heightSum * heightSum;
            float peakTime = rise / heightSum;
            float maxVerticalSpeed = Mathf.Max(initialVelocity, gravity - initialVelocity);

            elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 position = Vector3.LerpUnclamped(start, target, t);
                position.y = start.y + initialVelocity * t - 0.5f * gravity * t * t;
                transform.localPosition = position;

                // Grow near the apex and stretch in proportion to the actual speed.
                float progressToApex = t < peakTime
                    ? t / peakTime : (1f - t) / (1f - peakTime);
                float apexPunch = Mathf.SmoothStep(0f, 1f, progressToApex);
                Vector3 flightScale = Vector3.LerpUnclamped(startScale, targetScale, t) *
                                      (1f + apexPunch * Mathf.Max(0f, scalePunch));
                float verticalVelocity = initialVelocity - gravity * t;
                float envelope = Mathf.Sin(t * Mathf.PI);
                float stretch = Mathf.Abs(verticalVelocity) / maxVerticalSpeed * 0.10f * envelope;
                flightScale = Vector3.Scale(flightScale,
                    new Vector3(1f - stretch * 0.45f, 1f + stretch, 1f));
                transform.localScale = flightScale;

                float tilt = -horizontalDirection * tiltDegrees *
                             (verticalVelocity / maxVerticalSpeed) * envelope;
                float straighten = Mathf.SmoothStep(0f, 1f, t);
                transform.localRotation = Quaternion.Slerp(startRotation, Quaternion.identity, straighten) *
                                          Quaternion.Euler(0f, 0f, tilt);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            transform.localPosition = target;
            transform.localScale = targetScale;
            transform.localRotation = Quaternion.identity;

            // Impact happens at the seat, followed by one small, damped rebound.
            elapsed = 0f;
            while (elapsed < landingDuration)
            {
                float t = Mathf.Clamp01(elapsed / landingDuration);
                float squash;
                float bounce = 0f;
                float tilt = 0f;
                if (t < 0.22f)
                {
                    squash = landingSquash * Mathf.SmoothStep(0f, 1f, t / 0.22f);
                }
                else
                {
                    float settle = (t - 0.22f) / 0.78f;
                    float damping = (1f - settle) * (1f - settle);
                    float rebound = 4f * settle * (1f - settle);
                    bounce = reboundHeight * rebound * rebound;
                    squash = landingSquash * Mathf.Cos(settle * Mathf.PI * 2f) * damping;
                    tilt = horizontalDirection * tiltDegrees * 0.15f *
                           Mathf.Sin(settle * Mathf.PI * 2f) * damping;
                }

                transform.localPosition = target + Vector3.up * bounce;
                transform.localScale = Vector3.Scale(targetScale,
                    new Vector3(1f + squash, 1f - squash, 1f));
                transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            transform.localPosition = target;
            transform.localScale = targetScale;
            transform.localRotation = Quaternion.identity;
            SetSortingOrder(settledSortingOrder);
            _seatReady = true;
            RefreshWhiteOutline();
            PlayAnimation(seatAnimation, true);
            _moveRoutine = null;
        }

        private void PlayAnimation(string animationName, bool loop)
        {
            StopAnimationSequence();
            if (body == null || string.IsNullOrEmpty(animationName))
                return;

            Spine.Animation animation = body.SkeletonDataAsset
                .GetSkeletonData(true)
                .FindAnimation(animationName);
            if (animation == null) return;

            body.AnimationState.SetAnimation(
                0,
                animation,
                loop);
        }

        private void StopAnimationSequence()
        {
            ReleaseAnimation2();
            if (_animationSequence != null) StopCoroutine(_animationSequence);
            _animationSequence = null;
            _sequenceFirst = null;
            _sequenceSecond = null;
        }

        private void PlayAnimationSequence(string firstName, string secondName)
        {
            if (_animationSequence != null && _sequenceFirst == firstName &&
                _sequenceSecond == secondName) return;
            StopAnimationSequence();
            if (body == null || body.SkeletonDataAsset == null) return;
            body.Initialize(false);
            var data = body.SkeletonDataAsset.GetSkeletonData(true);
            if (data == null) return;
            var first = string.IsNullOrEmpty(firstName) ? null : data.FindAnimation(firstName);
            var second = string.IsNullOrEmpty(secondName) ? null : data.FindAnimation(secondName);
            if (first == null || second == null)
            {
                var available = first ?? second;
                if (available != null) body.AnimationState.SetAnimation(0, available, true);
                return;
            }
            _sequenceFirst = firstName;
            _sequenceSecond = secondName;
            _animationSequence = StartCoroutine(AnimationSequenceRoutine(first, second));
        }

        private IEnumerator AnimationSequenceRoutine(Spine.Animation first, Spine.Animation second)
        {
            while (body != null)
            {
                var idle = body.AnimationState.SetAnimation(0, first, true);
                float minimum = Mathf.Max(0.1f, animation2DelayRange.x);
                float maximum = Mathf.Max(minimum, animation2DelayRange.y);
                float remaining = Random.Range(minimum, maximum);
                while (remaining > 0f && body != null &&
                       body.AnimationState.GetCurrent(0) == idle)
                {
                    yield return null;
                    remaining -= Time.deltaTime;
                }
                if (body == null || body.AnimationState.GetCurrent(0) != idle) break;
                while (!Animation2Playing.Contains(this))
                {
                    TryStartAnimation2();
                    if (Animation2Playing.Contains(this)) break;
                    yield return null;
                    if (body == null || body.AnimationState.GetCurrent(0) != idle)
                    {
                        ReleaseAnimation2();
                        _animationSequence = null;
                        yield break;
                    }
                }
                var entry = body.AnimationState.SetAnimation(0, second, false);
                // Wait on Spine playback rather than wall time, respecting animation speed.
                do { yield return null; }
                while (body != null && body.AnimationState.GetCurrent(0) == entry &&
                       !entry.IsComplete);
                ReleaseAnimation2();
                if (body == null || body.AnimationState.GetCurrent(0) != entry) break;
            }
            ReleaseAnimation2();
            _animationSequence = null;
        }

        public void SetMergedRemaining(int remaining, int progressCapacity)
        {
            Remaining = Mathf.Max(0, remaining);
            _progressCapacity = Mathf.Max(Remaining, progressCapacity, 1);
            HasMerged = true;
            RefreshCount();
            RefreshDemandFill();
        }
    }
}
