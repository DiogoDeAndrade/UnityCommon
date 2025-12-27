using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UC.RPG;
using UnityEditor;
using UnityEngine;

namespace UC
{

    public class TurnManager_FactionBased : TurnManager
    {
        [SerializeField]
        private bool        autoStart = true;
        [SerializeField]
        private Faction[]   factionSequence;
        [SerializeField]
        private bool        simultaneousTurns = false;
        [SerializeField, HideIf(nameof(simultaneousTurns))] 
        float               timeBetweenEntities = 0.15f;
        [SerializeField] 
        float               timeBetweenFactions = 0.3f;

        List<UnityRPGEntity>    pendingEntities;
        int                     factionIndex = -1;
        bool                    suspend = false;

        void Start()
        {
            if (autoStart)
            {
                suspend = false;
                StartCoroutine(WaitAndNextStateCR());
            }
            else
            {
                suspend = true;
            }
        }

        public override void _StartTurns()
        {
            if (!suspend) return;

            suspend = false;
            NextState();
        }

        public override void _StopTurns()
        {
            if (suspend) return;

            suspend = true;
        }

        IEnumerator WaitAndNextStateCR()
        {
            yield return null;
            NextState();
        }

        void NextState()
        {
            int nTries = 0;
            while (nTries < factionSequence.Length)
            {
                if (factionIndex < 0) factionIndex = 0;
                else factionIndex = (factionIndex + 1) % factionSequence.Length;

                var faction = factionSequence[factionIndex];

                var entities = UnityRPGEntity.GetEntities()
                    .Select(e => e.Value)
                    .Where(entity => (entity != null) && (entity.faction == faction) && (!entity.isDead))
                    .ToList();

                if (entities.Count > 0)
                {
                    RunTurn(entities);
                    return;
                }
                else
                {
                    nTries++;
                }
            }
            
            Debug.LogError("No entities found for any faction. TurnManager_FactionBased is stuck.");
        }

        void RunTurn(List<UnityRPGEntity> entities)
        {
            if (pendingEntities != null)
            {
                foreach (var pendingEntity in pendingEntities)
                {
                    pendingEntity.onActionPerformed -= TurnDone;
                    pendingEntity.RunTurn(false);
                }
            }
            pendingEntities = new List<UnityRPGEntity>(entities);

            // Sort by initiative
            pendingEntities.Sort((x, y) => y.GetInitiative().CompareTo(x.GetInitiative()));

            if (simultaneousTurns)
            {
                var sEntities = new List<UnityRPGEntity>(pendingEntities);
                foreach (var pendingEntity in sEntities)
                {
                    pendingEntity.onActionPerformed += TurnDone;
                    pendingEntity.RunTurn(true);
                }
            }
            else
            {
                var pendingEntity = pendingEntities[0];
                pendingEntity.onActionPerformed += TurnDone;
                pendingEntity.RunTurn(true);
            }
        }

        private void TurnDone(UnityRPGEntity entity)
        {
            entity.onActionPerformed -= TurnDone;
            entity.RunTurn(false);
            pendingEntities.Remove(entity);

            if (pendingEntities.Count == 0)
            {
                if (!suspend)
                {
                    StartCoroutine(NextStateDeferredCR());
                }
            }
            else if (!simultaneousTurns)
            {
                // Queue next
                StartCoroutine(NextEntityCR());
            }
        }

        IEnumerator NextStateDeferredCR()
        {
            yield return new WaitForSeconds(timeBetweenFactions);
            NextState();
        }

        IEnumerator NextEntityCR()
        {
            yield return new WaitForSeconds(timeBetweenEntities);
            var pendingEntity = pendingEntities[0];
            pendingEntity.onActionPerformed += TurnDone;
            pendingEntity.RunTurn(true);
        }
    }
}
