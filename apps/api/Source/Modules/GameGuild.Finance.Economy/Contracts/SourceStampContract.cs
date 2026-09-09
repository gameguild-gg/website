namespace GameGuild.Finance.Economy.Contracts;

public sealed record SourceStampContract
{
    public SourceStampContract(
        SourceStampId id,
        string evidenceHash,
        SourceConfirmationState state,
        DateTimeOffset observedAt,
        DateTimeOffset? confirmedAt,
        string? providerReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceHash);
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        var postConfirmation = state is SourceConfirmationState.Confirmed or
            SourceConfirmationState.Disputed or SourceConfirmationState.Reversed;
        if (postConfirmation && confirmedAt is null)
            throw new ArgumentException("Post-confirmation source stamps require a confirmation timestamp.", nameof(confirmedAt));
        if (!postConfirmation && confirmedAt is not null)
            throw new ArgumentException("Pre-confirmation source stamps cannot carry a confirmation timestamp.", nameof(confirmedAt));
        if (confirmedAt < observedAt)
            throw new ArgumentException("Confirmation cannot precede observation.", nameof(confirmedAt));

        Id = id;
        EvidenceHash = evidenceHash.Trim();
        State = state;
        ObservedAt = observedAt;
        ConfirmedAt = confirmedAt;
        ProviderReference = string.IsNullOrWhiteSpace(providerReference) ? null : providerReference.Trim();
    }

    public SourceStampId Id { get; }
    public string EvidenceHash { get; }
    public SourceConfirmationState State { get; }
    public DateTimeOffset ObservedAt { get; }
    public DateTimeOffset? ConfirmedAt { get; }
    public string? ProviderReference { get; }
}
