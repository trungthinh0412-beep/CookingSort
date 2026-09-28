using UnityEngine;

public class WinArrowItem : MonoBehaviour
{
    public WinBonusArea currentWinBonusArea;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("BonusArea"))
        {
            var otherItem = other.GetComponent<WinBonusArea>();
            otherItem.ScaleUp();
            if (currentWinBonusArea)
            {
                if (currentWinBonusArea != otherItem)
                {
                    currentWinBonusArea.ChangeColor(false);
                    currentWinBonusArea = otherItem;
                    currentWinBonusArea.ChangeColor(true);
                }
            }
            else
            {
                currentWinBonusArea = otherItem;
                currentWinBonusArea.ChangeColor(true);
            }
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("BonusArea"))
        {
            var otherItem = collision.GetComponent<WinBonusArea>();
            otherItem.ScaleDown();
        }
    }
}
