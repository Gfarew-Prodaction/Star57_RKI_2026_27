using System;
using System.Collections.Generic;
using System.Linq;
using Zvezda57.Systems;

namespace Zvezda57.Dispatch
{
    internal sealed class IncidentRuntime : IIncidentStatus
    {
        private readonly IReadOnlyList<CrewType>[] _allowedCrewsByStage;

        public IncidentDefinition Definition { get; }
        public string Id => Definition.Id;
        public string Title => Definition.Title;
        public string Description => Definition.Description;
        public int Sector => Definition.Sector;
        public IncidentState State { get; internal set; }
        public bool IsActive => State == IncidentState.Waiting || State == IncidentState.Assigned;
        public string PowerRestorationIncidentId => Definition.CutOfficePowerOnFailure ? Definition.OnFailureIncidentId : null;
        public float Remaining { get; internal set; }
        public int StageIndex { get; internal set; }
        public int StageCount => Definition.Stages.Length == 0 ? 1 : Definition.Stages.Length;
        public string StageTitle => Definition.Stages.Length == 0 ? Definition.Title : Definition.Stages[StageIndex].Title;
        public IReadOnlyList<CrewType> AllowedCrews => _allowedCrewsByStage[StageIndex];
        public float Deadline => Definition.Stages.Length == 0 ? Definition.Deadline : Definition.Stages[StageIndex].Deadline;
        public float WorkTime => Definition.Stages.Length == 0 ? Definition.WorkTime : Definition.Stages[StageIndex].WorkTime;

        public IncidentRuntime(IncidentDefinition definition)
        {
            Definition = definition;
            _allowedCrewsByStage = definition.Stages.Length == 0
                ? new[] { ReadAllowedCrews(definition.AllowedCrews) }
                : definition.Stages.Select(stage => ReadAllowedCrews(stage.AllowedCrews)).ToArray();
        }

        private static IReadOnlyList<CrewType> ReadAllowedCrews(CrewType[] crews) =>
            Array.AsReadOnly(crews ?? Array.Empty<CrewType>());

        public MetricDelta GetEffect(bool success) => success ? Definition.SuccessEffect : Definition.FailureEffect;
    }
}
