using UC;
using System.Collections.Generic;
using UnityEngine;

public class DistortionRipple : MonoBehaviour
{
    private float                   duration;
    private SpriteRenderer          spriteRenderer;
    private float                   baseStrenght;
    private float                   time;
    private AnimationCurve          sizeCurve;
    private AnimationCurve          strengthCurve;
    private bool                    strengthIsSpriteAlpha;
    private bool                    _active = true;
    private MaterialPropertyBlock   mpb;
    
    private static readonly int StrengthID = Shader.PropertyToID("_Strength");

    public bool isActive => _active;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (mpb == null)
        {
            mpb = new MaterialPropertyBlock();
        }

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
            ReturnToPool(this);
            return;
        }
        else
        {
            transform.localScale = GetSize(time);
            if ((strengthCurve != null) && (strengthCurve.length > 0))
            {
                float normalizedTime = duration > 0.0f ? time / duration : 1.0f;

                float s = strengthCurve.Evaluate(normalizedTime) * baseStrenght;
                if (strengthIsSpriteAlpha)
                {
                    spriteRenderer.color = spriteRenderer.color.ChangeAlpha(s);
                }
                else
                {
                    spriteRenderer.GetPropertyBlock(mpb);
                    mpb.SetFloat(StrengthID, s);
                    spriteRenderer.SetPropertyBlock(mpb);
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
        time = 0.0f;
        _active = true;

        if (spriteRenderer)
        {
            spriteRenderer.color = spriteRenderer.color.ChangeAlpha(1.0f);

            if (mpb == null)
                mpb = new MaterialPropertyBlock();

            spriteRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(StrengthID, 0.0f);
            spriteRenderer.SetPropertyBlock(mpb);
        }
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

    static List<DistortionRipple> ripplesPool = new();

    static public DistortionRipple Spawn(Transform parent, bool localSpace, bool turnToMovement, int rippleLayer, Vector2 dir, 
                                         Sprite rippleSprite, int spriteSortingLayer, int spriteOrderInLayer, Material rippleMaterial,
                                         float duration, AnimationCurve sizeCurve, float baseStrenght, AnimationCurve strengthCurve, bool strengthIsSpriteAlpha)
    {
        DistortionRipple newElem = null;

        if (ripplesPool.Count > 0)
        {
            newElem = ripplesPool.PopLast();
            newElem.spriteRenderer.sprite = rippleSprite;
            newElem.spriteRenderer.sortingLayerID = spriteSortingLayer;
            newElem.spriteRenderer.sortingOrder = spriteOrderInLayer;
            newElem.spriteRenderer.sharedMaterial = rippleMaterial;
            newElem.gameObject.SetActive(true);
        }
        else
        {
            GameObject go = new GameObject();
            go.name = "Ripple";

            go.layer = rippleLayer;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = rippleSprite;
            sr.sortingLayerID = spriteSortingLayer;
            sr.sortingOrder = spriteOrderInLayer;
            sr.sharedMaterial = rippleMaterial;

            newElem = go.AddComponent<DistortionRipple>();
            newElem.spriteRenderer = sr;
        }

        if (localSpace)
        {
            newElem.transform.SetParent(parent, false);
            newElem.transform.localPosition = Vector3.zero;
            if (turnToMovement)
            {
                newElem.transform.localRotation = Quaternion.LookRotation(Vector3.forward, dir.Perpendicular());
            }
            else
            {
                newElem.transform.localRotation = Quaternion.identity;
            }
        }
        else
        {
            newElem.transform.position = parent.position;
            if (turnToMovement)
            {
                newElem.transform.rotation = Quaternion.LookRotation(Vector3.forward, dir.Perpendicular());
            }
            else
            {
                newElem.transform.rotation = parent.rotation;
            }
        }
        newElem.SetParams(duration, sizeCurve, baseStrenght, strengthCurve, strengthIsSpriteAlpha);
        return newElem;

    }

    static public void ReturnToPool(DistortionRipple ripple)
    {
        ripple.gameObject.SetActive(false);
        ripple._active = false;
        ripple.transform.SetParent(null);
        ripplesPool.Add(ripple);
    }
}
