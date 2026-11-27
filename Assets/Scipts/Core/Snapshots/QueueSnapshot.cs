using System;

namespace Zvezda57.Core
{
    [Serializable]
    public sealed class QueueSnapshot
    {
        public string CaseId;
        public IntakeState State;
    }
}
