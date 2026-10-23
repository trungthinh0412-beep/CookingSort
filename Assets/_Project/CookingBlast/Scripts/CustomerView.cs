using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CookingSort2D
{
    public class CustomerView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private TextMesh count;
        [SerializeField] private SpriteRenderer countBadge;
        [SerializeField] private Collider2D hitbox;

        private CustomerQueue _queue;
        private int _laneIndex = -1;
        private bool _front;
        private bool _seated;

        private Coroutine _pulseRoutine;
        private Vector3 _seatedBaseScale = Vector3.one;
        private Coroutine _moveRoutine;

        public FoodColor ColorType { get; private set; }
        public int Remaining { get; private set; }
        public bool IsSeated => _seated;
        public bool IsFront => _front;

        public void BindQueue(CustomerQueue queue, int laneIndex, CustomerOrderData order)
        {
            _queue = queue;
            _laneIndex = laneIndex;
            _seated = false;
            ColorType = order.color;
            Remaining = Mathf.Max(1, order.demand);
            RefreshVisual();
            SetFront(false);
            transform.localScale = Vector3.one;
        }




        public void SetFront(bool front)
        {
            _front = front && !_seated;
            if (hitbox != null) hitbox.enabled = _front;
            transform.localScale = Vector3.one;
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

            _seatedBaseScale = Vector3.one;
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
            _pulseRoutine = StartCoroutine(PulseRoutine());
            return Remaining <= 0;
        }

        public void PrepareForMerge()
        {
            // The jelly merge owns scale until its animation finishes.
            StopServePulse();
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
            if (countBadge != null) countBadge.sortingOrder = baseOrder + 2;
            if (count != null)
            {
                MeshRenderer r = count.GetComponent<MeshRenderer>();
                if (r != null) r.sortingOrder = baseOrder + 3;
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
        }

        private void RefreshCount()
        {
            if (count != null) count.text = Remaining.ToString();
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
        }
    }
}
