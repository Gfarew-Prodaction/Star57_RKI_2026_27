using System;
using Zvezda57.Dispatch;

namespace Zvezda57.Core
{
    [Serializable]
    public sealed class IncidentSnapshot
    {
        public string Id;
        public IncidentState State;
        public int Stage;
        public float Remaining;
    }
}
