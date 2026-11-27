using UnityEngine;

namespace Zvezda57.Dispatch
{
    [System.Serializable]
    public sealed class IncidentStage
    {
        public string Title;
        public CrewType[] AllowedCrews;
        [Min(1)]
        public float Deadline = 60;
        [Min(.1f)]
        public float WorkTime = 8;
    }
}
