using System.Collections;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG.Actions
{
    [System.Serializable]
    [GameActionName("RPG/Destroy Entity")]
    public class GameAction_DestroyEntity : GameAction
    {
        [SerializeField] private RPGTarget entity;

        public override bool NeedWait() { return false; }

        public override IEnumerator Execute(ActionContext context)
        {
            var targetObject = entity.GetGameObject(context);
            if (targetObject != null)
            {
                GameObject.Destroy(targetObject);

                yield break;
            }

            var targetEntity = entity.GetEntity(context);
            if (targetEntity != null)
            {
                // Destroy an RPG entity, we need to do some work here - figure out who's the owner and remove it from inventory and equipment list, if it exists
                var ownerObj = targetEntity.owner;
                if (ownerObj != null)
                {
                    ownerObj.RemoveChild(targetEntity);
                }

                // Search for a UnityRPGEntity
                var gameObject = UnityRPGEntity.GetEntity(targetEntity);
                if (gameObject) GameObject.Destroy(gameObject);

                yield break;
            }


            Debug.LogWarning("No target object, can't destroy entity!");
            yield break;
        }
    }
}
