using System;
using System.Collections.Generic;
using Zvezda57.Documents;

namespace Zvezda57.Core
{
    [Serializable]
    public sealed class ReviewSnapshot
    {
        public bool HasSelectedStamp, HasVerdict, Inked;
        public CaseVerdict SelectedStamp, Verdict;
        public InspectionTool Tool;
        public List<string> Evidence = new List<string>();
        public List<StampSnapshot> Stamps = new List<StampSnapshot>();
    }
}
