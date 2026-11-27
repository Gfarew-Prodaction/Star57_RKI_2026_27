using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zvezda57.Dispatch;
using Zvezda57.Documents;
using Zvezda57.Systems;

namespace Zvezda57.Core
{
    internal sealed class GameConfigurationValidator
    {
        private const int SectorCount = 6;
        private readonly List<string> _errors = new List<string>();
        private readonly Dictionary<string, IncidentDefinition> _incidents = new Dictionary<string, IncidentDefinition>(StringComparer.Ordinal);

        public static ConfigurationValidationResult Validate(
            CaseDefinition[] cases, IncidentDefinition[] incidents, ReferenceDefinition reference,
            UpgradeDefinition[] upgrades, CampaignDefinition campaign)
        {
            var validator = new GameConfigurationValidator();
            validator.CollectIncidents(incidents, campaign);
            validator.ValidateIncidents();
            var caseIds = validator.ValidateCases(cases);
            validator.ValidateReference(reference);
            validator.ValidateUpgrades(upgrades);
            validator.ValidateCampaign(campaign, caseIds);
            return new ConfigurationValidationResult(validator._errors, validator._incidents.Values);
        }

        private void CollectIncidents(IncidentDefinition[] incidents, CampaignDefinition campaign)
        {
            var entries = Items(incidents, "OfficeLifetimeScope.Incidents");
            for (int index = 0; index < entries.Length; index++)
            {
                AddIncident(entries[index], $"OfficeLifetimeScope.Incidents[{index}]");
            }

            foreach (var shift in campaign?.Shifts ?? Array.Empty<ShiftDefinition>())
            {
                if (shift == null)
                {
                    continue;
                }

                foreach (var entry in shift.Incidents ?? Array.Empty<ScheduledIncident>())
                {
                    if (entry?.Incident != null)
                    {
                        AddIncident(entry.Incident, shift.name);
                    }
                }
            }
        }

        private void AddIncident(IncidentDefinition incident, string source)
        {
            if (incident == null)
            {
                _errors.Add($"{source}: assign an incident asset.");
                return;
            }

            if (!CheckId(incident.Id, $"{incident.name}.Id"))
            {
                return;
            }

            if (!_incidents.TryAdd(incident.Id, incident) && _incidents[incident.Id] != incident)
            {
                _errors.Add($"{source}: duplicate incident ID '{incident.Id}' on {_incidents[incident.Id].name} and {incident.name}.");
            }
        }

