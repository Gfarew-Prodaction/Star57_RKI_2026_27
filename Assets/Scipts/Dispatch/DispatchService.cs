using System;
using System.Collections.Generic;
using System.Linq;
using Zvezda57.Core;

namespace Zvezda57.Dispatch
{
    public sealed class DispatchService : IDispatchService
    {
        private const float MaximumTickStep = .1f;
        private const float MinimumDurationMultiplier = .05f;
        private const int WrongDispatchTimePenalty = 10;
        private const int WrongDispatchReturnTime = 8;

        private readonly List<IncidentRuntime> _incidents;
        private readonly List<CrewRuntime> _crews;
        private bool _running;
        private float _elapsed;
        private Dictionary<string, float> _schedule;
        public void ConfigureSchedule(Dictionary<string, float> planned, Dictionary<string, bool> finished)
        {
            _schedule = planned;
            _elapsed = 0;
            _running = false;
            foreach (var incident in _incidents)
            {
                incident.StageIndex = 0;
                incident.Remaining = 0;
                if (finished.TryGetValue(incident.Id, out bool success))
                {
                    incident.State = success ? IncidentState.Resolved : IncidentState.Failed;
                }
                else
                {
                    incident.State = IncidentState.Inactive;
                }
            }

            foreach (var crew in _crews)
            {
                crew.State = CrewState.Free;
                crew.Incident = null;
                crew.Remaining = 0;
            }

            Changed?.Invoke();
        }

        public IReadOnlyList<IIncidentStatus> Incidents { get; }
        public IReadOnlyList<ICrewStatus> Crews { get; }

        public event Action Changed;
        public event Action<IIncidentStatus> Finished;
        public float TravelMultiplier { get; set; } = 1;
        public float FireWorkMultiplier { get; set; } = 1;
        public float EarlyWarningSeconds { get; set; }

        public void AddCrew(CrewType type)
        {
            _crews.Add(new CrewRuntime(type));
            Changed?.Invoke();
        }

        public DispatchService(IEnumerable<IncidentDefinition> definitions)
        {
            _incidents = definitions.Select(x => new IncidentRuntime(x)).ToList();
            _crews = Enum.GetValues(typeof(CrewType)).Cast<CrewType>().Select(x => new CrewRuntime(x)).ToList();
            Incidents = _incidents.AsReadOnly();
            Crews = _crews.AsReadOnly();
        }

        public DispatchSnapshot Capture() => new DispatchSnapshot
        {
            Elapsed = _elapsed,
            Incidents = _incidents.Select(x => new IncidentSnapshot { Id = x.Definition.Id, State = x.State, Stage = x.StageIndex, Remaining = x.Remaining }).ToList(),
            Crews = _crews.Select(x => new CrewSnapshot { Type = x.Type, State = x.State, IncidentId = x.Incident?.Definition.Id, Remaining = x.Remaining }).ToList()
        };
        public void Restore(DispatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _elapsed = Math.Max(0, snapshot.Elapsed);
            foreach (var item in snapshot.Incidents)
            {
                var incident = _incidents.Find(x => x.Definition.Id == item.Id);
                if (incident == null)
                {
                    continue;
                }

                incident.StageIndex = Math.Max(0, Math.Min(item.Stage, incident.StageCount - 1));
                incident.State = item.State;
                incident.Remaining = Math.Max(0, item.Remaining);
            }

            if (snapshot.Crews.Count > _crews.Count)
            {
                throw new InvalidOperationException("Saved crew roster differs from purchased upgrades.");
            }

            for (int i = 0; i < snapshot.Crews.Count; i++)
            {
                var item = snapshot.Crews[i];
                var crew = _crews[i];
                if (item.Type != crew.Type)
                {
                    throw new InvalidOperationException("Invalid saved crew identity");
                }

                crew.State = item.State;
                crew.Remaining = Math.Max(0, item.Remaining);
                crew.Incident = _incidents.Find(x => x.Definition.Id == item.IncidentId);
                if (crew.State != CrewState.Free && crew.Incident == null)
                {
                    throw new InvalidOperationException("Busy crew has no incident");
                }
            }

            Changed?.Invoke();
        }

        public void Start()
        {
            _running = true;
        }

        public bool Trigger(string id)
        {
            var incident = _incidents.Find(x => x.Id == id);
            if (incident == null || incident.State != IncidentState.Inactive)
            {
                return false;
            }

            ActivateIncident(incident);
            return true;
        }

        private void ActivateIncident(IncidentRuntime incident)
        {
            incident.State = IncidentState.Waiting;
            incident.Remaining = incident.Deadline + (incident.Definition.EarlyWarningEligible ? EarlyWarningSeconds : 0);
            Changed?.Invoke();
        }

        public bool Dispatch(CrewType type, string id, out string message)
        {
            int index = _crews.FindIndex(x => x.Type == type && x.State == CrewState.Free);
            return Dispatch(index, id, out message);
        }

        public bool Dispatch(int crewId, string id, out string message)
        {
            var crew = GetCrew(crewId);
            var incident = _incidents.Find(x => x.Id == id);
            return TryAssign(crew, incident, out message);
        }

