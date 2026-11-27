using System;
using System.Collections.Generic;

namespace Zvezda57.Core
{
    [Serializable]
    public sealed class DecisionJournal
    {
        public int Version = 1;
        public List<DecisionRecord> Decisions = new List<DecisionRecord>();
    }
}
