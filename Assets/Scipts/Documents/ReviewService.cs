using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zvezda57.Core;

namespace Zvezda57.Documents
{
    public sealed class ReviewService : IReviewService
    {
        private readonly ICaseQueue _queue;
        private readonly IDecisionStore _store;
        private readonly Dictionary<int, CaseVerdict> _stamps = new Dictionary<int, CaseVerdict>();
        private readonly HashSet<string> _evidence = new HashSet<string>();
        private CaseVerdict? _verdict;
        public CaseVerdict? PendingVerdict => _queue.State == IntakeState.Received ? _verdict : null;
        public CaseVerdict? SelectedStamp { get; private set; }
        public bool Inked { get; private set; }
        public InspectionTool Tool { get; private set; }
        public bool ToolsUnlocked { get; set; } = true;

        public ReviewSnapshot Capture() => new ReviewSnapshot
        {
            HasSelectedStamp = SelectedStamp.HasValue,
            SelectedStamp = SelectedStamp ?? default,
            HasVerdict = _verdict.HasValue,
            Verdict = _verdict ?? default,
            Inked = Inked,
            Tool = Tool,
            Evidence = new List<string>(_evidence),
            Stamps = _stamps.Select(stamp => new StampSnapshot { Page = stamp.Key, Verdict = stamp.Value }).ToList()
        };
        public void Restore(ReviewSnapshot snapshot)
        {
            Reset();
            if (snapshot == null)
            {
                return;
            }

            SelectedStamp = snapshot.HasSelectedStamp ? snapshot.SelectedStamp : (CaseVerdict?)null;
            _verdict = snapshot.HasVerdict ? snapshot.Verdict : (CaseVerdict?)null;
            Inked = snapshot.Inked && SelectedStamp.HasValue;
            Tool = ToolsUnlocked ? snapshot.Tool : InspectionTool.None;
            if (_queue.State != IntakeState.Received)
            {
                _verdict = null;
                return;
            }

            foreach (var item in snapshot.Evidence ?? new List<string>())
            {
                _evidence.Add(item);
            }

            foreach (var item in snapshot.Stamps ?? new List<StampSnapshot>())
            {
                if (item.Page >= 0 && item.Page < _queue.Current.Documents.Length)
                {
                    _stamps[item.Page] = item.Verdict;
                }
            }
        }

        public bool CanSubmit => PendingVerdict.HasValue;

        public ReviewService(ICaseQueue queue, IDecisionStore store)
        {
            _queue = queue;
            _store = store;
        }

        public void Reset()
        {
            _stamps.Clear();
            _evidence.Clear();
            _verdict = null;
            SelectedStamp = null;
            Inked = false;
            Tool = InspectionTool.None;
        }

        public void SelectStamp(CaseVerdict value)
        {
            SelectedStamp = value;
            Inked = false;
            Tool = InspectionTool.None;
        }

        public bool Ink()
        {
            Inked = SelectedStamp.HasValue;
            return Inked;
        }

        public bool Stamp(int page)
        {
            if (!Inked || !TryGetPage(page, out _))
            {
                return false;
            }

            var verdict = SelectedStamp.Value;
            _verdict = verdict;
            _stamps[page] = verdict;
            Inked = false;
            return true;
        }

        public CaseVerdict? GetStamp(int page) => _stamps.TryGetValue(page, out var value) ? value : (CaseVerdict?)null;
        public bool SelectTool(InspectionTool tool)
        {
            if (tool != InspectionTool.None && !ToolsUnlocked)
            {
                return false;
            }

            Tool = tool;
            SelectedStamp = null;
            Inked = false;
            return true;
        }

        public string Scan(int page, Vector2 point)
        {
            if (!ToolsUnlocked || Tool == InspectionTool.None || !TryGetPage(page, out var document))
            {
                return "";
            }

            foreach (var item in document.Evidence)
            {
                if (item.Tool == Tool && item.Region.Contains(point))
                {
                    _evidence.Add(item.Id);
                    return item.Finding;
                }
            }

            return Tool == InspectionTool.Ultraviolet ? "УФ: в этой области дополнительных следов нет." : "Лупа: в этой области дополнительных деталей нет.";
        }

        private bool TryGetPage(int page, out DocumentContent document)
        {
            if (_queue.State == IntakeState.Received && page >= 0 && page < _queue.Current.Documents.Length)
            {
                document = _queue.Current.Documents[page];
                return true;
            }

            document = null;
            return false;
        }

        public string Call(string number)
        {
            if (_queue.State != IntakeState.Received)
            {
                return "Сначала получите папку: проверка привязана к текущему делу.";
            }

            foreach (var response in _queue.Current.PhoneResponses)
            {
                if (response.Number == number)
                {
                    if (!string.IsNullOrEmpty(response.EvidenceId))
                    {
                        _evidence.Add(response.EvidenceId);
                    }

                    return response.Text;
                }
            }

            return "Абонент не подтвердил сведения по этому делу.";
        }

        public DecisionRecord Submit()
        {
            if (PendingVerdict is not CaseVerdict verdict)
            {
                return null;
            }

            var currentCase = _queue.Current;
            var rule = Array.Find(currentCase.Verdicts, candidate => candidate.Verdict == verdict);
            if (rule == null)
            {
                throw new InvalidOperationException("Missing verdict outcome for case " + currentCase.Id);
            }

            var record = new DecisionRecord
            {
                CaseId = currentCase.Id,
                Verdict = verdict,
                Accepted = rule.Accepted,
                Feedback = rule.Feedback,
                IncidentId = rule.IncidentId,
                Metrics = rule.Metrics,
                Evidence = _evidence.ToArray(),
                Timestamp = DateTime.UtcNow.ToString("O")
            };
            _store.Append(record);
            _queue.File();
            return record;
        }
    }
}
