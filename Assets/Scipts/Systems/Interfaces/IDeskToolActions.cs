using Zvezda57.Documents;

namespace Zvezda57.Systems
{
    internal interface IDeskToolActions
    {
        void SelectStamp(CaseVerdict verdict);
        void InkStamp();
        void OpenReference();
        void SelectTool(InspectionTool tool);
        void AnswerCall();
    }
}
