using UnityEngine;

namespace CookingSort2D
{
    [DisallowMultipleComponent]
    public class CustomerQueueSlotView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer renderer2D;

        public Vector3 LocalAnchor => transform.localPosition;

        private void Awake()
        {
            if (renderer2D == null) renderer2D = GetComponent<SpriteRenderer>();
        }

        public void SetActiveSlot(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}
