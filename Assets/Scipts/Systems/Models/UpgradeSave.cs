using System;
using System.Collections.Generic;

namespace Zvezda57.Systems
{
    [Serializable]
    public sealed class UpgradeSave
    {
        public List<string> Purchased = new List<string>();
        public List<string> Rewards = new List<string>();
        public int Points;
    }
}
