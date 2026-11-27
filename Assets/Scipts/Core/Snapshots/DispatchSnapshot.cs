using System;
using System.Collections.Generic;

namespace Zvezda57.Core
{
    [Serializable]
    public sealed class DispatchSnapshot
    {
        public float Elapsed;
        public List<IncidentSnapshot> Incidents = new List<IncidentSnapshot>();
        public List<CrewSnapshot> Crews = new List<CrewSnapshot>();
    }
}
