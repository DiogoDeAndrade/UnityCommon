using System;
using System.Collections.Generic;
using UnityEngine;
using UC;

namespace UC.RPG
{
    [Serializable]
    [PolymorphicName("RPG/Turn Driver/Composite")]
    public class TurnDriverComposite : TurnDriver
    {
        [SerializeReference]
        private List<TurnDriver> _drivers = new();

        // Optional bias added on top of the best sub-driver priority (lets this composite "compete" vs other drivers).
        [SerializeField]
        private float _priorityBias = 0.0f;

        public IReadOnlyList<TurnDriver> drivers => _drivers;

        public override string GetModuleHeaderString() => "Turn Driver (Composite)";

        public override bool IsEnabled(UnityRPGEntity entity)
        {
            if (!_enabled)
            {
                return false;
            }

            return FindBestDriver(entity) != null;
        }

        public override float GetPriority(UnityRPGEntity entity)
        {
            var best = FindBestDriver(entity);
            if (best == null) return float.NegativeInfinity;

            return best.GetPriority(entity) + _priorityBias;
        }

        public override bool Execute(UnityRPGEntity entity)
        {
            var best = FindBestDriver(entity);
            if (best != null)
            {
                return best.Execute(entity);
            }

            return false;
        }

        private TurnDriver FindBestDriver(UnityRPGEntity entity)
        {
            TurnDriver best = null;
            float bestPriority = float.NegativeInfinity;

            if (_drivers == null)
                return null;

            for (int i = 0; i < _drivers.Count; i++)
            {
                var d = _drivers[i];
                if (d == null || !d.enabled)
                    continue;

                if (!d.IsEnabled(entity))
                    continue;

                float p = d.GetPriority(entity);

                // Tie-breaker: earlier in list wins (stable + designer-friendly)
                if (p > bestPriority)
                {
                    bestPriority = p;
                    best = d;
                }
            }

            return best;
        }
    }
}
