using System;
using System.Collections.Generic;

namespace Zvezda57.Systems
{
    public interface ICityMetrics
    {
        int Reactor { get; }

        int Secrecy { get; }

        int Calm { get; }

        IReadOnlyList<string> History { get; }

        CityMetricsState Capture();
        void Restore(CityMetricsState state);
        event Action Changed;
        bool Apply(string token, string reason, MetricDelta delta);
    }
}
