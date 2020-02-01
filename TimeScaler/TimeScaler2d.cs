using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeScaler2d : MonoBehaviour
{
    public float timeScale
    {
        get { return _timeScale; }
    }

    float _timeScale = 1.0f;

    Rigidbody2D     rb;
    Animator[]      animators;

    public float originalGravityScale
    {
        get
        {
            return rb.gravityScale / _timeScale;
        }
        set
        {
            rb.gravityScale = value * _timeScale;
        }
    }

    public Vector2 originalVelocity
    {
        get
        {
            return rb.velocity / _timeScale;
        }
        set
        {
            rb.velocity = value * _timeScale;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animators = GetComponentsInChildren<Animator>(true);
    }

    private void Update()
    {
        if (rb.velocity.magnitude > 0.01f)
            Debug.Log(name + " velocity = " + rb.velocity);
    }

    public void ModifyScale(float s)
    {
        float scaleDerivative = (_timeScale * s) / _timeScale;

        _timeScale *= s;

        rb.velocity *= scaleDerivative;
        rb.angularVelocity *= scaleDerivative;
        rb.gravityScale *= scaleDerivative;
        rb.drag *= scaleDerivative;
        rb.angularDrag *= scaleDerivative;
        rb.mass /= scaleDerivative;

        foreach (var anim in animators)
        {
            anim.speed = _timeScale;
        }
    }

    public void SetScale(float s)
    {
        float scaleDerivative = s / _timeScale;

        _timeScale = s;

        rb.velocity *= scaleDerivative;
        rb.angularVelocity *= scaleDerivative;
        rb.gravityScale *= scaleDerivative;
        rb.drag *= scaleDerivative;
        rb.angularDrag *= scaleDerivative;
        rb.mass /= scaleDerivative;

        foreach (var anim in animators)
        {
            anim.speed = _timeScale;
        }
    }
}
