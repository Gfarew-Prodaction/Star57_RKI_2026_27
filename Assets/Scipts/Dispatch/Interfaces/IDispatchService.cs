using System;
using System.Collections.Generic;
using Zvezda57.Core;

namespace Zvezda57.Dispatch
{
    public interface IDispatchService : IDispatchStatus
    {
        event Action<IIncidentStatus> Finished;
        float TravelMultiplier { get; set; }

        float FireWorkMultiplier { get; set; }

        float EarlyWarningSeconds { get; set; }

        void AddCrew(CrewType type);
        bool Dispatch(int crewId, string incidentId, out string message);
        bool DispatchToSector(int crewId, int sector, out string message);
        bool Trigger(string id);
        bool Dispatch(CrewType crew, string incidentId, out string message);
        void Tick(float seconds);
        DispatchSnapshot Capture();
        void Restore(DispatchSnapshot snapshot);
        void Start();
        void ConfigureSchedule(Dictionary<string, float> schedule, Dictionary<string, bool> finished);
    }
}
