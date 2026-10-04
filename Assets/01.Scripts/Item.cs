using UnityEngine;

public class Item : MonoBehaviour
{
    public ItemSO itemSo;
    
    public bool PickUpItem()
    {
        if (InventoryManager.Instance == null)
        {
            return false;
        }

        bool itemAdded = InventoryManager.Instance.AddItem(itemSo);
        if (itemAdded == false)
        {
            return false;
        }

        // Remove the world object only after inventory insertion succeeds.
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
}
