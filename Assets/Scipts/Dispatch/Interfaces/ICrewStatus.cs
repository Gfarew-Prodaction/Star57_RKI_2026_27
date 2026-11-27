namespace Zvezda57.Dispatch
{
    public interface ICrewStatus
    {
        CrewType Type { get; }
        CrewState State { get; }
        float Remaining { get; }
    }
}
