using System;

namespace UC.RPG
{
    [Flags]
    public enum DataChannel { Channel1 = 1, Channel2 = 2, Channel3 = 4, Channel4 = 8, 
                              Channel5 = 16, Channel6 = 32, Channel7 = 64, Channel8 = 128, 
                              All = Channel1 | Channel2 | Channel3 | Channel4 | Channel5 | Channel6 | Channel7 | Channel8
    };
}
