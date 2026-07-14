using UnityEngine;
using NaughtyAttributes;
using System;

namespace UC
{

    public class FullscreenWiper : MonoBehaviour
    {
        [SerializeField] private Color wiperColor = Color.black;
        [SerializeField] private bool startWiped;
        [ShowIf("startWiped")]
        [SerializeField] private bool autoWipeIn;
        [ShowIf(EConditionOperator.And, "startWiped", "autoWipeIn")]
        [SerializeField] private float wipeInTime = 0.75f;
        [ShowIf(EConditionOperator.And, "startWiped", "autoWipeIn")]
        [SerializeField] private WipeType wipeInType = WipeType.Random;

        WipeGraphic wiper;
        float target;
        float wipeInc;
        System.Action callback;

        static FullscreenWiper fsWiper;

        void Awake()
        {
            if (fsWiper == null)
            {
                fsWiper = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

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
                wiper.open = wiper.open + wipeInc * Time.deltaTime;

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
            fsWiper._Wipe(1.0f, time, WipeType.Random, null);
        }

        public static void WipeIn(float time, WipeType type)
        {
            fsWiper._Wipe(1.0f, time, type, null);
        }

        public static void WipeIn(float time, WipeType type, System.Action action)
        {
            fsWiper._Wipe(1.0f, time, type, action);
        }

        public static void WipeOut(float time)
        {
            fsWiper._Wipe(0.0f, time, WipeType.Random, null);
        }

        public static void WipeOut(float time, WipeType type)
        {
            fsWiper._Wipe(0.0f, time, type, null);
        }

        public static void WipeOut(float time, WipeType type, System.Action action)
        {
            fsWiper._Wipe(0.0f, time, type, action);
        }

        public static bool hasWiper => fsWiper != null;

        public static bool isWiping => (fsWiper != null) && (fsWiper.wipeInc != 0.0f);

    }
}
