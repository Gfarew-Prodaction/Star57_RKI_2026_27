using System;

namespace Zvezda57.Documents
{
    [Serializable]
    public sealed class VerdictRule
    {
        public CaseVerdict Verdict;
        public bool Accepted;
        public string Feedback;
        public string IncidentId;
        public Zvezda57.Systems.MetricDelta Metrics;
    }
}
