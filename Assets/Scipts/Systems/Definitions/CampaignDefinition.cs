using UnityEngine;

namespace Zvezda57.Systems
{
    [CreateAssetMenu(menuName = "Zvezda57/Campaign")]
    public sealed class CampaignDefinition : ScriptableObject
    {
        public ShiftDefinition[] Shifts;
        public DialogueDefinition[] Story;
        public EndingDefinition[] Endings;
        public string FinaleIncidentId = "3.C";
        public string CriticalIncidentId = "3.A";
        public int ReactorThreshold = 60;
        public float YuraTaskSeconds = 8;
        public float IncomingCallSeconds = 10;
        public float RadioSeconds = 35;
        public RadioStep[] RadioSteps = new RadioStep[0];
    }
}
