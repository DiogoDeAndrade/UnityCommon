using System.Collections;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG.Actions
{
    [System.Serializable]
    [GameActionName("RPG/Destroy Entity")]
    public class GameAction_DestroyEntity : GameAction
    {
        [SerializeField] private UnityRPGEntity entity;

        public override bool NeedWait() { return false; }

        public override IEnumerator Execute(GameObject source, GameObject target)
        {
            var targetEntity = (entity) ? (entity) : (target.GetComponent<UnityRPGEntity>());
            if (targetEntity == null)
            {
                Debug.LogWarning("No RPG entity on target object, can't destroy entity!");
                yield break;
            }

            GameObject.Destroy(targetEntity.gameObject);

            yield break;
        }
    }
}
