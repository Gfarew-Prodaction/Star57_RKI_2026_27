using System;
using System.Collections.Generic;
using Zvezda57.Documents;

namespace Zvezda57.Core
{
    public sealed class CaseQueue : ICaseQueue
    {
        private IReadOnlyList<CaseDefinition> _cases;
        private int _index = -1;
        public CaseDefinition Current => _index >= 0 && _index < _cases.Count ? _cases[_index] : null;
        public IntakeState State { get; private set; } = IntakeState.Waiting;
        public int Remaining => Math.Max(0, _cases.Count - _index - 1);

        public event Action Changed;
        public CaseQueue(IReadOnlyList<CaseDefinition> cases)
        {
            _cases = cases;
        }

        public void SetCases(IReadOnlyList<CaseDefinition> content)
        {
            _cases = content;
            _index = -1;
            State = IntakeState.Waiting;
            Changed?.Invoke();
        }

        public QueueSnapshot Capture() => new QueueSnapshot
        {
            CaseId = Current?.Id,
            State = State
        };
        public void Restore(QueueSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(snapshot.CaseId))
            {
                if (snapshot.State == IntakeState.Complete)
                {
                    _index = _cases.Count;
                    State = IntakeState.Complete;
                    Changed?.Invoke();
                }

                return;
            }

            for (int i = 0; i < _cases.Count; i++)
            {
                if (_cases[i].Id == snapshot.CaseId)
                {
                    _index = i;
                    State = snapshot.State;
                    Changed?.Invoke();
                    return;
                }
            }

            if (snapshot.State == IntakeState.Filed)
            {
                _index = -1;
                State = IntakeState.Filed;
                Changed?.Invoke();
            }
        }

        public bool OfferNext()
        {
            if (State != IntakeState.Waiting && State != IntakeState.Filed)
            {
                return false;
            }

            _index++;
            State = _index < _cases.Count ? IntakeState.Offered : IntakeState.Complete;
            Changed?.Invoke();
            return State == IntakeState.Offered;
        }

        public bool Receive()
        {
            if (State != IntakeState.Offered)
            {
                return false;
            }

            State = IntakeState.Received;
            Changed?.Invoke();
            return true;
        }

        public bool File()
        {
            if (State != IntakeState.Received)
            {
                return false;
            }

            State = IntakeState.Filed;
            Changed?.Invoke();
            return true;
        }
    }
}
