using System;
using System.Collections.Generic;

namespace Zvezda57.Systems
{
    public interface IUpgradeService
    {
        IReadOnlyList<UpgradeDefinition> Definitions { get; }

        int Points { get; }

        int DraftCost { get; }

        bool PurchasingAllowed { get; set; }

        bool Owns(string id);
        bool IsSelected(string id);
        bool Toggle(string id, out string message);
        bool Commit(out string message);
        void Cancel();
        void Reward(string token, int points);
        void Clear();
        event Action Changed;
    }
}
