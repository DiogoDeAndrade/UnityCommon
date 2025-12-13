using System;

namespace UC
{

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class GameActionNameAttribute : PolymorphicNameAttribute
    {
        public GameActionNameAttribute(string path) : base(path) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ConditionNameAttribute : PolymorphicNameAttribute
    {
        public ConditionNameAttribute(string path) : base(path) { }
    }
}