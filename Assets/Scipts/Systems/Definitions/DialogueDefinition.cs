using UnityEngine;

namespace Zvezda57.Systems
{
    [CreateAssetMenu(menuName = "Zvezda57/Dialogue")]
    public sealed class DialogueDefinition : ScriptableObject
    {
        public string Id, Speaker;
        [TextArea(3, 12)]
        public string Text;
        public DialogueChoice[] Choices;
    }
}