        public bool DispatchToSector(int crewId, int sector, out string message)
        {
            var crew = GetCrew(crewId);
            IncidentRuntime firstWaiting = null;
            IncidentRuntime mostUrgentMatch = null;
            foreach (var incident in _incidents)
            {
                if (incident.Sector != sector || incident.State != IncidentState.Waiting)
                {
                    continue;
                }

                firstWaiting ??= incident;
                if (crew != null && incident.AllowedCrews.Contains(crew.Type) &&
                    (mostUrgentMatch == null || incident.Remaining < mostUrgentMatch.Remaining))
                {
                    mostUrgentMatch = incident;
                }
            }

            if (firstWaiting == null)
            {
                message = "В секторе нет ожидающего вызова.";
                return false;
            }

            return TryAssign(crew, mostUrgentMatch ?? firstWaiting, out message);
        }

        private CrewRuntime GetCrew(int crewId) => crewId >= 0 && crewId < _crews.Count ? _crews[crewId] : null;

        private bool TryAssign(CrewRuntime crew, IncidentRuntime incident, out string message)
        {
            if (crew == null || crew.State != CrewState.Free)
            {
                message = "Экипаж занят.";
                return false;
            }

            if (incident == null || incident.State != IncidentState.Waiting)
            {
                message = "Нет ожидающего вызова.";
                return false;
            }

            if (!incident.AllowedCrews.Contains(crew.Type))
            {
                crew.Incident = incident;
                crew.State = CrewState.Return;
                crew.Remaining = WrongDispatchReturnTime;
                incident.Remaining = Math.Max(0, incident.Remaining - WrongDispatchTimePenalty);
                message = $"Служба не подходит. Возврат: {WrongDispatchReturnTime} с. Срок вызова: −{WrongDispatchTimePenalty} с.";
                if (incident.Remaining == 0)
                {
                    FailIncident(incident);
                    message += " Вызов провален.";
                }

                Changed?.Invoke();
                return false;
            }

            crew.State = CrewState.Travel;
            crew.Incident = incident;
            crew.Remaining = incident.Definition.TravelTime * Math.Max(MinimumDurationMultiplier, TravelMultiplier);
            incident.State = IncidentState.Assigned;
            message = "Экипаж направлен.";
            Changed?.Invoke();
            return true;
        }

        public void Tick(float seconds)
        {
            if (!_running || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                return;
            }

            while (seconds > 0)
            {
                float step = Math.Min(MaximumTickStep, seconds);
                seconds -= step;
                _elapsed += step;

                foreach (var incident in _incidents)
                {
                    UpdateIncident(incident, step);
                }

                foreach (var crew in _crews)
                {
                    UpdateCrew(crew, step);
                }
            }

            Changed?.Invoke();
        }

        private void UpdateIncident(IncidentRuntime incident, float seconds)
        {
            if (incident.State == IncidentState.Inactive && IsScheduledNow(incident.Definition))
            {
                ActivateIncident(incident);
            }

            if (!incident.IsActive)
            {
                return;
            }

            incident.Remaining = Math.Max(0, incident.Remaining - seconds);

            bool crewOnSite = _crews.Exists(crew => crew.Incident == incident && crew.State == CrewState.Work);
            if (crewOnSite)
            {
                incident.Remaining += seconds;
            }
            else if (incident.Remaining <= 0)
            {
                FailIncident(incident);
            }
        }

        private bool IsScheduledNow(IncidentDefinition definition)
        {
            float delay;
            if (_schedule == null)
            {
                if (!definition.AutoStart)
                {
                    return false;
                }

                delay = definition.StartAfter;
            }
            else if (!_schedule.TryGetValue(definition.Id, out delay))
            {
                return false;
            }

            float earlyWarning = definition.EarlyWarningEligible ? EarlyWarningSeconds : 0;
            return _elapsed >= Math.Max(0, delay - earlyWarning);
        }

        private void FailIncident(IncidentRuntime incident)
        {
            incident.State = IncidentState.Failed;

            string escalation = incident.Definition.OnFailureIncidentId;
            if (!string.IsNullOrEmpty(escalation))
            {
                Trigger(escalation);
            }

            foreach (var crew in _crews.Where(crew => crew.Incident == incident && crew.State != CrewState.Return))
            {
                SendCrewBack(crew);
            }

            Finished?.Invoke(incident);
        }

        private void UpdateCrew(CrewRuntime crew, float seconds)
        {
            if (crew.State == CrewState.Free)
            {
                return;
            }

            crew.Remaining -= seconds;
            if (crew.Remaining > 0)
            {
                return;
            }

            switch (crew.State)
            {
                case CrewState.Travel:
                    crew.State = CrewState.Work;
                    float workMultiplier = crew.Type == CrewType.Fire
                        ? Math.Max(MinimumDurationMultiplier, FireWorkMultiplier)
                        : 1f;
                    crew.Remaining = crew.Incident.WorkTime * workMultiplier;
                    break;

                case CrewState.Work:
                    SendCrewBack(crew);
                    CompleteStage(crew.Incident);
                    break;

                case CrewState.Return:
                    crew.State = CrewState.Free;
                    crew.Incident = null;
                    crew.Remaining = 0;
                    break;
            }
        }

        private void CompleteStage(IncidentRuntime incident)
        {
            if (incident.StageIndex + 1 < incident.StageCount)
            {
                incident.StageIndex++;
                incident.State = IncidentState.Waiting;
                incident.Remaining = incident.Deadline;
                return;
            }

            incident.State = IncidentState.Resolved;
            Finished?.Invoke(incident);
        }

        private static void SendCrewBack(CrewRuntime crew)
        {
            crew.State = CrewState.Return;
            crew.Remaining = crew.Incident.Definition.ReturnTime;
        }
    }
}
