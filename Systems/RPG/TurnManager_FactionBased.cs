using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UC.RPG;
using UnityEngine;

namespace UC
{

    public class TurnManager_FactionBased : MonoBehaviour
    {
        [SerializeField]
        private bool        autoStart = true;
        [SerializeField]
        private Faction[]   factionSequence;
        [SerializeField]
        private bool        simultaneousTurns = false;

        List<UnityRPGEntity>    pendingEntities;
        int                     factionIndex = -1;

        void Start()
        {
            if (autoStart)
                StartCoroutine(WaitAndNextStateCR());
        }

        public void StartTurns()
        {
            NextState();
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

                var entities = UnityRPGEntity
                    .GetEntities()
                    .Select(e => e.Value)
                    .Where(entity => (entity.faction != null) && (entity.faction == faction) && (!entity.isDead))
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
                foreach (var pendingEntity in pendingEntities)
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
                NextState();
           }
            else if (!simultaneousTurns)
            {
                // Next entity
                var pendingEntity = pendingEntities[0];
                pendingEntity.onActionPerformed += TurnDone;
                pendingEntity.RunTurn(true);
            }
        }
    }
}
