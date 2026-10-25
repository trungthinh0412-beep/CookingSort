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

        public void Attach(CustomerView customer)
        {
            Occupant = customer;
            if (customer != null)
            {
                customer.transform.SetParent(transform, false);
                customer.transform.localPosition = Vector3.zero;
                customer.transform.localRotation = Quaternion.identity;
                customer.transform.localScale = Vector3.one * 0.9f;
                customer.SetSortingOrder(230);
                customer.SetSeated();
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
