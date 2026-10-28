using System.Collections;
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

        [SerializeField] private SpriteRenderer body;
        [SerializeField] private Sprite eggSprite;
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
        private int _progressCapacity;
        private bool _eggHidden;
        private Sprite _animalSprite;
        private SpriteAnimator _spriteAnimator;
        private bool _resumeAnimatorAfterEgg;

        private Coroutine _pulseRoutine;
        private Vector3 _seatedBaseScale = Vector3.one;
        private Coroutine _moveRoutine;

        // FoodColor is the legacy serialized name. Runtime matching is by Type.
        public FoodColor Type => matchType;
        public FoodColor ColorType => Type;
        public int Remaining { get; private set; }
        public int ProgressCapacity => Mathf.Max(1, _progressCapacity);
        public bool IsSeated => _seated;
        public bool IsSeatReady => _seated && _seatReady;
        public bool IsFront => _front;
        public bool IsEggHidden => _eggHidden;
        public int Demand => Mathf.Max(1, demand);

        public void BindQueue(CustomerQueue queue, int laneIndex, CustomerOrderData order)
        {
            _queue = queue;
            _laneIndex = laneIndex;
            _seated = false;
            _seatReady = false;
            _animalSprite = body != null ? body.sprite : null;
            _spriteAnimator = GetComponent<SpriteAnimator>();
            Sprite mysterySprite = eggSprite != null
                ? eggSprite
                : queue != null ? queue.EggSprite : null;
            _eggHidden = order != null && order.isEgg && queue != null &&
                         mysterySprite != null;
            _resumeAnimatorAfterEgg = _eggHidden && _spriteAnimator != null &&
                                      _spriteAnimator.enabled;
            if (_eggHidden)
            {
                // Animal variants animate this same SpriteRenderer every frame.
                // Pause that writer before applying the Egg sprite, otherwise
                // it immediately replaces EGG_Mystic with an animal frame.
                if (_resumeAnimatorAfterEgg)
                    _spriteAnimator.enabled = false;
                if (body != null)
                    body.sprite = mysterySprite;
            }
            Remaining = Demand;
            _progressCapacity = Demand;
            RefreshVisual();
            SetFront(false);
            transform.localScale = Vector3.one;
        }




        public void SetFront(bool front)
        {
            _front = front && !_seated;
            if (hitbox != null) hitbox.enabled = _front;
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
            _queue = null;
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
            float scalePunch = 0f)
        {
            // A customer already moved into a seat must never receive queue movement again.
            if (_seated) return;
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _moveRoutine = StartCoroutine(
                MoveRoutine(localPosition, localScale, duration, scalePunch));
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
            _seatReady = false;
            // The demand bar belongs to the seated state: reveal it as soon as
            // the animal leaves CustomerQueue and starts its curved seat flight.
            SetDemandBarVisible(true);
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
            if (body != null) body.sortingOrder = baseOrder;

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
            if (_front && !_seated && _queue != null)
                _queue.TrySelect(this);
        }

        /// <summary>
        /// Reveals the configured animal without selecting it. The first click
        /// opens the Egg; a later click follows the normal seat flow.
        /// </summary>
        public bool RevealEgg()
        {
            if (!_eggHidden) return false;
            _eggHidden = false;
            if (body != null && _animalSprite != null)
                body.sprite = _animalSprite;
            if (_resumeAnimatorAfterEgg && _spriteAnimator != null)
            {
                _spriteAnimator.enabled = true;
                _resumeAnimatorAfterEgg = false;
            }
            return true;
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
            float scalePunch)
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
            }
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
                float squash = Mathf.Sin(t * Mathf.PI) * landingSquash * 0.55f;
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
            _moveRoutine = null;
        }

        public void SetMergedRemaining(int remaining, int progressCapacity)
        {
            Remaining = Mathf.Max(0, remaining);
            _progressCapacity = Mathf.Max(Remaining, progressCapacity, 1);
            RefreshCount();
            RefreshDemandFill();
        }
    }
}
