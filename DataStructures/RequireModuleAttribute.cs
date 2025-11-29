using System;
using UnityEngine;

namespace UC.RPG
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class RequireModuleAttribute : PropertyAttribute
    {
        public readonly Type moduleType;
        public readonly bool includeParents;

        public RequireModuleAttribute(Type moduleType, bool includeParents = true)
        {
            this.moduleType = moduleType;
            this.includeParents = includeParents;
        }
    }
}
