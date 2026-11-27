using System;
using UnityEngine;
using Zvezda57.Dispatch;

namespace Zvezda57.Systems
{
    [Serializable]
    public sealed class ScheduledIncident
    {
        [Tooltip("Select an incident asset. The schedule adds it to the dispatch catalog.")]
        public IncidentDefinition Incident;

        [Tooltip("ID used by older schedules. Leave blank when selecting an incident asset.")]
        public string Id;

        [Min(0)]
        public float Delay;

        public string IncidentId => Incident != null ? Incident.Id : Id;
    }
}
