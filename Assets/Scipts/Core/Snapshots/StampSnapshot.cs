using System;
using Zvezda57.Documents;

namespace Zvezda57.Core
{
    [Serializable]
    public sealed class StampSnapshot
    {
        public int Page;
        public CaseVerdict Verdict;
    }
}
