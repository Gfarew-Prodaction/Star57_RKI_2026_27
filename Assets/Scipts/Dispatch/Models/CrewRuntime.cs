namespace Zvezda57.Dispatch
{
    internal sealed class CrewRuntime : ICrewStatus
    {
        public CrewType Type { get; }
        public CrewState State { get; internal set; }
        public IncidentRuntime Incident { get; internal set; }
        public float Remaining { get; internal set; }

        public CrewRuntime(CrewType type)
        {
            Type = type;
        }
    }
}
