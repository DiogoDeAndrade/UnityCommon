using System;

namespace UC
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
}
