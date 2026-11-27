using UnityEngine;

namespace Zvezda57.Systems
{
    [CreateAssetMenu(menuName = "Zvezda57/Ending")]
    public sealed class EndingDefinition : ScriptableObject
    {
        public EndingKind Kind;
        public string Title;
        [TextArea(4, 15)]
        public string Text;
    }
}