        private void ValidateIncidents()
        {
            foreach (var incident in _incidents.Values)
            {
                string path = incident.name;
                if (string.IsNullOrWhiteSpace(incident.Title) || string.IsNullOrWhiteSpace(incident.Description))
                {
                    _errors.Add($"{path}: fill in the incident card's Title and Description.");
                }

                if (incident.Sector < 1 || incident.Sector > SectorCount)
                {
                    _errors.Add($"{path}.Sector: enter a sector from 1 to {SectorCount}.");
                }

                CheckSeconds(incident.TravelTime, $"{path}.TravelTime", .1f);
                CheckSeconds(incident.ReturnTime, $"{path}.ReturnTime", .1f);
                CheckSeconds(incident.StartAfter, $"{path}.StartAfter", 0);
                CheckOptionalIncident(incident.OnFailureIncidentId, $"{path}.OnFailureIncidentId");
                if (incident.CutOfficePowerOnFailure && string.IsNullOrEmpty(incident.OnFailureIncidentId))
                {
                    _errors.Add($"{path}.OnFailureIncidentId: assign the incident that restores office power.");
                }

                if (incident.OnFailureIncidentId == incident.Id)
                {
                    _errors.Add($"{path}.OnFailureIncidentId: the incident references itself.");
                }

                var stages = Items(incident.Stages, $"{path}.Stages");
                if (stages.Length == 0)
                {
                    ValidateResponse(incident.AllowedCrews, incident.Deadline, incident.WorkTime, path);
                }

                for (int index = 0; index < stages.Length; index++)
                {
                    string stagePath = $"{path}.Stages[{index}]";
                    var stage = stages[index];
                    if (stage == null)
                    {
                        _errors.Add($"{stagePath}: fill in the stage.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(stage.Title))
                    {
                        _errors.Add($"{stagePath}.Title: enter a stage title.");
                    }

                    ValidateResponse(stage.AllowedCrews, stage.Deadline, stage.WorkTime, stagePath);
                }
            }
        }

        private void ValidateResponse(CrewType[] crews, float deadline, float workTime, string path)
        {
            if (crews == null || crews.Length == 0 || crews.Any(crew => !Enum.IsDefined(typeof(CrewType), crew)))
            {
                _errors.Add($"{path}.AllowedCrews: assign at least one valid crew type.");
            }

            CheckSeconds(deadline, $"{path}.Deadline", 1);
            CheckSeconds(workTime, $"{path}.WorkTime", .1f);
        }

        private HashSet<string> ValidateCases(CaseDefinition[] cases)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var entries = Items(cases, "OfficeLifetimeScope.Cases");
            for (int index = 0; index < entries.Length; index++)
            {
                var item = entries[index];
                if (item == null)
                {
                    _errors.Add($"OfficeLifetimeScope.Cases[{index}]: assign a case asset.");
                    continue;
                }

                CheckUniqueId(item.Id, $"{item.name}.Id", ids);
                var documents = Items(item.Documents, $"{item.name}.Documents");
                if (documents.Length == 0 || documents.Any(document => document == null || document.Evidence == null || document.Evidence.Any(evidence => evidence == null)))
                {
                    _errors.Add($"{item.name}.Documents: add documents and fill in any evidence entries.");
                }

                if (Items(item.PhoneResponses, $"{item.name}.PhoneResponses").Any(response => response == null))
                {
                    _errors.Add($"{item.name}.PhoneResponses: an empty response was found.");
                }

                var verdicts = Items(item.Verdicts, $"{item.name}.Verdicts");
                if (verdicts.Length != Enum.GetValues(typeof(CaseVerdict)).Length ||
                    verdicts.Any(rule => rule == null || !Enum.IsDefined(typeof(CaseVerdict), rule.Verdict)) ||
                    verdicts.Select(rule => rule.Verdict).Distinct().Count() != verdicts.Length)
                {
                    _errors.Add($"{item.name}.Verdicts: define exactly one result for each stamp.");
                }

                foreach (var rule in verdicts.Where(rule => rule != null))
                {
                    CheckOptionalIncident(rule.IncidentId, $"{item.name}.Verdicts[{rule.Verdict}].IncidentId");
                }
            }

            return ids;
        }

        private void ValidateReference(ReferenceDefinition reference)
        {
            if (reference == null)
            {
                _errors.Add("OfficeLifetimeScope.Reference: assign a reference book.");
                return;
            }

            if (reference.Tabs == null || reference.Pages == null || reference.Tabs.Length == 0 ||
                reference.Tabs.Length != reference.Pages.Length)
            {
                _errors.Add($"{reference.name}: each reference tab needs a page.");
            }
        }

        private void ValidateUpgrades(UpgradeDefinition[] upgrades)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var entries = Items(upgrades, "OfficeLifetimeScope.Upgrades");
            for (int index = 0; index < entries.Length; index++)
            {
                var upgrade = entries[index];
                if (upgrade == null)
                {
                    _errors.Add($"OfficeLifetimeScope.Upgrades[{index}]: assign an upgrade asset.");
                    continue;
                }

                CheckUniqueId(upgrade.Id, $"{upgrade.name}.Id", ids);
                if (upgrade.Cost < 1 || !Enum.IsDefined(typeof(UpgradeEffect), upgrade.Effect))
                {
                    _errors.Add($"{upgrade.name}: enter a positive cost and a valid effect.");
                }

                if (float.IsNaN(upgrade.Multiplier) || upgrade.Multiplier < .05f || upgrade.Multiplier > 1)
                {
                    _errors.Add($"{upgrade.name}.Multiplier: enter a value from 0.05 to 1.");
                }
            }
        }

        private void ValidateCampaign(CampaignDefinition campaign, HashSet<string> caseIds)
        {
            if (campaign == null)
            {
                _errors.Add("OfficeLifetimeScope.Campaign: assign a campaign asset.");
                return;
            }

            string path = campaign.name;
            CheckIncident(campaign.FinaleIncidentId, $"{path}.FinaleIncidentId");
            CheckIncident(campaign.CriticalIncidentId, $"{path}.CriticalIncidentId");
            if (campaign.FinaleIncidentId == campaign.CriticalIncidentId)
            {
                _errors.Add($"{path}: the finale and critical incident must have distinct IDs.");
            }

            if (campaign.ReactorThreshold < 0 || campaign.ReactorThreshold > 100)
            {
                _errors.Add($"{path}.ReactorThreshold: enter a value from 0 to 100.");
            }

            CheckSeconds(campaign.YuraTaskSeconds, $"{path}.YuraTaskSeconds", .1f);
            CheckSeconds(campaign.IncomingCallSeconds, $"{path}.IncomingCallSeconds", .1f);
            CheckSeconds(campaign.RadioSeconds, $"{path}.RadioSeconds", .1f);

            var shifts = Items(campaign.Shifts, $"{path}.Shifts");
            if (shifts.Length == 0)
            {
                _errors.Add($"{path}.Shifts: add at least one shift.");
            }

            var dialogueIds = new HashSet<string>(StringComparer.Ordinal);
            var scheduledIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < shifts.Length; index++)
            {
                var shift = shifts[index];
                if (shift == null)
                {
                    _errors.Add($"{path}.Shifts[{index}]: assign a shift asset.");
                    continue;
                }

                ValidateDialogue(shift.Intro, $"{shift.name}.Intro", dialogueIds);
                ValidateShift(shift, caseIds, scheduledIds);
            }

