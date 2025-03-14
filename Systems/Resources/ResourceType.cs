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
    public bool         useCombatText;
    [ShowIf(nameof(useCombatText))]
    public string       ctBaseText = "Resource {value}";
    [ShowIf(nameof(useCombatText))]
    public Color        ctPositiveColor = Color.white;
    [ShowIf(nameof(useCombatText))]
    public Color        ctNegativeColor = Color.white;
}
