using System;

namespace FunWithFER.Models
{
    [Flags]
    public enum RecordingState
    {
        Previewing = 0,
        Recording = 1,
        Stopped = 2,
        NotInitialized = 4
    }
}
