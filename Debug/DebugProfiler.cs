using System.Diagnostics;
using UnityEngine;

namespace UC
{
    public class DebugProfiler 
    {
        private long    t0;
        private long    t1;
        private bool    activeTimer;
        private double  _accumulatedTime;
        private double  _lastTime;

        public void Mark()
        {
            if (activeTimer)
            {
                t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                _lastTime = (t1 - t0) / (float)System.Diagnostics.Stopwatch.Frequency;
                _accumulatedTime += _lastTime;
                activeTimer = false;
            }
            else
            {
                t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                activeTimer = true;
            }
        }

        public void Clear()
        {
            _accumulatedTime = 0;
            _lastTime = 0;
            t0 = t1 = 0;
            activeTimer = false;
        }

        public double accumulatedTimeSec => _accumulatedTime;
        public double accumulatedTimeMS => _accumulatedTime * 1000;
        public double lastTimeSec => _lastTime;

        [Conditional("UC_PROFILER_ENABLE")]
        public static void DebugMark(DebugProfiler dp) => dp?.Mark();
    }
}
