using System;
using System.Collections.Generic;

namespace Zvezda57.Systems
{
    [Serializable]
    public sealed class CityMetricsState
    {
        public int Reactor, Secrecy, Calm;
        public List<string> Tokens = new List<string>(), History = new List<string>();
    }
}
