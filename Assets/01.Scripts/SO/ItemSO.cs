using UnityEditor.Build;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/ItemSO")]
public class ItemSO : ScriptableObject
{
    public GameObject prefab;
    public Sprite sprite;
    public ItemType  itemType;
    public bool stackable;
    

    public enum ItemType
    {
        tool,
        item,
    }
}
