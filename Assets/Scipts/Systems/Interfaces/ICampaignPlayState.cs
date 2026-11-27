namespace Zvezda57.Systems
{
    public interface ICampaignPlayState
    {
        bool CanInteract { get; }
        bool CanEndShift { get; }
        int ShiftIndex { get; }
        IncomingCallState CallState { get; }
    }
}
