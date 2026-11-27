using System;
using Zvezda57.Dispatch;

namespace Zvezda57.Core
{
    [Serializable]
    public sealed class CrewSnapshot
    {
        public CrewType Type;
        public CrewState State;
        public string IncidentId;
        public float Remaining;
    }
}
