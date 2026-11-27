using System.Collections.Generic;
using Zvezda57.Dispatch;

namespace Zvezda57.Core
{
    public sealed class ConfigurationValidationResult
    {
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<IncidentDefinition> Incidents { get; }
        public bool IsValid => Errors.Count == 0;

        internal ConfigurationValidationResult(List<string> errors, IEnumerable<IncidentDefinition> incidents)
        {
            Errors = errors.AsReadOnly();
            Incidents = new List<IncidentDefinition>(incidents).AsReadOnly();
        }
    }
}
