using GameGuild;

[assembly: UseCaseEventContract(typeof(GameGuild.Learning.Certificates.IssueCertificateEndpointCommand), "learning.certificates.issue", NoDomainEventReason = "Certificate issuance is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(GameGuild.Learning.Certificates.RevokeCertificateEndpointCommand), "learning.certificates.revoke", NoDomainEventReason = "Certificate revocation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(GameGuild.Learning.Certificates.CreateCertificateTemplateEndpointCommand), "learning.certificates.templates.create", NoDomainEventReason = "Certificate-template creation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(GameGuild.Learning.Certificates.UpdateCertificateTemplateEndpointCommand), "learning.certificates.templates.update", NoDomainEventReason = "Certificate-template updates are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(GameGuild.Learning.Certificates.DeleteCertificateTemplateEndpointCommand), "learning.certificates.templates.delete", NoDomainEventReason = "Certificate-template deletion is observed through the durable generic operation event.")]
