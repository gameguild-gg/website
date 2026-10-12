using GameGuild.Commerce.Payments;
using GameGuild.Commerce.Subscriptions.Services.Email.Renderers;
using GameGuild.CQRS;
using GameGuild.Notifications.Services.Email;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Dependency injection configuration for the Subscriptions module
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    ///     Configures and registers all dependencies for the Subscriptions module
    /// </summary>
    /// <param name="services">The service collection to register dependencies with</param>
    /// <returns>The configured service collection</returns>
    public static IServiceCollection AddSubscriptionsModule(this IServiceCollection services)
    {
        // Issue #416 plan-management services: slug generation (create/full-update when the
        // client omits the slug) and the 5-minute public plan-list cache. Both are
        // stateless (the cache state lives in IMemoryCache), so singletons are safe and
        // keep cache entries shared across scopes.
        services.AddSingleton<ISubscriptionPlanSlugGenerator, SubscriptionPlanSlugGenerator>();
        services.AddSingleton<ISubscriptionPlanListCache, SubscriptionPlanListCache>();

        // Register Subscription Plan Service (required by sub-services)
        services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();

        // Register Subscription Notification Service (required by billing sub-service)
        services.AddScoped<ISubscriptionNotificationService, SubscriptionNotificationService>();

        // Register focused sub-services
        services.AddScoped<SubscriptionLifecycleService>();
        services.AddScoped<ISubscriptionLifecycleService>(sp => sp.GetRequiredService<SubscriptionLifecycleService>());

        services.AddScoped<SubscriptionBillingService>();
        services.AddScoped<ISubscriptionBillingService>(sp => sp.GetRequiredService<SubscriptionBillingService>());

        services.AddScoped<SubscriptionQueryAndExternalIdService>();
        services.AddScoped<ISubscriptionQueryService>(sp => sp.GetRequiredService<SubscriptionQueryAndExternalIdService>());
        services.AddScoped<ISubscriptionExternalIdService>(sp => sp.GetRequiredService<SubscriptionQueryAndExternalIdService>());
        services.AddScoped<ISubscriptionPaymentContextService>(sp => sp.GetRequiredService<SubscriptionQueryAndExternalIdService>());

        // Payment-method registry coordination (issue #414): implements the cross-module
        // contract for the Payments module (delete guard, re-pointing, checkout assignment).
        services.AddScoped<ISubscriptionPaymentMethodCoordination, SubscriptionPaymentMethodCoordinationService>();

        // Register SubscriptionService as thin facade for backward compatibility
        services.AddScoped<SubscriptionService>();

        // Register Plan Pricing Resolver for cross-module pricing lookups (Payments module integration)
        services.AddScoped<IPlanPricingResolver, SubscriptionPlanPricingResolver>();

        // Unified pricing engine seam (issue #417): expose the existing BillingCalculator
        // proration math to the Payments-owned engine abstraction.
        services.AddScoped<IPlanProrationCalculator, PlanProrationCalculator>();
        services.AddScoped<IBillingCalculator, BillingCalculator>();
        services.AddScoped<IPaymentSubscriptionSyncService, PaymentSubscriptionSyncService>();

        // Payment recovery lifecycle (issue #415): dunning suspension after retry exhaustion
        // and reason-guarded reactivation after a successful recovery payment.
        services.AddScoped<IPaymentDunningLifecycleService, PaymentDunningLifecycleService>();

        // Register Command Handlers (only existing ones)
        services.AddScoped<ICommandHandler<ActivateSubscriptionCommand>, ActivateSubscriptionCommandHandler>();
        services.AddScoped<IRequestHandler<ActivateSubscriptionCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<ActivateSubscriptionCommand>>());

        services.AddScoped<ICommandHandler<StartSubscriptionTrialCommand>, StartSubscriptionTrialCommandHandler>();
        services.AddScoped<IRequestHandler<StartSubscriptionTrialCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<StartSubscriptionTrialCommand>>());

        services.AddScoped<ICommandHandler<EndSubscriptionTrialCommand>, EndSubscriptionTrialCommandHandler>();
        services.AddScoped<IRequestHandler<EndSubscriptionTrialCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<EndSubscriptionTrialCommand>>());

        services.AddScoped<ICommandHandler<CancelSubscriptionCommand>, CancelSubscriptionCommandHandler>();
        services.AddScoped<IRequestHandler<CancelSubscriptionCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<CancelSubscriptionCommand>>());

        services.AddScoped<ICommandHandler<PauseSubscriptionCommand>, PauseSubscriptionHandler>();
        services.AddScoped<IRequestHandler<PauseSubscriptionCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<PauseSubscriptionCommand>>());

        services.AddScoped<ICommandHandler<ResumeSubscriptionCommand>, ResumeSubscriptionHandler>();
        services.AddScoped<IRequestHandler<ResumeSubscriptionCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<ResumeSubscriptionCommand>>());

        services.AddScoped<ICommandHandler<ReactivateSubscriptionCommand>, ReactivateSubscriptionCommandHandler>();
        services.AddScoped<IRequestHandler<ReactivateSubscriptionCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<ReactivateSubscriptionCommand>>());

        services.AddScoped<ICommandHandler<CreateSubscriptionCommand, Guid>, CreateSubscriptionCommandHandler>();
        services.AddScoped<IRequestHandler<CreateSubscriptionCommand, Guid>>(sp => sp.GetRequiredService<ICommandHandler<CreateSubscriptionCommand, Guid>>());

        services.AddScoped<ICommandHandler<RecordSubscriptionPaymentCommand, PaymentRecordResult>, RecordSubscriptionPaymentCommandHandler>();
        services.AddScoped<IRequestHandler<RecordSubscriptionPaymentCommand, PaymentRecordResult>>(sp => sp.GetRequiredService<ICommandHandler<RecordSubscriptionPaymentCommand, PaymentRecordResult>>());

        services.AddScoped<ICommandHandler<RecordSubscriptionPaymentFailureCommand>, RecordSubscriptionPaymentFailureCommandHandler>();
        services.AddScoped<IRequestHandler<RecordSubscriptionPaymentFailureCommand>>(sp => sp.GetRequiredService<ICommandHandler<RecordSubscriptionPaymentFailureCommand>>());

        services.AddScoped<ICommandHandler<SetSubscriptionAutoRenewCommand>, SetSubscriptionAutoRenewCommandHandler>();
        services.AddScoped<IRequestHandler<SetSubscriptionAutoRenewCommand>>(sp => sp.GetRequiredService<ICommandHandler<SetSubscriptionAutoRenewCommand>>());

        services.AddScoped<ICommandHandler<SuspendSubscriptionCommand>, SuspendSubscriptionCommandHandler>();
        services.AddScoped<IRequestHandler<SuspendSubscriptionCommand>>(sp => sp.GetRequiredService<ICommandHandler<SuspendSubscriptionCommand>>());

        services.AddScoped<ICommandHandler<UpdateSubscriptionMetadataCommand>, UpdateSubscriptionMetadataCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateSubscriptionMetadataCommand>>(sp => sp.GetRequiredService<ICommandHandler<UpdateSubscriptionMetadataCommand>>());

        services.AddScoped<ICommandHandler<SetSubscriptionExternalIdsCommand>, SetSubscriptionExternalIdsCommandHandler>();
        services.AddScoped<IRequestHandler<SetSubscriptionExternalIdsCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<SetSubscriptionExternalIdsCommand>>());

        services.AddScoped<ICommandHandler<ProcessSubscriptionRenewalCommand>, ProcessSubscriptionRenewalCommandHandler>();
        services.AddScoped<IRequestHandler<ProcessSubscriptionRenewalCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<ProcessSubscriptionRenewalCommand>>());

        services.AddScoped<ICommandHandler<UpgradeSubscriptionPlanCommand, SubscriptionUpgradeResult>, UpgradeSubscriptionPlanCommandHandler>();
        services.AddScoped<IRequestHandler<UpgradeSubscriptionPlanCommand, SubscriptionUpgradeResult>>(sp => sp.GetRequiredService<ICommandHandler<UpgradeSubscriptionPlanCommand, SubscriptionUpgradeResult>>());

        services.AddScoped<ICommandHandler<DowngradeSubscriptionPlanCommand, SubscriptionDowngradeResult>, DowngradeSubscriptionPlanCommandHandler>();
        services.AddScoped<IRequestHandler<DowngradeSubscriptionPlanCommand, SubscriptionDowngradeResult>>(sp => sp.GetRequiredService<ICommandHandler<DowngradeSubscriptionPlanCommand, SubscriptionDowngradeResult>>());

        services.AddScoped<ICommandHandler<ResendSubscriptionNotificationCommand, SubscriptionNotificationDto>, ResendSubscriptionNotificationCommandHandler>();
        services.AddScoped<IRequestHandler<ResendSubscriptionNotificationCommand, SubscriptionNotificationDto>>(sp => sp.GetRequiredService<ICommandHandler<ResendSubscriptionNotificationCommand, SubscriptionNotificationDto>>());

        // Register Query Handlers (only existing ones)
        services.AddScoped<IRequestHandler<GetActiveSubscriptionPlansQuery, IEnumerable<SubscriptionPlan>>, GetActiveSubscriptionPlansQueryHandler>();

        services.AddScoped<IQueryHandler<GetActiveTenantSubscriptionQuery, Subscription?>, GetActiveTenantSubscriptionQueryHandler>();
        services.AddScoped<IRequestHandler<GetActiveTenantSubscriptionQuery, Subscription?>>(sp => sp.GetRequiredService<IQueryHandler<GetActiveTenantSubscriptionQuery, Subscription?>>());

        services.AddScoped<IQueryHandler<GetPagedSubscriptionsQuery, PagedResult<Subscription>>, GetPagedSubscriptionsQueryHandler>();
        services.AddScoped<IRequestHandler<GetPagedSubscriptionsQuery, PagedResult<Subscription>>>(sp => sp.GetRequiredService<IQueryHandler<GetPagedSubscriptionsQuery, PagedResult<Subscription>>>());

        services.AddScoped<IQueryHandler<GetSubscriptionByIdQuery, Subscription?>, GetSubscriptionByIdQueryHandler>();
        services.AddScoped<IRequestHandler<GetSubscriptionByIdQuery, Subscription?>>(sp => sp.GetRequiredService<IQueryHandler<GetSubscriptionByIdQuery, Subscription?>>());

        services.AddScoped<IQueryHandler<GetTenantSubscriptionsQuery, IEnumerable<Subscription>>, GetTenantSubscriptionsQueryHandler>();
        services.AddScoped<IRequestHandler<GetTenantSubscriptionsQuery, IEnumerable<Subscription>>>(sp => sp.GetRequiredService<IQueryHandler<GetTenantSubscriptionsQuery, IEnumerable<Subscription>>>());

        services.AddScoped<IQueryHandler<GetSubscriptionNotificationsQuery, PagedResult<SubscriptionNotificationDto>>, GetSubscriptionNotificationsQueryHandler>();
        services.AddScoped<IRequestHandler<GetSubscriptionNotificationsQuery, PagedResult<SubscriptionNotificationDto>>>(sp => sp.GetRequiredService<IQueryHandler<GetSubscriptionNotificationsQuery, PagedResult<SubscriptionNotificationDto>>>());

        services.AddScoped<IQueryHandler<GetSubscriptionChurnReportQuery, SubscriptionChurnReportDto>, GetSubscriptionChurnReportQueryHandler>();
        services.AddScoped<IRequestHandler<GetSubscriptionChurnReportQuery, SubscriptionChurnReportDto>>(sp => sp.GetRequiredService<IQueryHandler<GetSubscriptionChurnReportQuery, SubscriptionChurnReportDto>>());

        // Register FluentValidation Validators (only existing ones)
        services.AddScoped<FluentValidation.IValidator<ActivateSubscriptionCommand>, ActivateSubscriptionCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<StartSubscriptionTrialCommand>, StartSubscriptionTrialCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<EndSubscriptionTrialCommand>, EndSubscriptionTrialCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<CancelSubscriptionCommand>, CancelSubscriptionCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<CreateSubscriptionCommand>, CreateSubscriptionCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<ReactivateSubscriptionCommand>, ReactivateSubscriptionCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<RecordSubscriptionPaymentCommand>, RecordSubscriptionPaymentCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<RecordSubscriptionPaymentFailureCommand>, RecordSubscriptionPaymentFailureCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<SetSubscriptionAutoRenewCommand>, SetSubscriptionAutoRenewCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<SetSubscriptionExternalIdsCommand>, SetSubscriptionExternalIdsCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<SuspendSubscriptionCommand>, SuspendSubscriptionCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<UpdateSubscriptionMetadataCommand>, UpdateSubscriptionMetadataCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<ProcessSubscriptionRenewalCommand>, ProcessSubscriptionRenewalCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<UpgradeSubscriptionPlanCommand>, UpgradeSubscriptionPlanCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<DowngradeSubscriptionPlanCommand>, DowngradeSubscriptionPlanCommandValidator>();

        // Register Query Validators (only existing ones)
        services.AddScoped<FluentValidation.IValidator<GetActiveTenantSubscriptionQuery>, GetActiveTenantSubscriptionQueryValidator>();
        services.AddScoped<FluentValidation.IValidator<GetSubscriptionByIdQuery>, GetSubscriptionByIdQueryValidator>();
        services.AddScoped<FluentValidation.IValidator<GetTenantSubscriptionsQuery>, GetTenantSubscriptionsQueryValidator>();

        // Register Repositories
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();

        services.AddHostedService<MonthlyStatementDispatchBackgroundService>();

        // Monthly statement email renderer: regenerates PDF/CSV artifacts at send time (REGEN-IS-CANONICAL).
        services.AddScoped<IEmailRenderer, MonthlyStatementRenderer>();

        return services;
    }
}
