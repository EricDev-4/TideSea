using UnityEngine;

public class PlayerBehavior : MonoBehaviour
{
    private Ray _ray;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LayerMask layersToHit;
    [SerializeField] private float maxDistance;
    [SerializeField] private Transform grabPos;

    private GameObject target;

    public ItemSO currentItem;
    
    private void Update()
    {
        if (playerCamera != null && Input.GetMouseButtonDown(0))
        {
            _ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            if (Physics.Raycast(_ray, out RaycastHit hit, maxDistance, layersToHit))
            {
                Item item = hit.collider.GetComponentInParent<Item>();
                if (item != null)
                    item.PickUpItem();
            }
        }
        if (Input.GetMouseButtonDown(1))
            DropCurrentItem();
    }

    public void EquipItem(ItemSO item)
    {
        if (item == currentItem && (item == null || target != null))
            return;

        if (target != null)
        {
            target.SetActive(false);
            Destroy(target);
            target = null;
        }
        currentItem = null;

        if (item == null || item.prefab == null || grabPos == null)
            return;

        target = Instantiate(item.prefab, grabPos);
        target.transform.localPosition = Vector3.zero;
        target.transform.localRotation = Quaternion.identity;
        currentItem = item;

        // The held model is a visual copy; only dropped/world items use physics.
        foreach (Rigidbody rb in target.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }
        foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Item pickup in target.GetComponentsInChildren<Item>(true))
            pickup.enabled = false;
        foreach (Transform child in target.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
    }

    public void DropCurrentItem()
    {
        if (target == null || InventoryManager.Instance == null)
            return;

        Vector3 position = target.transform.position;
        Quaternion rotation = target.transform.rotation;
        if (InventoryManager.Instance.RemoveSelectedItem(out ItemSO item))
            Instantiate(item.prefab, position, rotation);
    }
}
