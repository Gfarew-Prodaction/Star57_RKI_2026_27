using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zvezda57.Systems
{
    public sealed class CityMetrics : ICityMetrics
    {
        private readonly HashSet<string> _applied = new HashSet<string>();
        private readonly List<string> _history = new List<string>();
        public int Reactor { get; private set; } = 75;
        public int Secrecy { get; private set; } = 75;
        public int Calm { get; private set; } = 75;
        public IReadOnlyList<string> History { get; }

        public CityMetrics()
        {
            History = _history.AsReadOnly();
        }

        public CityMetricsState Capture() => new CityMetricsState
        {
            Reactor = Reactor,
            Secrecy = Secrecy,
            Calm = Calm,
            Tokens = new List<string>(_applied),
            History = new List<string>(_history)
        };
        public void Restore(CityMetricsState state)
        {
            Reactor = Mathf.Clamp(state.Reactor, 0, 100);
            Secrecy = Mathf.Clamp(state.Secrecy, 0, 100);
            Calm = Mathf.Clamp(state.Calm, 0, 100);
            _applied.Clear();
            foreach (var token in state.Tokens ?? new List<string>())
            {
                _applied.Add(token);
            }

            _history.Clear();
            _history.AddRange(state.History ?? new List<string>());
            Changed?.Invoke();
        }

        public event Action Changed;
        public bool Apply(string token, string reason, MetricDelta delta)
        {
            if (!_applied.Add(token))
            {
                return false;
            }

            int r = Reactor, s = Secrecy, c = Calm;
            Reactor = Mathf.Clamp(r + delta.Reactor, 0, 100);
            Secrecy = Mathf.Clamp(s + delta.Secrecy, 0, 100);
            Calm = Mathf.Clamp(c + delta.Calm, 0, 100);
            _history.Add(reason + $" · Реактор {Reactor - r:+0;-0;0}, секретность {Secrecy - s:+0;-0;0}, спокойствие {Calm - c:+0;-0;0}");
            Changed?.Invoke();
            return true;
        }
    }
}
