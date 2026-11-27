using UnityEngine;

namespace Zvezda57.Systems
{
    [CreateAssetMenu(menuName = "Zvezda57/Reference book")]
    public sealed class ReferenceDefinition : ScriptableObject
    {
        public string[] Tabs;
        [TextArea(4, 20)]
        public string[] Pages;
    }
}
