using UnityEngine;

namespace CookingSort2D
{
    public class SeatSlotView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer background;
        public CustomerView Occupant { get; private set; }
        public bool IsEmpty => Occupant == null;

        private void Awake()
        {
            if (background == null) background = GetComponent<SpriteRenderer>();
            Refresh();
        }

        public void Attach(CustomerView customer, System.Action onLanded = null)
        {
            Occupant = customer;
            if (customer != null)
            {
                customer.transform.SetParent(transform, true);
                customer.transform.localRotation = Quaternion.identity;
                customer.SetSortingOrder(230);
                customer.SetSeated();
                customer.AnimateSeatJump(Vector3.zero, onLanded);
            }
            Refresh();
        }

        public CustomerView Release()
        {
            CustomerView old = Occupant;
            Occupant = null;
            Refresh();
            return old;
        }

        private void Refresh()
        {
            if (background == null) background = GetComponent<SpriteRenderer>();
            if (background != null)
                background.color = Occupant == null
                    ? new Color32(255, 244, 222, 220)
                    : new Color32(255, 255, 255, 245);
        }
    }
}
