using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    #region Singletone

    private static InventoryManager instance;

    public static InventoryManager Instance
    {
        get
        {
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    #endregion

    public int maxStackedItems = 4;
    public InventorySlot[]  slots;
    public GameObject inventoryItemPrefab;

    private int selectedSlot = -1;

    [SerializeField] private PlayerBehavior _player;

    [System.Obsolete]
    private void Start()
    {
        if (_player == null)
            _player = FindFirstObjectByType<PlayerBehavior>();
        foreach (InventorySlot slot in slots)
            slot.Deselect();
        ChangeSelectedSlot(0);
    }

    private void Update()
    {
        for (int i = 0; i < Mathf.Min(slots.Length, 9); i++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
            {
                ChangeSelectedSlot(i);
                break;
            }
        }
    }

    public void ChangeSelectedSlot(int newValue)
    {
        if (newValue < 0 || newValue >= slots.Length)
            return;
        if(selectedSlot >= 0)
            slots[selectedSlot].Deselect();
        
        slots[newValue].Select();
        selectedSlot = newValue;
        RefreshHeldItem();
    }

    public ItemSO GetItem()
    {
        if (selectedSlot < 0 || selectedSlot >= slots.Length)
            return null;
        InventoryItem item = slots[selectedSlot].GetComponentInChildren<InventoryItem>();
        return item != null ? item._itemSO : null;
    }

    public void RefreshHeldItem()
    {
        if (_player != null)
            _player.EquipItem(GetItem());
    }
    
    public bool AddItem(ItemSO item)
    {
        if (item == null)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            InventorySlot slot = slots[i];
            InventoryItem inventoryItem = slot.GetComponentInChildren<InventoryItem>();
            if (inventoryItem != null &&
                inventoryItem._itemSO == item &&
                inventoryItem.count < maxStackedItems &&
                item.stackable
               )
            {
                inventoryItem.count++;
                inventoryItem.RefreshCount();
                ChangeSelectedSlot(i);
                return true;
            }
        }
        for (int i = 0; i < slots.Length; i++)
        {
            InventorySlot slot = slots[i];
            InventoryItem slotInItem = slot.GetComponentInChildren<InventoryItem>();
            if (slotInItem == null)
            {
                SpawnNewItem(item, slot);
                ChangeSelectedSlot(i);
                return true;
            }
        }
        return false;
    }

    public bool RemoveSelectedItem(out ItemSO item)
    {
        item = GetItem();
        if (item == null || item.prefab == null)
            return false;

        InventoryItem inventoryItem = slots[selectedSlot].GetComponentInChildren<InventoryItem>();
        inventoryItem.count--;
        if (inventoryItem.count > 0)
        {
            inventoryItem.RefreshCount();
        }
        else
        {
            // Destroy is deferred; detach now so GetItem sees an empty slot immediately.
            inventoryItem.transform.SetParent(null);
            inventoryItem.gameObject.SetActive(false);
            Destroy(inventoryItem.gameObject);
        }
        RefreshHeldItem();
        return true;
    }

    private void SpawnNewItem(ItemSO item , InventorySlot slot)
    {
        GameObject newItemGO = Instantiate(inventoryItemPrefab, slot.transform);
        InventoryItem inventoryItem = newItemGO.GetComponent<InventoryItem>();
        inventoryItem.InitItem(item);
        Debug.Log("SpawnItem " + item.prefab.name);
    }
}
