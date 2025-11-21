using System.Collections;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG.Actions
{
    [System.Serializable]
    [GameActionName("RPG/Pickup Item")]
    public class GameAction_PickupItem : GameAction
    {
        [SerializeField] private UnityRPGEntity entity;

        public override bool NeedWait() { return false; }
         
        public override IEnumerator Execute(GameObject go)
        {
            throw new System.NotImplementedException();
        }
    }
}
