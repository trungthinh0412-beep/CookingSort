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

        public void Attach(
            CustomerView customer,
            float seatedScale,
            float flyDuration,
            float arcHeight,
            float scalePunch,
            SeatFlightFeelSettings flightFeel = null)
        {
            Occupant = customer;
            if (customer != null)
            {
                const int seatedSortingOrder = 230;
                const int flightSortingBoost = 10;
                Vector3 startScale = customer.transform.localScale;
                customer.transform.SetParent(transform, true);
                customer.transform.localRotation = Quaternion.identity;
                customer.SetSortingOrder(seatedSortingOrder + flightSortingBoost);
                customer.SetSeated(seatedScale);
                customer.transform.localScale = startScale;
                customer.AnimateToSeat(
                    Vector3.zero,
                    flyDuration,
                    arcHeight,
                    scalePunch,
                    flightFeel,
                    seatedSortingOrder);
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
