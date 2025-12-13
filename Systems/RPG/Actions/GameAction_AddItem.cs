using System.Collections;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG.Actions
{
    [System.Serializable]
    [GameActionName("RPG/Add Item")]
    public class GameAction_AddItem : GameAction
    {
        [SerializeField] private RPGTarget itemTarget;

        public override bool NeedWait() { return false; }

        public override IEnumerator Execute(ActionContext context)
        {
            // Get action entity
            var rpgEntity = RPGTarget.TriggerEntity.GetEntity(context);
            var inventoryInstance = rpgEntity?.inventory;

            if (inventoryInstance == null)
            {
                Debug.LogWarning("No inventory on action object, can't add item!");
                yield break;
            }

            // Find item to add
            var item = itemTarget.GetEntity(context);
            if (item == null)
            {
                Debug.LogWarning("No item on target entity, can't add item!");
                yield break;
            }

            inventoryInstance.Add(item);
            rpgEntity.AddChild(item);

            yield break;
        }
    }
}
