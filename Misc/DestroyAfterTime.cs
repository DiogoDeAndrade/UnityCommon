using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    public float time = 10.0f;
    public bool fadeOut = false;

    SpriteRenderer spriteRenderer;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            fadeOut = false;
        }
    }

    void Update()
    {
        if (time > 0)
        {
            time -= Time.deltaTime;

            if (time <= 0)
            {
                if (fadeOut)
                {
                    StartCoroutine(FadeOutCR());
                }
                else
                { 
                    Destroy(gameObject);
                }
            }
        }
    }

    IEnumerator FadeOutCR()
    {
        while (spriteRenderer.color.a > 0)
        {
            spriteRenderer.color = spriteRenderer.color.MoveTowards(spriteRenderer.color.ChangeAlpha(0.0f), 2.0f * Time.deltaTime);
            yield return null;
        }

        Destroy(gameObject);
    }
}
