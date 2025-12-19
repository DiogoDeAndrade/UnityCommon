using System;
using UnityEngine;

namespace UC.RPG
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class DisplayDebugGraphAttribute : PropertyAttribute
    {
        public readonly string showMember;
        public readonly string minMember;
        public readonly string maxMember;
        public readonly string evalMember;
        public readonly string labelFunction;

        public readonly string title;
        public readonly float height;
        public readonly float padding;
        public readonly float sampleSpacing;
        public readonly float hSpacing;
        public readonly int vDivs;

        public DisplayDebugGraphAttribute(string showMember, string minMember, string maxMember, string evalMember, string title = "Preview", float height = 70f, float padding = 6f, float sampleSpacing = 0f, float hSpacing = 0f, string labelFunction = null, int vDivs = 3)
        {
            this.showMember = showMember;
            this.minMember = minMember;
            this.maxMember = maxMember;
            this.evalMember = evalMember;
            this.labelFunction = labelFunction;

            this.title = title;
            this.height = height;
            this.padding = padding;
            this.sampleSpacing = sampleSpacing;
            this.hSpacing = hSpacing;
            this.vDivs = vDivs;
        }
    }
}
