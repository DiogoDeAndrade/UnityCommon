using NaughtyAttributes;
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

    public AudioClip characterSnd;
    [MinMaxSlider(0.0f, 1.0f), ShowIf(nameof(hasCharacterSnd))]
    public Vector2 characterSndVolume = Vector2.one;
    [MinMaxSlider(0.1f, 2.0f), ShowIf(nameof(hasCharacterSnd))]
    public Vector2 characterSndPitch = Vector2.one;

    bool hasCharacterSnd => characterSnd != null;
}
