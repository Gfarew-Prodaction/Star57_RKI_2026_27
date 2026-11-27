using System;

namespace Zvezda57.Systems
{
    public interface IYuraAssignments
    {
        YuraTask Task { get; }
        float TaskRemaining { get; }

        event Action Changed;

        bool Assign(YuraTask task, out string message);
    }
}
