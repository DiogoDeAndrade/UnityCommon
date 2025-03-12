using NaughtyAttributes;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceType", menuName = "Unity Common/Data/Resource Type")]
public class ResourceType : ScriptableObject
{
    public string       displayName;
    public Color        displaySpriteColor = Color.white;
    public Sprite       displaySprite;
    public Color        displayTextColor = Color.white;
    public Color        displayBarColor = Color.white;
    public float        defaultValue = 100.0f;
    public float        maxValue = 100.0f;
}
