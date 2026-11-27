using System;

namespace Zvezda57.Documents
{
    [Serializable]
    public sealed class EvidenceDefinition
    {
        public string Id;
        public InspectionTool Tool;
        public UnityEngine.Rect Region = new UnityEngine.Rect(0, 0, 1, 1);
        [UnityEngine.TextArea]
        public string Finding;
    }
}
