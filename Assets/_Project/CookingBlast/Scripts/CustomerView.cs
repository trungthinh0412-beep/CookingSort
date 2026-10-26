using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CookingSort2D
{
    public class CustomerView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer shadow;
        [SerializeField] private TextMeshProUGUI count;
        [SerializeField] private Image countBadge;
        [SerializeField] private Collider2D hitbox;
        [SerializeField] private Image demandFill;
        [SerializeField, Min(1)] private int demand = 20;

        [Header("Seat Jump")]
        [SerializeField, Min(0.01f)] private float seatJumpDuration = 0.34f;
        [SerializeField, Min(0f)] private float seatJumpHeight = 0.55f;
        [SerializeField, Range(0f, 1f)] private float shadowScaleAtJumpPeak = 0.45f;

        private CustomerQueue _queue;
        private int _laneIndex = -1;
        private bool _front;
        private bool _seated;

        private Coroutine _pulseRoutine;
        private Vector3 _authoredScale = Vector3.one;
        private Vector3 _seatedBaseScale = Vector3.one;
        private Coroutine _moveRoutine;
        private Coroutine _seatJumpRoutine;
        private Vector3 _shadowBaseLocalPosition;
        private Vector3 _shadowBaseLocalScale = Vector3.one;

        public FoodColor ColorType { get; private set; }
        public int Remaining { get; private set; }
        public bool IsSeated => _seated;
        public bool IsFront => _front;
        public bool IsSeatJumping => _seatJumpRoutine != null;
        public int Demand => Mathf.Max(1, demand);

        private void Awake()
        {
            _authoredScale = transform.localScale;
            _seatedBaseScale = _authoredScale;
            if (shadow == null)
            {
                Transform shadowTransform = transform.Find("Shadow_Animal");
                if (shadowTransform != null)
                    shadow = shadowTransform.GetComponent<SpriteRenderer>();
            }
            if (shadow != null)
            {
                _shadowBaseLocalPosition = shadow.transform.localPosition;
                _shadowBaseLocalScale = shadow.transform.localScale;
            }
        }

        public void BindQueue(CustomerQueue queue, int laneIndex, CustomerOrderData order)
        {
            _queue = queue;
            _laneIndex = laneIndex;
            _seated = false;
            ColorType = order.color;
            Remaining = Demand;
            RefreshVisual();
            SetFront(false);
            transform.localScale = _authoredScale;
        }




        public void SetFront(bool front)
        {
            _front = front && !_seated;
            if (hitbox != null) hitbox.enabled = _front;
        }

        public void SetSeated()
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }
            _seated = true;
            _front = false;
            _queue = null;
            if (hitbox != null) hitbox.enabled = false;
            if (countBadge != null) countBadge.gameObject.SetActive(true);

            _seatedBaseScale = _authoredScale;
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
            CancelSeatJump();
            StopServePulse();
        }

        public void AnimateSeatJump(Vector3 targetLocalPosition, System.Action onLanded)
        {
            CancelSeatJump();
            _seatJumpRoutine = StartCoroutine(
                SeatJumpRoutine(targetLocalPosition, onLanded));
        }

        public void CancelSeatJump()
        {
            if (_seatJumpRoutine != null)
            {
                StopCoroutine(_seatJumpRoutine);
                _seatJumpRoutine = null;
            }
            RestoreShadow();
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



        public void AnimateTo(Vector3 localPosition, float duration = 0.16f)
        {
            // A customer already moved into a seat must never receive queue movement again.
            if (_seated) return;
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _moveRoutine = StartCoroutine(MoveRoutine(localPosition, duration));
        }

        public void SetSortingOrder(int baseOrder)
        {
            if (body != null) body.sortingOrder = baseOrder;
            if (shadow != null) shadow.sortingOrder = baseOrder - 1;

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

        private void RefreshVisual()
        {
            if (countBadge != null) countBadge.gameObject.SetActive(_seated);
            RefreshCount();
            RefreshDemandFill();
        }

        private void RefreshCount()
        {
            if (count != null) count.text = Remaining.ToString();
        }

        private void RefreshDemandFill()
        {
            if (demandFill != null)
                demandFill.fillAmount = Mathf.Clamp01(Remaining / (float)Demand);
        }

        private IEnumerator MoveRoutine(Vector3 target, float duration)
        {
            Vector3 start = transform.localPosition;
            float elapsed = 0f;
            while (elapsed < duration && !_seated)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                transform.localPosition = Vector3.LerpUnclamped(start, target, t);
                yield return null;
            }
            if (!_seated) transform.localPosition = target;
            _moveRoutine = null;
        }

        private IEnumerator SeatJumpRoutine(
            Vector3 targetLocalPosition,
            System.Action onLanded)
        {
            Vector3 startLocalPosition = transform.localPosition;
            float duration = Mathf.Max(0.01f, seatJumpDuration);
            float height = Mathf.Max(0f, seatJumpHeight);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                float height01 = Mathf.Sin(t * Mathf.PI);
                float lift = height * height01;

                transform.localPosition = Vector3.LerpUnclamped(
                    startLocalPosition,
                    targetLocalPosition,
                    eased) + Vector3.up * lift;

                if (shadow != null)
                {
                    float scaleY = Mathf.Max(0.001f, Mathf.Abs(transform.localScale.y));
                    shadow.transform.localPosition =
                        _shadowBaseLocalPosition + Vector3.down * (lift / scaleY);
                    float shadowScale = Mathf.Lerp(
                        1f,
                        Mathf.Clamp01(shadowScaleAtJumpPeak),
                        height01);
                    shadow.transform.localScale =
                        _shadowBaseLocalScale * shadowScale;
                }

                yield return null;
            }

            transform.localPosition = targetLocalPosition;
            _seatJumpRoutine = null;
            RestoreShadow();
            onLanded?.Invoke();
        }

        private void RestoreShadow()
        {
            if (shadow == null) return;
            shadow.transform.localPosition = _shadowBaseLocalPosition;
            shadow.transform.localScale = _shadowBaseLocalScale;
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

        public void SetRemaining(int remaining)
        {
            Remaining = Mathf.Max(0, remaining);
            RefreshCount();
            RefreshDemandFill();
        }
    }
}
