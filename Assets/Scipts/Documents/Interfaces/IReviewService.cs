using UnityEngine;
using Zvezda57.Core;

namespace Zvezda57.Documents
{
    public interface IReviewService : IReviewProgress
    {
        CaseVerdict? PendingVerdict { get; }
        InspectionTool Tool { get; }

        void SelectStamp(CaseVerdict verdict);
        bool Ink();
        bool Stamp(int page);
        CaseVerdict? GetStamp(int page);
        bool SelectTool(InspectionTool tool);
        string Scan(int page, Vector2 point);
        string Call(string number);

        DecisionRecord Submit();
        bool ToolsUnlocked { get; set; }

        ReviewSnapshot Capture();
        void Restore(ReviewSnapshot snapshot);
        void Reset();
    }
}
