using System;
using System.Collections.Generic;

namespace Zvezda57.Dispatch
{
    public interface IDispatchStatus
    {
        IReadOnlyList<IIncidentStatus> Incidents { get; }

        IReadOnlyList<ICrewStatus> Crews { get; }

        event Action Changed;
    }
}
