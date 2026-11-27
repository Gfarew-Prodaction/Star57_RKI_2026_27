using UnityEngine;

namespace Zvezda57.Documents
{
    [System.Serializable]
    public sealed class DocumentContent
    {
        public string Title;
        public DocumentForm Form;
        [TextArea(5, 15)]
        public string Body;
        public EvidenceDefinition[] Evidence = new EvidenceDefinition[0];
    }
}
