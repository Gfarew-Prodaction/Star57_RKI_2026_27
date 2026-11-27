using UnityEngine;

namespace Zvezda57.Systems
{
    [CreateAssetMenu(menuName = "Zvezda57/Shift")]
    public sealed class ShiftDefinition : ScriptableObject
    {
        public string Title;
        public string[] CaseIds;
        public ScheduledIncident[] Incidents;
        public DialogueDefinition Intro;
        public int Reward;
    }
}
