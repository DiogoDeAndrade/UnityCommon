using UnityEngine;
using NaughtyAttributes;
using System;

namespace UC
{

    public class FullscreenWiper : Singleton<FullscreenWiper>
    {
        [SerializeField] private Color wiperColor = Color.black;
        [SerializeField] private bool useUnscaledTime = false;
        [SerializeField] private bool startWiped;
        [ShowIf("startWiped")]
        [SerializeField] private bool autoWipeIn;
        [ShowIf(EConditionOperator.And, "startWiped", "autoWipeIn")]
        [SerializeField] private float wipeInTime = 0.75f;
        [ShowIf(EConditionOperator.And, "startWiped", "autoWipeIn")]
        [SerializeField] private WipeType wipeInType = WipeType.Random;

        WipeGraphic wiper;
        float       target;
        float       wipeInc;
        Action      callback;

        float deltaTime => (useUnscaledTime) ? Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime) : Time.deltaTime;

        protected override void Awake()
        {
            if (Instance != this) return;
            wiper = GetComponentInChildren<WipeGraphic>();

            wiper.color = wiperColor;
            wiper.open = (startWiped) ? (0.0f) : (1.0f);

            if ((startWiped) && (autoWipeIn))
            {
                WipeIn(wipeInTime, wipeInType);
            }
            else
            {
                wipeInc = 0;
            }
        }

        void Update()
        {
            if (wipeInc != 0.0f)
            {
                wiper.open = wiper.open + wipeInc * deltaTime;

                if (((wipeInc > 0.0f) && (wiper.open >= target)) ||
                    ((wipeInc < 0.0f) && (wiper.open <= target)))
                {
                    wiper.open = target;
                    wipeInc = 0.0f;
                    if (callback != null) callback.Invoke();
                    callback = null;
                }
            }
        }

        void _Wipe(float targetOpenness, float time, WipeType type, System.Action action)
        {
            // Check if we're already wiping towards this target
            if ((action == callback) && (action != null))
            {
                if (target == targetOpenness)
                {
                    return;
                }
            }

            if (type == WipeType.Random)
            {
                type = (WipeType)UnityEngine.Random.Range(0, 7);
            }
            wiper.type = type;

            target = targetOpenness;

            if (time <= 0.0f)
            {
                wiper.open = targetOpenness;
                wipeInc = 0.0f;
                callback = null;
                if (action != null) action.Invoke();
                return;
            }

            wipeInc = (targetOpenness - wiper.open) / time;
            if (wipeInc == 0.0f)
            {
                // Already at the target
                callback = null;
                if (action != null) action.Invoke();
                return;
            }
            callback = action;
        }

        public static void WipeIn(float time)
        {
            Instance?._Wipe(1.0f, time, WipeType.Random, null);
        }

        public static void WipeIn(float time, WipeType type)
        {
            Instance?._Wipe(1.0f, time, type, null);
        }

        public static void WipeIn(float time, WipeType type, System.Action action)
        {
            Instance?._Wipe(1.0f, time, type, action);
        }

        public static void WipeOut(float time)
        {
            Instance?._Wipe(0.0f, time, WipeType.Random, null);
        }

        public static void WipeOut(float time, WipeType type)
        {
            Instance?._Wipe(0.0f, time, type, null);
        }

        public static void WipeOut(float time, WipeType type, System.Action action)
        {
            Instance?._Wipe(0.0f, time, type, action);
        }

        public static bool hasWiper => Instance != null;

        public static bool isWiping => hasWiper && (Instance?.wipeInc != 0.0f);

    }
}
