using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class Tweener : MonoBehaviour
{
    public delegate float EaseFunction(float t);

    public class BaseInterpolator
    {
        public float        currentTime;
        public float        totalTime;
        public EaseFunction easeFunction;

        public bool isFinished => currentTime >= totalTime;

        public void Run(float elapsedTime)
        {
            currentTime += elapsedTime;
            float t = Mathf.Clamp01(currentTime / totalTime);
            t = easeFunction(t);
            EvaluateAndSet(t);
        }

        protected virtual void EvaluateAndSet(float t) {;}
    }

    class Interpolator<T> : BaseInterpolator 
    {
        public Action<T>       action;
        public T               startValue;
        public T               endValue;
    }

    class FloatInterpolator : Interpolator<float>
    {
        protected override void EvaluateAndSet(float t)
        {
            var deltaValue = endValue - startValue;
            var currentValue = (startValue + deltaValue * t);
            action?.Invoke(currentValue);
        }
    }

    class Vec2Interpolator : Interpolator<Vector2>
    {
        protected override void EvaluateAndSet(float t)
        {
            var deltaValue = endValue - startValue;
            var currentValue = (startValue + deltaValue * t);
            action?.Invoke(currentValue);
        }
    }

    class Vec3Interpolator : Interpolator<Vector3>
    {
        protected override void EvaluateAndSet(float t)
        {
            var deltaValue = endValue - startValue;
            var currentValue = (startValue + deltaValue * t);
            action?.Invoke(currentValue);
        }
    }

    class ColorInterpolator : Interpolator<Color>
    {
        protected override void EvaluateAndSet(float t)
        {
            var deltaValue = endValue - startValue;
            var currentValue = (startValue + deltaValue * t);
            action?.Invoke(currentValue);
        }
    }

    List<BaseInterpolator> interpolators = new();

    public BaseInterpolator Interpolate(float sourceValue, float targetValue, float time, Action<float> setAction)
    {
        interpolators.Add(new FloatInterpolator()
        {
            easeFunction = Linear,
            currentTime = 0.0f,
            totalTime = time,
            startValue = sourceValue,
            endValue = targetValue,
            action = setAction,
        });
        return interpolators[interpolators.Count - 1];
    }

    public BaseInterpolator Interpolate(Vector2 sourceValue, Vector2 targetValue, float time, Action<Vector2> setAction)
    {
        interpolators.Add(new Vec2Interpolator()
        {
            easeFunction = Linear,
            currentTime = 0.0f,
            totalTime = time,
            startValue = sourceValue,
            endValue = targetValue,
            action = setAction,
        });
        return interpolators[interpolators.Count - 1];
    }

    public BaseInterpolator Interpolate(Vector3 sourceValue, Vector3 targetValue, float time, Action<Vector3> setAction)
    {
        interpolators.Add(new Vec3Interpolator()
        {
            easeFunction = Linear,
            currentTime = 0.0f,
            totalTime = time,
            startValue = sourceValue,
            endValue = targetValue,
            action = setAction,
        });
        return interpolators[interpolators.Count - 1];
    }

    public BaseInterpolator Interpolate(Color sourceValue, Color targetValue, float time, Action<Color> setAction)
    {
        interpolators.Add(new ColorInterpolator()
        {
            easeFunction = Linear,
            currentTime = 0.0f,
            totalTime = time,
            startValue = sourceValue,
            endValue = targetValue,
            action = setAction,
        });
        return interpolators[interpolators.Count - 1];
    }

    static public float Linear(float t) => t;

    private void Update()
    {
        foreach (var interpolator in interpolators)
        {
            interpolator.Run(Time.deltaTime);
        }
        interpolators.RemoveAll((interpolator) => interpolator.isFinished);
    }
}

public static class TweenerExtension
{
    public static Tweener Tween(this GameObject go)
    {
        var tmp = go.GetComponent<Tweener>();
        if (tmp) return tmp;
        return go.AddComponent<Tweener>();
    }

    public static Tweener Tween(this Component go)
    {
        var tmp = go.GetComponent<Tweener>();
        if (tmp) return tmp;
        return go.gameObject.AddComponent<Tweener>();
    }
}
