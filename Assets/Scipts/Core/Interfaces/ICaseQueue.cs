using System;
using System.Collections.Generic;
using Zvezda57.Documents;

namespace Zvezda57.Core
{
    public interface ICaseQueue
    {
        CaseDefinition Current { get; }

        IntakeState State { get; }

        int Remaining { get; }

        event Action Changed;
        void SetCases(IReadOnlyList<CaseDefinition> content);
        QueueSnapshot Capture();
        void Restore(QueueSnapshot snapshot);
        bool OfferNext();
        bool Receive();
        bool File();
    }
}
