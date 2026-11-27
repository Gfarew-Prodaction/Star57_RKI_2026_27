using System.Collections.Generic;

namespace Zvezda57.Core
{
    public interface IDecisionStore
    {
        IReadOnlyList<DecisionRecord> Records { get; }

        void Append(DecisionRecord record);
        void Clear();
    }
}
