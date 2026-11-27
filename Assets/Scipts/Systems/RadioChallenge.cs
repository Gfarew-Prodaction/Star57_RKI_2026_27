using System;

namespace Zvezda57.Systems
{
    internal sealed class RadioChallenge
    {
        internal const int MistakeLimit = 2;
        private const float MistakePenaltySeconds = 5f;

        private readonly RadioStep[] _steps;
        private readonly float _duration;

        public int Index { get; private set; }
        public int Mistakes { get; private set; }
        public float Remaining { get; private set; }
        public RadioStep Current => Index >= 0 && Index < _steps.Length ? _steps[Index] : null;

        public RadioChallenge(RadioStep[] steps, float duration)
        {
            _steps = steps;
            _duration = duration;
        }

        public void Restore(int index, int mistakes, float remaining)
        {
            Index = index;
            Mistakes = mistakes;
            Remaining = remaining;
        }

        public void Start()
        {
            Restore(0, 0, _duration);
        }

        public bool Elapse(float seconds)
        {
            Remaining = Math.Max(0, Remaining - seconds);
            return Remaining <= 0;
        }

        public RadioChoiceResult Choose(int option)
        {
            var step = Current;
            if (step == null || option < 0 || option >= step.Options.Length)
            {
                return RadioChoiceResult.Invalid;
            }

            if (option == step.CorrectOption)
            {
                Index++;
                return Index == _steps.Length ? RadioChoiceResult.Completed : RadioChoiceResult.InProgress;
            }

            Mistakes++;
            bool expired = Elapse(MistakePenaltySeconds);
            return expired || Mistakes >= MistakeLimit ? RadioChoiceResult.Failed : RadioChoiceResult.InProgress;
        }
    }
}
