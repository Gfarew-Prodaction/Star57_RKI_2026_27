using UnityEngine;
using Zvezda57.Systems;

namespace Zvezda57.Dispatch
{
    [CreateAssetMenu(menuName = "Zvezda57/Incident")]
    public sealed class IncidentDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea]
        public string Description;
        [Range(1, 6)]
        public int Sector = 1;
        public CrewType[] AllowedCrews;
        [Min(1)]
        public float Deadline = 60;
        [Min(.1f)]
        public float TravelTime = 5;
        [Min(.1f)]
        public float WorkTime = 10;
        [Min(.1f)]
        public float ReturnTime = 5;
        public IncidentStage[] Stages = new IncidentStage[0];
        public string OnFailureIncidentId;
        public bool CutOfficePowerOnFailure;
        public MetricDelta SuccessEffect, FailureEffect;
        public bool EarlyWarningEligible;
        public bool AutoStart;
        [Min(0)]
        public float StartAfter;
    }
}
