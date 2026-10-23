using GameGuild;
using GameGuild.Commerce.Products;

// Pricing A/B experiments and automated scheduling (issue #395): the mutating commands
// declare no named cross-module events; the durable generic operation event remains the
// observable contract for each, matching the pricing-rule command family.
[assembly: UseCaseEventContractAttribute(
    typeof(CreatePricingExperimentCommand),
    "commerce.products.create-pricing-experiment",
    NoDomainEventReason = "No named cross-module event is declared; the durable generic operation event remains the observable contract.")]

[assembly: UseCaseEventContractAttribute(
    typeof(TransitionPricingExperimentCommand),
    "commerce.products.transition-pricing-experiment",
    NoDomainEventReason = "No named cross-module event is declared; the durable generic operation event remains the observable contract.")]

[assembly: UseCaseEventContractAttribute(
    typeof(AssignPricingExperimentCommand),
    "commerce.products.assign-pricing-experiment",
    NoDomainEventReason = "No named cross-module event is declared; the durable generic operation event remains the observable contract.")]

[assembly: UseCaseEventContractAttribute(
    typeof(RecordPricingExperimentOutcomeCommand),
    "commerce.products.record-pricing-experiment-outcome",
    NoDomainEventReason = "No named cross-module event is declared; the durable generic operation event remains the observable contract.")]

[assembly: UseCaseEventContractAttribute(
    typeof(RunPricingScheduleSweepCommand),
    "commerce.products.run-pricing-schedule-sweep",
    NoDomainEventReason = "No named cross-module event is declared; the durable generic operation event remains the observable contract.")]
