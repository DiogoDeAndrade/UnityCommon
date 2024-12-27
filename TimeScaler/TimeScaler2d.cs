using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeScaler2d : MonoBehaviour
{
    public float timeScale
    {
        get { return _timeScale; }
    }

    public float time
    {
        get { return _time; }
    }

    public float fixedTime
    {
        get { return _fixedTime; }
    }

    public float deltaTime
    {
        get { return Time.deltaTime * _timeScale; }
    }

    public float fixedDeltaTime
    {
        get { return Time.fixedDeltaTime * _timeScale; }
    }

    float _timeScale = 1.0f;
    float _time = 0.0f;
    float _fixedTime = 0.0f;

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
            return rb.linearVelocity / _timeScale;
        }
        set
        {
            rb.linearVelocity = value * _timeScale;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animators = GetComponentsInChildren<Animator>(true);
    }

    void Update()
    {
        _time += Time.deltaTime * _timeScale;
        _fixedTime += Time.fixedDeltaTime * _timeScale;
    }

    public void ModifyScale(float s)
    {
        float scaleDerivative = (_timeScale * s) / _timeScale;

        _timeScale *= s;

        rb.linearVelocity *= scaleDerivative;
        rb.angularVelocity *= scaleDerivative;
        rb.gravityScale *= scaleDerivative;
        rb.linearDamping *= scaleDerivative;
        rb.angularDamping *= scaleDerivative;
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

        rb.linearVelocity *= scaleDerivative;
        rb.angularVelocity *= scaleDerivative;
        rb.gravityScale *= scaleDerivative;
        rb.linearDamping *= scaleDerivative;
        rb.angularDamping *= scaleDerivative;
        rb.mass /= scaleDerivative;

        foreach (var anim in animators)
        {
            anim.speed = _timeScale;
        }
    }

    public void AddForce(Vector2 force, ForceMode2D mode)
    {
//        rb.AddForce(force * timeScale, mode);
        rb.AddForce(force, mode);
    }
}
