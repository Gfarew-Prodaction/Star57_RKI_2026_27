using System;

namespace Zvezda57.Systems
{
    [Serializable]
    public struct MetricDelta
    {
        public int Reactor, Secrecy, Calm;
        public MetricDelta(int reactor, int secrecy, int calm)
        {
            Reactor = reactor;
            Secrecy = secrecy;
            Calm = calm;
        }
    }
}
