using System;
using Zvezda57.Core;

namespace Zvezda57.Systems
{
    public interface ICampaignService : ICampaignPlayState, IYuraAssignments
    {
        CampaignPhase Phase { get; }

        ShiftDefinition Shift { get; }

        int YuraTrust { get; }

        DialogueDefinition Dialogue { get; }

        EndingDefinition Ending { get; }

        bool TaskInterrupted { get; }

        float CallRemaining { get; }

        RadioStep RadioStep { get; }

        int RadioIndex { get; }

        float RadioRemaining { get; }

        int RadioMistakes { get; }
        int RadioMistakeLimit { get; }

        DeskSnapshot Desk { get; }

        void BindView(Func<DeskSnapshot> capture);
        void Checkpoint();
        void TogglePause();
        bool AnswerCall();
        bool ChooseRadio(int option);
        void CancelRadio();

        void Start();
        void Tick(float seconds);
        bool CanChoose(int index);
        bool Choose(int index);
        DecisionRecord SubmitCase();
        bool EndShift();
        bool Continue();
        bool HasFlag(string flag);
        void Clear();
    }
}
