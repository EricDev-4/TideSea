using System;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryItem : MonoBehaviour , IBeginDragHandler, IDragHandler , IEndDragHandler
{
    public Transform originParent;
    [SerializeField] private Image _image;
    public ItemSO _itemSO;

    public int count = 1;
    public TMP_Text  countText;

    public void InitItem(ItemSO newItem)
    {
        _itemSO = newItem;
        _image.sprite = newItem.sprite;
        RefreshCount();
    }

    public void RefreshCount()
    {
        countText.text = count.ToString();
        bool textActive = count > 1;
        countText.gameObject.SetActive(textActive);
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        _image.raycastTarget = false;
        originParent = transform.parent;
        transform.SetParent(transform.root);
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _image.raycastTarget = true;
        transform.SetParent(originParent);
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.RefreshHeldItem();
    }
}
