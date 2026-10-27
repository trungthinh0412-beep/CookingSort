using System;
using System.Collections;
using UnityEngine;

namespace CookingSort2D
{
    public class FoodTileView : MonoBehaviour
    {
        [Header("Matching")]
        [Tooltip("This Tile can only be eaten by an Animal with the same Match Type.")]
        [SerializeField] private FoodColor matchType = FoodColor.Blue;

        [SerializeField] private bool usePrefabSprite = false;
[SerializeField] private SpriteRenderer body;
        [SerializeField] private TextMesh symbol;

        [Header("Board Footprint")]
        [Tooltip("Number of logical Board cells occupied horizontally when placed from the Level Editor.")]
        [SerializeField, Min(1)] private int footprintWidth = 1;
        [Tooltip("Number of logical Board cells occupied vertically when placed from the Level Editor.")]
        [SerializeField, Min(1)] private int footprintHeight = 1;
        [Tooltip("How many servings this Tile contains. A normal 1 x 1 Tile uses 1.")]
        [SerializeField, Min(1)] private int footprintValue = 1;
        [Tooltip("Enable when this prefab uses a 1 x 1 sprite that should be stretched to its footprint. Disable when the sprite is already authored as 1 x 2, 2 x 1, 2 x 2, etc.")]
        [SerializeField] private bool stretchVisualToFootprint = true;

        [Header("Fly To Animal")]
        [Tooltip("Seconds for this Tile to fly into its Animal. Increase to fly slower; decrease to fly faster.")]
        [SerializeField, Min(0.01f)] private float consumeDuration = 0.22f;

        // FoodColor is kept as the serialized level ID for backwards compatibility.
        // In gameplay this value is a match type: a Tile can only be eaten by an
        // Animal whose Type has the same value.
        public FoodColor Type => matchType;
        public FoodColor ColorType => Type;
        public int FootprintWidth => Mathf.Max(1, footprintWidth);
        public int FootprintHeight => Mathf.Max(1, footprintHeight);
        public int FootprintValue => Mathf.Max(1, footprintValue);
        public bool UsesFootprint => FootprintWidth > 1 || FootprintHeight > 1 || FootprintValue > 1;

        public Vector2 GetVisualSize(Transform relativeTo)
        {
            AutoBind();
            if (body == null || body.sprite == null) return Vector2.zero;

            Vector3 worldSize = body.bounds.size;
            Vector3 relativeScale = relativeTo != null ? relativeTo.lossyScale : Vector3.one;
            float scaleX = Mathf.Max(0.0001f, Mathf.Abs(relativeScale.x));
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(relativeScale.y));
            return new Vector2(worldSize.x / scaleX, worldSize.y / scaleY);
        }

        public Vector2 GetVisualCenterOffset(Transform relativeTo)
        {
            AutoBind();
            if (body == null || relativeTo == null) return Vector2.zero;

            Vector3 visualCenter = relativeTo.InverseTransformPoint(body.bounds.center);
            Vector3 tileOrigin = relativeTo.InverseTransformPoint(transform.position);
            Vector3 offset = visualCenter - tileOrigin;
            return new Vector2(offset.x, offset.y);
        }

        private void Awake()
        {
            AutoBind();
        }

public void Setup(FoodColor color)
        {
            AutoBind();
            if (body != null)
                body.color = color == FoodColor.Neutral
                    ? CookingSortPalette.Get(color)
                    : usePrefabSprite ? Color.white : CookingSortPalette.Get(color);
            // Regular 1x1 tiles are artwork-only, without letters or numbers.
            // Footprints bigger than 1x1 explicitly enable their value label.
            if (symbol != null)
            {
                symbol.text = string.Empty;
            }
        }

public void SetupCluster(FoodColor color, int value, int width, int height)
        {
            Setup(color);
            // Keep prefab instance scale at (1,1,1); only stretch its visual.
            if (stretchVisualToFootprint)
                StretchFootprintVisual(width, height);

            // An explicitly placed 1x1 footprint must also have NO text.
            if (width <= 1 && height <= 1)
                return;

            if (symbol == null)
            {
                GameObject label = new GameObject("ClusterValue");
                label.transform.SetParent(transform, false);
                symbol = label.AddComponent<TextMesh>();
                symbol.anchor = TextAnchor.MiddleCenter;
                symbol.alignment = TextAlignment.Center;
                symbol.fontSize = 72;
                symbol.characterSize = 0.06f;
                symbol.fontStyle = FontStyle.Bold;
            }
            symbol.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            symbol.transform.localScale = Vector3.one;
            SetClusterValue(value);
        }

private void StretchFootprintVisual(int width, int height)
        {
            AutoBind();
            if (body == null) return;
            Vector3 footprintScale = new Vector3(
                Mathf.Max(1, width), Mathf.Max(1, height), 1f);

            if (body.transform == transform)
            {
                // The prefab has its SpriteRenderer on the root. Leave that
                // transform untouched and render a stretched child instead.
                SpriteRenderer source = body;
                GameObject visualObject = new GameObject("FootprintVisual");
                visualObject.transform.SetParent(transform, false);
                var visual = visualObject.AddComponent<SpriteRenderer>();
                visual.sprite = source.sprite;
                visual.sharedMaterial = source.sharedMaterial;
                visual.color = source.color;
                visual.flipX = source.flipX;
                visual.flipY = source.flipY;
                visual.sortingLayerID = source.sortingLayerID;
                visual.sortingOrder = source.sortingOrder;
                visual.maskInteraction = source.maskInteraction;
                visual.spriteSortPoint = source.spriteSortPoint;
                visualObject.transform.localScale = footprintScale;
                source.enabled = false;
                body = visual;
            }
            else
                body.transform.localScale =
                    Vector3.Scale(body.transform.localScale, footprintScale);
        }


        public void SetClusterValue(int value)
        {
            if (symbol == null) return;
            symbol.text = Mathf.Max(0, value).ToString();
            symbol.color = Color.white;
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
            float duration = Mathf.Max(0.01f, consumeDuration);
            float elapsed = 0f;

            Vector3 direction = end - start;
            Vector3 side = new Vector3(-direction.y, direction.x, 0f);
            if (side.sqrMagnitude > 0.0001f) side.Normalize();
            float sideSign = end.x >= start.x ? 1f : -1f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Zero velocity at both ends avoids a visible jerk when a Tile
                // changes from the vertical drop into its curved flight.
                float eased = t * t * (3f - 2f * t);
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
