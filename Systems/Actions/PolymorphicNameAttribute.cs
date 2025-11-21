using System;

namespace UC.Interaction
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class PolymorphicNameAttribute : Attribute
    {
        public string Path { get; }

        public PolymorphicNameAttribute(string path)
        {
            Path = path;
        }
    }

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
