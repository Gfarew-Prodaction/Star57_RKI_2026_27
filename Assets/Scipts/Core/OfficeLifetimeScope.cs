using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer.Unity;
using VContainer;
using Zvezda57.Dispatch;
using Zvezda57.Documents;
using Zvezda57.Systems;
using Zvezda57.UI;

namespace Zvezda57.Core
{
    public sealed class OfficeLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private CaseDefinition[] cases;
        [SerializeField]
        private IncidentDefinition[] incidents;
        [SerializeField]
        private ReferenceDefinition reference;
        [SerializeField]
        private UpgradeDefinition[] upgrades;
        [SerializeField]
        private CampaignDefinition campaign;
        public void ConfigureCampaign(CampaignDefinition content)
        {
            campaign = content;
        }

        public void ConfigureUpgrades(UpgradeDefinition[] content)
        {
            upgrades = content;
        }

        public void ConfigureFeatures(IncidentDefinition[] content, ReferenceDefinition book)
        {
            incidents = content;
            reference = book;
        }

        public void ConfigureCases(CaseDefinition[] content)
        {
            cases = content;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            var configuration = ValidateConfiguration();
            if (!configuration.IsValid)
            {
                Debug.LogWarning(FormatErrors(configuration), this);
                if (Application.isPlaying)
                {
                    StartCoroutine(ReturnToMenu());
                }

                return;
            }

            if (Application.isPlaying)
            {
                SceneNavigation.PrepareGameSave();
            }

            var path = SavePaths.Decisions;
            var store = new JsonDecisionStore(path);
            var queue = new CaseQueue(System.Array.Empty<CaseDefinition>());
            builder.RegisterInstance<IDecisionStore>(store);
            builder.RegisterInstance<ICaseQueue>(queue);
            var review = new ReviewService(queue, store);
            builder.RegisterInstance<IReviewService>(review);
            var dispatch = new DispatchService(configuration.Incidents);
            builder.RegisterInstance<IDispatchService>(dispatch);
            var metrics = new CityMetrics();
            builder.RegisterInstance<ICityMetrics>(metrics);
            var upgradeService = new UpgradeService(upgrades, dispatch, path + ".upgrades");
            builder.RegisterInstance<IUpgradeService>(upgradeService);
            builder.RegisterInstance<ICampaignService>(new CampaignService(campaign, cases, queue, store, dispatch, metrics, upgradeService, path + ".campaign", review));
            builder.RegisterInstance(reference);
            builder.RegisterComponent(GetComponent<OfficePrototype>());
            builder.RegisterBuildCallback(container => container.Resolve<OfficePrototype>().Initialize());
        }

        public ConfigurationValidationResult ValidateConfiguration()
        {
            var configuration = GameConfigurationValidator.Validate(cases, incidents, reference, upgrades, campaign);
            if (!configuration.IsValid)
            {
                return configuration;
            }

            var errors = new List<string>();
            var office = GetComponent<OfficePrototype>();
            var hud = GetComponent<CampaignHud>();
            var investigation = GetComponent<InvestigationHud>();
            var operations = GetComponent<OperationsHud>();
            var controller = GetComponent<InvestigationController>();
            foreach (var definition in cases)
            {
                CheckCapacity(errors, definition.name + ".Documents", definition.Documents.Length, office.DocumentCapacity);
            }

            foreach (var dialogue in campaign.Story.Concat(campaign.Shifts.Select(shift => shift.Intro)))
            {
                CheckCapacity(errors, dialogue.name + ".Choices", dialogue.Choices.Length, hud.DialogueCapacity);
            }

            foreach (var step in campaign.RadioSteps)
            {
                CheckCapacity(errors, "Campaign.RadioSteps.Options", step.Options.Length, hud.RadioCapacity);
            }

            CheckCapacity(errors, "Reference.Tabs", reference.Tabs.Length, investigation.ReferenceCapacity);
            CheckCapacity(errors, "OfficeLifetimeScope.Upgrades", upgrades.Length, operations.UpgradeCapacity);
            int reserveCrews = upgrades.Count(upgrade => upgrade.Effect == UpgradeEffect.ExtraOps ||
                upgrade.Effect == UpgradeEffect.ExtraFire || upgrade.Effect == UpgradeEffect.ExtraRepair);
            CheckCapacity(errors, "DispatchPresenter.CrewSlots", 4 + reserveCrews, controller.CrewCapacity);
            return new ConfigurationValidationResult(errors, configuration.Incidents);
        }

        private static void CheckCapacity(List<string> errors, string data, int required, int capacity)
        {
            if (required > capacity)
            {
                errors.Add($"{data}: assign {required} scene slots in the Inspector ({capacity} available).");
            }
        }

        [ContextMenu("Validate Configuration")]
        private void PrintConfigurationValidation()
        {
            var result = ValidateConfiguration();
            if (result.IsValid)
            {
                Debug.Log($"Configuration valid. Incidents in catalog: {result.Incidents.Count}.", this);
                return;
            }

            Debug.LogWarning(FormatErrors(result), this);
        }

        private static string FormatErrors(ConfigurationValidationResult result)
        {
            return "Office configuration errors:\n• " + string.Join("\n• ", result.Errors);
        }

        private static IEnumerator ReturnToMenu()
        {
            yield return null;
            while (SceneNavigation.IsLoading)
            {
                yield return null;
            }

            SceneNavigation.RejectGameConfiguration("Не удалось начать смену. Проверьте настройки игры в Unity Console.");
        }
    }
}
