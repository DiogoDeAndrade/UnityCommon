using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class Tweener : MonoBehaviour
{
    public delegate float EaseFunction(float t);

    public class BaseInterpolator
    {
        public string           name;
        public float            currentTime;
        public float            totalTime;
        public EaseFunction     easeFunction;
        public Action           doneAction;

        public bool isFinished => currentTime >= totalTime;

        public void Run(float elapsedTime)
        {
            currentTime += elapsedTime;
            float t = Mathf.Clamp01(currentTime / totalTime);
            t = easeFunction(t);
            EvaluateAndSet(t);
        }

        internal virtual void EvaluateAndSet(float t) {;}

        internal BaseInterpolator Done(Action action)
        {
            doneAction = action;
            return this;
        }
    }

    class Interpolator<T> : BaseInterpolator 
    {
        public Action<T>       action;
        public T               startValue;
        public T               endValue;
    }

    class FloatInterpolator : Interpolator<float>
    {
        internal override void EvaluateAndSet(float t)
        {
            var deltaValue = endValue - startValue;
            var currentValue = (startValue + deltaValue * t);
            action?.Invoke(currentValue);
        }
    }

    class Vec2Interpolator : Interpolator<Vector2>
    {
        internal override void EvaluateAndSet(float t)
        {
            var deltaValue = endValue - startValue;
            var currentValue = (startValue + deltaValue * t);
            action?.Invoke(currentValue);
        }
    }

    class Vec3Interpolator : Interpolator<Vector3>
    {
        internal override void EvaluateAndSet(float t)
        {
            var deltaValue = endValue - startValue;
            var currentValue = (startValue + deltaValue * t);
            action?.Invoke(currentValue);
        }
    }

    class ColorInterpolator : Interpolator<Color>
    {
        internal override void EvaluateAndSet(float t)
        {
            var deltaValue = endValue - startValue;
            var currentValue = (startValue + deltaValue * t);
            action?.Invoke(currentValue);
        }
    }

    List<BaseInterpolator>          interpolators = new();
    Dictionary<string, int>         namedInterpolators = new();

    public BaseInterpolator Interpolate(float sourceValue, float targetValue, float time, Action<float> setAction, string name = null)
    {
        return Add(new FloatInterpolator()
        {
            name = name,
            easeFunction = Linear,
            currentTime = 0.0f,
            totalTime = time,
            startValue = sourceValue,
            endValue = targetValue,
            action = setAction,
        });
    }

    public BaseInterpolator Interpolate(Vector2 sourceValue, Vector2 targetValue, float time, Action<Vector2> setAction, string name = null)
    {
        return Add(new Vec2Interpolator()
        {
            name = name,
            easeFunction = Linear,
            currentTime = 0.0f,
            totalTime = time,
            startValue = sourceValue,
            endValue = targetValue,
            action = setAction,
        });
    }

    public BaseInterpolator Interpolate(Vector3 sourceValue, Vector3 targetValue, float time, Action<Vector3> setAction, string name = null)
    {
        return Add(new Vec3Interpolator()
        {
            name = name,
            easeFunction = Linear,
            currentTime = 0.0f,
            totalTime = time,
            startValue = sourceValue,
            endValue = targetValue,
            action = setAction,
        });
    }

    public BaseInterpolator Interpolate(Color sourceValue, Color targetValue, float time, Action<Color> setAction, string name = null)
    {
        return Add(new ColorInterpolator()
        {
            name = name,
            easeFunction = Linear,
            currentTime = 0.0f,
            totalTime = time,
            startValue = sourceValue,
            endValue = targetValue,
            action = setAction,
        });
    }

    static public float Linear(float t) => t;

    private void Update()
    {
        for (int i = 0; i < interpolators.Count; i++)
        {
            if (interpolators[i] == null) continue;
            interpolators[i].Run(Time.deltaTime);
            if (interpolators[i].isFinished)
            {
                CompleteAction(i);
            }
        }        
    }

    private void CompleteAction(int index)
    {
        interpolators[index].doneAction?.Invoke();
        if (!string.IsNullOrEmpty(interpolators[index].name))
        {
            namedInterpolators.Remove(interpolators[index].name);
        }
        interpolators[index] = null;
    }

    private BaseInterpolator Add(BaseInterpolator interpolator)
    {
        if (!string.IsNullOrEmpty(interpolator.name) && namedInterpolators.TryGetValue(interpolator.name, out int foundIndex))
        {
            interpolators[foundIndex] = interpolator;
            return interpolator;
        }

        for (int i = 0; i < interpolators.Count; i++)
        {
            if (interpolators[i] == null)
            {
                interpolators[i] = interpolator;
                if (!string.IsNullOrEmpty(interpolator.name)) namedInterpolators[interpolator.name] = i;
                return interpolator;
            }
        }
        interpolators.Add(interpolator);
        if (!string.IsNullOrEmpty(interpolator.name)) namedInterpolators[interpolator.name] = interpolators.Count - 1;

        return interpolator;
    }

    public enum StopBehaviour { SkipToEnd, Cancel };
    public void Stop(string name, StopBehaviour behaviour)
    {
        if (namedInterpolators.TryGetValue(name, out int foundIndex))
        {
            if (behaviour == StopBehaviour.SkipToEnd)
            {
                interpolators[foundIndex].EvaluateAndSet(1.0f);
            }
            CompleteAction(foundIndex);
        }
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
