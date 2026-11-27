using System;

namespace Zvezda57.Documents
{
    [Serializable]
    public sealed class PhoneResponse
    {
        public string Number;
        public string EvidenceId;
        [UnityEngine.TextArea]
        public string Text;
    }
}
