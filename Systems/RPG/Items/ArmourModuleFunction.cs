using System;
using System.Collections.Generic;

namespace UC.RPG
{    
    [Serializable]
    public abstract class ArmourModuleFunction
    {
        public abstract void Mitigate(List<ChangeData> damage);
    }
}
