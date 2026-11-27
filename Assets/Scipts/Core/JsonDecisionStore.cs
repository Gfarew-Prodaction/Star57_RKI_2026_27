using System;
using System.Collections.Generic;

namespace Zvezda57.Core
{
    public sealed class JsonDecisionStore : IDecisionStore
    {
        private readonly JsonSaveFile<DecisionJournal> _file;
        private DecisionJournal _journal;
        public IReadOnlyList<DecisionRecord> Records { get; private set; }

        public JsonDecisionStore(string path)
        {
            _file = new JsonSaveFile<DecisionJournal>(path, "decision journal");
            SetJournal(_file.Load(saved => saved.Version == 1 && saved.Decisions != null)
                ?? new DecisionJournal());
        }

        public void Append(DecisionRecord record)
        {
            if (_journal.Decisions.Exists(x => x.CaseId == record.CaseId))
            {
                throw new InvalidOperationException("Case already resolved: " + record.CaseId);
            }

            var updated = new DecisionJournal
            {
                Decisions = new List<DecisionRecord>(_journal.Decisions)
            };
            updated.Decisions.Add(record);
            _file.Write(updated);
            SetJournal(updated);
        }

        public void Clear()
        {
            _file.Delete();
            SetJournal(new DecisionJournal());
        }

        private void SetJournal(DecisionJournal journal)
        {
            _journal = journal;
            Records = journal.Decisions.AsReadOnly();
        }
    }
}
