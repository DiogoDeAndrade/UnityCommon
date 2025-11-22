using System.Collections;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG.Actions
{
    [System.Serializable]
    [GameActionName("RPG/Add Item")]
    public class GameAction_AddItem : GameAction
    {
        [SerializeField] private UnityRPGEntity entity;

        public override bool NeedWait() { return false; }

        public override IEnumerator Execute(GameObject source, GameObject target)
        {
            // Get action entity
            var sourceEntity = source.GetComponent<UnityRPGEntity>();
            var inventoryInstance = sourceEntity?.rpgEntity?.inventory ?? null;

            if (inventoryInstance == null)
            {
                Debug.LogWarning("No inventory on action object, can't add item!");
                yield break;
            }

            // Find item to add
            var targetEntity = (entity) ? (entity) : (target.GetComponent<UnityRPGEntity>());
            if (targetEntity == null)
            {
                Debug.LogWarning("No RPG entity on target object, can't get item to add!");
                yield break;
            }
            var item = targetEntity.rpgEntity.item;
            if (item == null)
            {
                Debug.LogWarning("No item on target entity, can't add item!");
                yield break;
            }

            inventoryInstance.Add(item);

            yield break;
        }
    }
}
