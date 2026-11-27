using System;
using Zvezda57.Documents;
using Zvezda57.Systems;

namespace Zvezda57.Core
{
    [Serializable]
    public sealed class DecisionRecord
    {
        public MetricDelta Metrics;
        public string CaseId;
        public CaseVerdict Verdict;
        public bool Accepted;
        public string Feedback;
        public string IncidentId;
        public string[] Evidence;
        public string Timestamp;
    }
}
