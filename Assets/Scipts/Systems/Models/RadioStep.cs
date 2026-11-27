using System;
using UnityEngine;

namespace Zvezda57.Systems
{
    [Serializable]
    public sealed class RadioStep
    {
        [TextArea]
        public string Prompt;
        public string[] Options;
        public int CorrectOption;
    }
}
