using TMPro;
using UnityEngine;

namespace Zvezda57.Dispatch
{
    public sealed class SectorSocket : MonoBehaviour
    {
        [Range(1, 6)]
        public int Sector;
        [SerializeField]
        private Renderer indicator;

        [SerializeField]
        private TMP_Text label;

        public TMP_Text Label => label;
        public Renderer Indicator => indicator;
    }
}
