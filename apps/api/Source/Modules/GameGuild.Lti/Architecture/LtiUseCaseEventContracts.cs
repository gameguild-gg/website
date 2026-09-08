using GameGuild;

[assembly: UseCaseEventContract(typeof(GameGuild.Lti.LaunchLtiCommand), "lti.launch", NoDomainEventReason = "LTI launch and user-mapping changes are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(GameGuild.Lti.CreateLtiDeploymentCommand), "lti.deployments.create", NoDomainEventReason = "LTI deployment creation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(GameGuild.Lti.CreateLtiLineItemCommand), "lti.line-items.create", NoDomainEventReason = "LTI line-item creation is observed through the durable generic operation event.")]
