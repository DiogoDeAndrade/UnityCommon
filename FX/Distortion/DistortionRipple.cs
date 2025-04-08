using UC;
using UnityEngine;

public class DistortionRipple : MonoBehaviour
{
    private float           duration;
    private SpriteRenderer  spriteRenderer;
    private Material        material;
    private float           baseStrenght;
    private float           time;
    private AnimationCurve  sizeCurve;
    private AnimationCurve  strengthCurve;
    private bool            strengthIsSpriteAlpha;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        material = spriteRenderer.material;
        UpdateRipple();
    }
    
    void Update()
    {
        time += Time.deltaTime;

        UpdateRipple();
    }


    void UpdateRipple()
    {
        if (time > duration)
        {
            Destroy(gameObject);
        }
        else
        {
            transform.localScale = GetSize(time);
            if ((strengthCurve != null) && (strengthCurve.length > 0))
            {
                float s = strengthCurve.Evaluate(time / duration) * baseStrenght;
                if (strengthIsSpriteAlpha)
                {
                    spriteRenderer.color = spriteRenderer.color.ChangeAlpha(s);
                }
                else
                {
                    material.SetFloat("_Strength", s);
                }
            }
        }
    }

    public void SetParams(float duration, AnimationCurve sizeCurve, float baseStrenght, AnimationCurve strengthCurve, bool strengthIsSpriteAlpha)
    {
        this.duration = duration;
        this.sizeCurve = sizeCurve;
        this.baseStrenght = baseStrenght;
        this.strengthCurve = strengthCurve;
        this.strengthIsSpriteAlpha = strengthIsSpriteAlpha;
    }

    Vector3 GetSize(float t)
    {
        if ((sizeCurve != null) && (sizeCurve.length > 0))
        {
            float s = sizeCurve.Evaluate(t / duration);
            return new Vector3(s, s, s);
        }

        return Vector3.one;
    }
}
