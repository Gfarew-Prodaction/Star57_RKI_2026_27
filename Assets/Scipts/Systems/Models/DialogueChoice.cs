using System;

namespace Zvezda57.Systems
{
    [Serializable]
    public sealed class DialogueChoice
    {
        public string Text;
        public int TrustDelta;
        public string SetFlag;
        public int MinimumTrust = -100;
        public string RequiredFlag;
    }
}
