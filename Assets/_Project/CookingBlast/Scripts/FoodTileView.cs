using System;
using System.Collections;
using UnityEngine;

namespace CookingSort2D
{
    public class FoodTileView : MonoBehaviour
    {
        
        [SerializeField] private bool usePrefabSprite = false;
[SerializeField] private SpriteRenderer body;
        [SerializeField] private TextMesh symbol;

        public FoodColor ColorType { get; private set; }

        private void Awake()
        {
            AutoBind();
        }

        public void Setup(FoodColor color)
        {
            ColorType = color;
            AutoBind();
            if (body != null) body.color = usePrefabSprite ? Color.white : CookingSortPalette.Get(color);
            if (symbol != null)
            {
                symbol.text = CookingSortPalette.ShortName(color);
                symbol.color = color == FoodColor.White
                    ? new Color32(90, 98, 108, 210)
                    : new Color32(255, 255, 255, 210);
            }
        }

public void FitToCell(float targetSize)
        {
            AutoBind();
            if (body == null || body.sprite == null || targetSize <= 0f) return;
            Vector3 dimensions = body.sprite.bounds.size;
            float longest = Mathf.Max(dimensions.x, dimensions.y);
            if (longest <= 0.0001f) return;
            // Keep the artwork's proportions, including shadows/highlights.
            transform.localScale = Vector3.one * (targetSize / longest);
        }


        public void SetSortingOrder(int order)
        {
            AutoBind();
            if (body != null) body.sortingOrder = order;
            if (symbol != null)
            {
                MeshRenderer r = symbol.GetComponent<MeshRenderer>();
                if (r != null) r.sortingOrder = order + 1;
            }
        }

public void ConsumeTo(Transform target, Action onComplete)
        {
            if (!isActiveAndEnabled || target == null)
            {
                onComplete?.Invoke();
                return;
            }
            StartCoroutine(ConsumeRoutine(target, onComplete));
        }

private IEnumerator ConsumeRoutine(Transform target, Action onComplete)
        {
            AutoBind();
            int oldOrder = body != null ? body.sortingOrder : 0;
            SetSortingOrder(100);

            Vector3 start = transform.position;
            Vector3 end = target.position;
            Vector3 startScale = transform.localScale;
            const float duration = 0.22f;
            float elapsed = 0f;

            Vector3 direction = end - start;
            Vector3 side = new Vector3(-direction.y, direction.x, 0f);
            if (side.sqrMagnitude > 0.0001f) side.Normalize();
            float sideSign = end.x >= start.x ? 1f : -1f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                Vector3 curve = side * sideSign * Mathf.Sin(t * Mathf.PI) * 0.12f;
                transform.position = Vector3.LerpUnclamped(start, end, eased) + curve;
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.5f, eased);
                yield return null;
            }

            transform.position = end;
            transform.localScale = startScale * 0.5f;
            SetSortingOrder(oldOrder);
            onComplete?.Invoke();
        }

        private void AutoBind()
        {
            if (body == null) body = GetComponent<SpriteRenderer>();
            if (symbol == null)
            {
                Transform t = transform.Find("Symbol");
                if (t != null) symbol = t.GetComponent<TextMesh>();
            }
        }
    }
}
