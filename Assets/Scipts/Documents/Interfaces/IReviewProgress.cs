namespace Zvezda57.Documents
{
    public interface IReviewProgress
    {
        CaseVerdict? SelectedStamp { get; }
        bool Inked { get; }
        bool CanSubmit { get; }
    }
}
