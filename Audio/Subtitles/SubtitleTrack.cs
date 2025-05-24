using System.Collections.Generic;
using UnityEngine;

namespace UC
{
    [CreateAssetMenu(fileName = "SubtitleTrack", menuName = "Audio/Subtitle Track", order = 1)]
    public class SubtitleTrack : ScriptableObject
    {
        [System.Serializable]
        public class SubtitleLine
        {
            public float startTime;   // In seconds
            public float endTime;     // In seconds
            [TextArea]
            public string text;
        }

        public List<SubtitleLine> lines = new List<SubtitleLine>();

        public SubtitleLine GetAtTime(float time)
        {
            foreach (var line in lines)
            {
                if ((line.startTime <= time) && (line.endTime >= time))
                {
                    return line;
                }
            }
            return null;
        }
    }
}
