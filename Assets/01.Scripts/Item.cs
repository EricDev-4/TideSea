using UnityEngine;

public class Item : MonoBehaviour
{
    public ItemSO itemSo;
    
    public bool PickUpItem()
    {
        if (InventoryManager.Instance == null ||
            !InventoryManager.Instance.AddItem(itemSo))
            return false;

        // Remove the world object only after inventory insertion succeeds.
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
}
