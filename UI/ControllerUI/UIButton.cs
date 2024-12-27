using UnityEngine;

public class UIButton : BaseUIControl
{
    public override void Interact()
    {
        if (changeSnd) SoundManager.PlaySound(SoundType.SecondaryFX, changeSnd);
        NotifyInteract();
    }
}
