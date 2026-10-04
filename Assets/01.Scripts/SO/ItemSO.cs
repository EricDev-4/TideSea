using UnityEditor.Build;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/ItemSO")]
public class ItemSO : ScriptableObject
{
    public GameObject prefab;
    public Sprite sprite;
    public ItemType  itemType;
    public bool stackable;
    public Vector3 localScale;
    

    public enum ItemType
    {
        tool = 0,      // 채집 도구
        item = 1,      // 일반 아이템
        material = 2,  // 제작 재료
        food = 4,      // 음식
    }
}
