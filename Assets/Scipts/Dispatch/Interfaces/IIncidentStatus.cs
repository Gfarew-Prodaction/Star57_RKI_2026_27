using System.Collections.Generic;
using Zvezda57.Systems;

namespace Zvezda57.Dispatch
{
    public interface IIncidentStatus
    {
        string Id { get; }
        string Title { get; }
        string Description { get; }
        int Sector { get; }
        IncidentState State { get; }
        bool IsActive { get; }
        string PowerRestorationIncidentId { get; }
        float Remaining { get; }
        int StageIndex { get; }
        int StageCount { get; }
        string StageTitle { get; }
        IReadOnlyList<CrewType> AllowedCrews { get; }

        MetricDelta GetEffect(bool success);
    }
}
