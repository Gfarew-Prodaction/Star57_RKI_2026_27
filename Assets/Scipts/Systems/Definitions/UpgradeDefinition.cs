using UnityEngine;

namespace Zvezda57.Systems
{
    [CreateAssetMenu(menuName = "Zvezda57/Upgrade")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        public string Id, Label;
        [TextArea]
        public string Description;
        [Min(1)]
        public int Cost = 1;
        public UpgradeEffect Effect;
        [Range(.05f, 1)]
        public float Multiplier = 1;
    }
}
