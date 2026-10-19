namespace GameGuild.Learning.Certificates;

/// <summary>Stages durable auxiliary-evidence intents before the certificate transaction commits.</summary>
/// <remarks>Implementations only stage local integration events. Network calls and signing belong to asynchronous consumers.</remarks>
public interface ICertificateDurableEvidenceProducer
{
    void StageIssued(Certificate certificate);
    void StageRevoked(Certificate certificate);
}

public sealed class NoOpCertificateDurableEvidenceProducer : ICertificateDurableEvidenceProducer
{
    public static NoOpCertificateDurableEvidenceProducer Instance { get; } = new();
    private NoOpCertificateDurableEvidenceProducer() { }
    public void StageIssued(Certificate certificate) => ArgumentNullException.ThrowIfNull(certificate);
    public void StageRevoked(Certificate certificate) => ArgumentNullException.ThrowIfNull(certificate);
}
