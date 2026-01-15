using UC.RPG;
using UnityEngine;

namespace UC.Interaction
{
    public partial class ActionContext
    {
        public GameObject       triggerGameObject;
        public RPGEntity        triggerEntity;
        public GameObject       targetGameObject;
        public RPGEntity        targetEntity;
        // For resource change events
        public GameObject       changeSource;
        public ChangeData       changeData;
        // For action execution
        public ActionRunner     runner;
    }
}