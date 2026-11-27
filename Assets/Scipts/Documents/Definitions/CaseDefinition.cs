using UnityEngine;

namespace Zvezda57.Documents
{
    [CreateAssetMenu(menuName = "Zvezda57/Case")]
    public sealed class CaseDefinition : ScriptableObject
    {
        public bool DeliveredByYura;
        public string Id;
        public string VisitorName;
        [TextArea]
        public string Greeting;
        public Sprite Portrait;
        public Sprite SpeakingPortrait;
        public Sprite ReactionPortrait;
        public Sprite DocumentPhoto;
        public Color PortraitTint = new Color(.3f, .4f, .45f);
        public DocumentContent[] Documents;
        public VerdictRule[] Verdicts = new VerdictRule[0];
        public PhoneResponse[] PhoneResponses = new PhoneResponse[0];
    }
}
