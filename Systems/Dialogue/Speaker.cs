using UnityEngine;

[CreateAssetMenu(fileName = "Speaker", menuName = "Unity Common/Dialogue/Speaker")]
public class Speaker : ScriptableObject
{
    public string   displayName;
    public string[] nameAlias;
    public Color    nameColor = Color.white;
    public Sprite   displaySprite;
    public Color    displaySpriteColor = Color.white;
    public Color    textColor = Color.white;
}