            if (scheduledIds.Contains(campaign.FinaleIncidentId))
            {
                _errors.Add($"{path}.FinaleIncidentId: the story triggers this incident. Remove it from the schedule.");
            }

            var story = Items(campaign.Story, $"{path}.Story");
            for (int index = 0; index < story.Length; index++)
            {
                ValidateDialogue(story[index], $"{path}.Story[{index}]", dialogueIds);
            }

            var endings = Items(campaign.Endings, $"{path}.Endings");
            if (endings.Length != Enum.GetValues(typeof(EndingKind)).Length ||
                endings.Any(ending => ending == null || !Enum.IsDefined(typeof(EndingKind), ending.Kind)) ||
                endings.Select(ending => ending.Kind).Distinct().Count() != endings.Length)
            {
                _errors.Add($"{path}.Endings: assign one asset for each ending.");
            }

            var radio = Items(campaign.RadioSteps, $"{path}.RadioSteps");
            if (radio.Length == 0 ||
                radio.Any(step => step?.Options == null || step.CorrectOption < 0 || step.CorrectOption >= step.Options.Length))
            {
                _errors.Add($"{path}.RadioSteps: add steps and a valid correct option index for each step.");
            }
        }

        private void ValidateShift(ShiftDefinition shift, HashSet<string> caseIds, HashSet<string> scheduledIds)
        {
            string path = shift.name;
            var shiftCases = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in Items(shift.CaseIds, $"{path}.CaseIds"))
            {
                CheckUniqueId(id, $"{path}.CaseIds", shiftCases);
                if (!caseIds.Contains(id))
                {
                    _errors.Add($"{path}.CaseIds: case '{id}' is missing from the catalog.");
                }
            }

            if (shift.Reward < 0)
            {
                _errors.Add($"{path}.Reward: enter a nonnegative reward.");
            }

            var schedule = Items(shift.Incidents, $"{path}.Incidents");
            for (int index = 0; index < schedule.Length; index++)
            {
                string entryPath = $"{path}.Incidents[{index}]";
                var entry = schedule[index];
                if (entry == null)
                {
                    _errors.Add($"{entryPath}: fill in the schedule entry.");
                    continue;
                }

                if (entry.Incident != null && !string.IsNullOrEmpty(entry.Id) && entry.Id != entry.IncidentId)
                {
                    _errors.Add($"{entryPath}: ID '{entry.Id}' does not match the selected asset ID '{entry.IncidentId}'.");
                }

                if (CheckIncident(entry.IncidentId, entryPath) && !scheduledIds.Add(entry.IncidentId))
                {
                    _errors.Add($"{entryPath}: incident '{entry.IncidentId}' is already scheduled in this campaign.");
                }

                CheckSeconds(entry.Delay, $"{entryPath}.Delay", 0);
            }
        }

        private void ValidateDialogue(DialogueDefinition dialogue, string path, HashSet<string> ids)
        {
            if (dialogue == null)
            {
                _errors.Add($"{path}: assign a dialogue asset.");
                return;
            }

            CheckUniqueId(dialogue.Id, $"{dialogue.name}.Id", ids);
            if (dialogue.Choices == null || dialogue.Choices.Length == 0 || dialogue.Choices.Any(choice => choice == null))
            {
                _errors.Add($"{dialogue.name}.Choices: add and fill in the response options.");
            }
        }

        private void CheckOptionalIncident(string id, string path)
        {
            if (!string.IsNullOrEmpty(id))
            {
                CheckIncident(id, path);
            }
        }

        private bool CheckIncident(string id, string path)
        {
            if (!CheckId(id, path))
            {
                return false;
            }

            if (_incidents.ContainsKey(id))
            {
                return true;
            }

            _errors.Add($"{path}: incident '{id}' is missing from the catalog.");
            return false;
        }

        private bool CheckId(string id, string path)
        {
            if (!string.IsNullOrWhiteSpace(id) && id == id.Trim())
            {
                return true;
            }

            _errors.Add($"{path}: enter an ID without leading or trailing spaces.");
            return false;
        }

        private void CheckUniqueId(string id, string path, HashSet<string> ids)
        {
            if (CheckId(id, path) && !ids.Add(id))
            {
                _errors.Add($"{path}: duplicate ID '{id}'.");
            }
        }

        private void CheckSeconds(float value, string path, float minimum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum)
            {
                _errors.Add($"{path}: enter a finite duration of at least {minimum} seconds.");
            }
        }

        private T[] Items<T>(T[] values, string path)
        {
            if (values != null)
            {
                return values;
            }

            _errors.Add($"{path}: the list is missing.");
            return Array.Empty<T>();
        }
    }
}
