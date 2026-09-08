using GameGuild;
using GameGuild.Identity.Authentication;

[assembly: UseCaseEventContract(typeof(CreateStepUpChallengeCommand), "identity.authentication.create-step-up-challenge", NoDomainEventReason = "Security challenge creation is internal; the operation event excludes operation targets, payload hashes and authentication evidence.")]
[assembly: UseCaseEventContract(typeof(BeginStepUpWebAuthnCommand), "identity.authentication.begin-step-up-webauthn", NoDomainEventReason = "WebAuthn challenge state is internal and observed through the durable operation event.")]
[assembly: UseCaseEventContract(typeof(VerifyStepUpChallengeCommand), "identity.authentication.verify-step-up-challenge", NoDomainEventReason = "Security receipt issuance is internal and observed through the durable operation event without including credentials or receipts.")]
