using NaughtyAttributes;
using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Unity Common/Data/Item")]
public class Item : ScriptableObject
{
    public string       displayName;
    public Color        displaySpriteColor;
    public Sprite       displaySprite;
    public Color        displayTextColor;
    public GameObject   scenePrefab;
    public bool         isStackable = false;
    [ShowIf(nameof(isStackable))]
    public int          maxStack = 1;
}
