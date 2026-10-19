using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CookingSort2D
{
    public class CustomerView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private TextMesh face;
        [SerializeField] private TextMesh count;
        [SerializeField] private SpriteRenderer countBadge;
        [SerializeField] private Collider2D hitbox;

        private CustomerQueue _queue;
        private int _laneIndex = -1;
        private bool _front;
        private bool _seated;
        
        private Coroutine _pulseRoutine;
private Coroutine _moveRoutine;

        public FoodColor ColorType { get; private set; }
        public int Remaining { get; private set; }
        public bool IsSeated => _seated;
        public bool IsFront => _front;

        private void Awake()
        {
            AutoBind();
        }

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
            AutoBind();
            _front = front && !_seated;
            if (body != null)
            {
                Color c = CookingSortPalette.Get(ColorType);
                c.a = _front ? 1f : 0.72f;
                body.color = c;
            }
            if (hitbox != null) hitbox.enabled = _front;
            transform.localScale = _front ? Vector3.one * 1.04f : Vector3.one * 0.94f;
        }

public void SetSeated()
        {
            AutoBind();
            // A queued customer may still be sliding forward when the player taps rapidly.
            // Stop that queue-local tween before the object changes parent to a seat.
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }
            _seated = true;
            _front = false;
            _queue = null;
            if (body != null) body.color = CookingSortPalette.Get(ColorType);
            if (hitbox != null) hitbox.enabled = false;
            if (countBadge != null) countBadge.gameObject.SetActive(true);
            transform.localScale = Vector3.one;
            RefreshCount();
        }

public bool ServeOne()
        {
            if (Remaining <= 0) return true;
            Remaining--;
            RefreshCount();
            if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
            _pulseRoutine = StartCoroutine(PulseRoutine());
            return Remaining <= 0;
        }

public void PrepareForMerge()
        {
            // A tile's serving pulse may still be running when its flight ends.
            // Hand scale control entirely to the jelly merge animation.
            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }
            transform.localScale = Vector3.one;
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
            AutoBind();
            if (body != null) body.sortingOrder = baseOrder;
            if (face != null)
            {
                MeshRenderer r = face.GetComponent<MeshRenderer>();
                if (r != null) r.sortingOrder = baseOrder + 1;
            }
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
            AutoBind();
            if (body != null) body.color = CookingSortPalette.Get(ColorType);
            if (face != null)
            {
                face.text = CookingSortPalette.ShortName(ColorType);
                face.color = Color.white;
            }
            if (countBadge != null) countBadge.gameObject.SetActive(_seated);
            RefreshCount();
        }

        private void RefreshCount()
        {
            AutoBind();
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
            Vector3 baseScale = transform.localScale;
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

        private void AutoBind()
        {
            if (body == null) body = GetComponent<SpriteRenderer>();
            if (hitbox == null) hitbox = GetComponent<Collider2D>();
            if (face == null)
            {
                Transform t = transform.Find("Face");
                if (t != null) face = t.GetComponent<TextMesh>();
            }
            if (countBadge == null)
            {
                Transform t = transform.Find("CountBadge");
                if (t != null) countBadge = t.GetComponent<SpriteRenderer>();
            }
            if (count == null)
            {
                Transform t = transform.Find("CountBadge/Count");
                if (t != null) count = t.GetComponent<TextMesh>();
            }
        }
    

public void SetRemaining(int remaining)
        {
            Remaining = Mathf.Max(0, remaining);
            RefreshCount();
        }
}
}
