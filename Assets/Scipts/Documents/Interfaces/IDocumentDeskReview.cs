using Zvezda57.Core;

namespace Zvezda57.Documents
{
    internal interface IDocumentDeskReview
    {
        void ResetCase();
        void InspectPage(int page);
        bool TryStamp(int page);
        string StampText(int page);
        void CaptureDesk(DeskSnapshot snapshot);
        void RestoreDesk(DeskSnapshot snapshot);
    }
}
