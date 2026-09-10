using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Posting;
using System.Numerics;

namespace GameGuild.Finance.Economy.Writer;

public enum EconomyWriterRejectionCode
{
    UnauthorizedTemplateShape = 1,
    AbsentSourceStamp = 2,
    ReusedSourceStamp = 3,
    MutatedSourceStamp = 4,
    UnconfirmedExternalMint = 5,
    ProviderOverCredit = 6,
    ForgedConfirmationTime = 7,
    EarlyMaturity = 8,
    OverAllocation = 9,
    OverlappingRootRange = 10,
    StaleReversalEpoch = 11,
    LineageNonConservation = 12
}

public sealed record EconomyWriterValidationError(EconomyWriterRejectionCode Code, string Message);

public sealed record EconomyWriterValidationResult(IReadOnlyList<EconomyWriterValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed class EconomyWriterContractException : InvalidOperationException
{
    public EconomyWriterContractException(IReadOnlyList<EconomyWriterValidationError> errors)
        : base(string.Join("; ", errors.Select(error => $"{error.Code}: {error.Message}"))) => Errors = errors;

    public IReadOnlyList<EconomyWriterValidationError> Errors { get; }
}

public sealed record WriterSourceFact(
    bool Exists,
    bool AlreadyMinted,
    string PersistedHash,
    string SubmittedHash,
    SourceConfirmationState State,
    DateTimeOffset AuthoritativeConfirmedAt,
    DateTimeOffset SubmittedConfirmedAt,
    long ProviderAuthoritativeUnits,
    long ProviderPreviouslyCreditedUnits);

public sealed record WriterAllocationFact(long RequestedUnits, long AvailableUnits);

public sealed record WriterLineageFact(CurrencyCode Currency, long InputUnits, long OutputUnits);

public sealed record WriterReversalFenceFact(long CapturedEpoch, long CurrentEpoch);

public sealed record WriterMaturityFact(
    CurrencyCode Currency,
    ProvenanceKind Provenance,
    DateTimeOffset ConfirmedAt,
    DateTimeOffset MaturesAt);

public sealed record EconomyWriterValidationRequest(
    PostingRequest Posting,
    WriterSourceFact? Source,
    IReadOnlyList<WriterAllocationFact> Allocations,
    IReadOnlyList<RootTraceRange> InputRanges,
    IReadOnlyList<RootTraceRange> OutputRanges,
    IReadOnlyList<WriterLineageFact> Lineage,
    WriterReversalFenceFact? ReversalFence,
    WriterMaturityFact? Maturity);

public static class EconomyWriterContractValidator
{
    public const int EarnedHardMaturityDays = 120;

    public static EconomyWriterValidationResult Validate(EconomyWriterValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<EconomyWriterValidationError>();
        ValidatePosting(request.Posting, errors);
        ValidateSource(request, errors);
        ValidateMaturity(request.Maturity, errors);
        ValidateAllocations(request.Allocations, errors);
        ValidateRootRanges(request.OutputRanges, errors);
        ValidateReversalFence(request.ReversalFence, errors);
        ValidateLineage(request.Lineage, errors);
        return new EconomyWriterValidationResult(errors);
    }

    public static void EnsureValid(EconomyWriterValidationRequest request)
    {
        var result = Validate(request);
        if (!result.IsValid) throw new EconomyWriterContractException(result.Errors);
    }

    private static void ValidatePosting(
        PostingRequest posting,
        ICollection<EconomyWriterValidationError> errors)
    {
        if (!PostingMatrix.Validate(posting).IsValid)
            Add(errors, EconomyWriterRejectionCode.UnauthorizedTemplateShape,
                "The posting does not match a registered immutable template.");
    }

    private static void ValidateSource(
        EconomyWriterValidationRequest request,
        ICollection<EconomyWriterValidationError> errors)
    {
        var registration = PostingTemplateCatalog.Find(request.Posting.Template.Kind, request.Posting.Template.Version);
        if (registration?.RequiredSourceState is null) return;

        var source = request.Source;
        if (source is null || !source.Exists)
        {
            Add(errors, EconomyWriterRejectionCode.AbsentSourceStamp,
                "A persisted provider source stamp is required.");
            return;
        }

        if (source.AlreadyMinted)
            Add(errors, EconomyWriterRejectionCode.ReusedSourceStamp,
                "The provider source stamp has already funded a mint.");
        if (!string.Equals(source.PersistedHash, source.SubmittedHash, StringComparison.Ordinal))
            Add(errors, EconomyWriterRejectionCode.MutatedSourceStamp,
                "Submitted provider evidence differs from the persisted source stamp.");
        if (source.State != registration.RequiredSourceState.Value)
            Add(errors, EconomyWriterRejectionCode.UnconfirmedExternalMint,
                "External value cannot mint currency before provider confirmation.");

        var requestedUnits = request.Allocations.Aggregate(
            BigInteger.Zero,
            (total, allocation) => total + allocation.RequestedUnits);
        var cumulativeUnits = new BigInteger(source.ProviderPreviouslyCreditedUnits) + requestedUnits;
        if (source.ProviderAuthoritativeUnits < 0 || source.ProviderPreviouslyCreditedUnits < 0 ||
            cumulativeUnits > source.ProviderAuthoritativeUnits)
            Add(errors, EconomyWriterRejectionCode.ProviderOverCredit,
                "Cumulative minted units exceed the provider-authoritative amount.");
        if (source.SubmittedConfirmedAt != source.AuthoritativeConfirmedAt)
            Add(errors, EconomyWriterRejectionCode.ForgedConfirmationTime,
                "The submitted confirmation timestamp is not provider-authoritative.");
    }

    private static void ValidateMaturity(
        WriterMaturityFact? maturity,
        ICollection<EconomyWriterValidationError> errors)
    {
        if (maturity is not { Currency: CurrencyCode.HardCoin, Provenance: ProvenanceKind.EarnedHard }) return;

        if (maturity.MaturesAt - maturity.ConfirmedAt != TimeSpan.FromDays(EarnedHardMaturityDays))
            Add(errors, EconomyWriterRejectionCode.EarlyMaturity,
                "Earned hard currency must mature exactly 120 days after confirmation.");
    }

    private static void ValidateAllocations(
        IReadOnlyList<WriterAllocationFact> allocations,
        ICollection<EconomyWriterValidationError> errors)
    {
        if (allocations.Any(allocation => allocation.RequestedUnits <= 0 || allocation.AvailableUnits < 0 ||
                                          allocation.RequestedUnits > allocation.AvailableUnits))
            Add(errors, EconomyWriterRejectionCode.OverAllocation,
                "A posting cannot consume more units than its parent fragment contains.");
    }

    private static void ValidateRootRanges(
        IReadOnlyList<RootTraceRange> outputRanges,
        ICollection<EconomyWriterValidationError> errors)
    {
        foreach (var rootRanges in outputRanges.GroupBy(range => (range.Root, range.Epoch)))
        {
            var ordered = rootRanges.OrderBy(range => range.Start).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                if (ordered[index].Start >= ordered[index - 1].EndExclusive) continue;

                Add(errors, EconomyWriterRejectionCode.OverlappingRootRange,
                    "A root trace interval cannot be owned by more than one output fragment.");
                return;
            }
        }
    }

    private static void ValidateReversalFence(
        WriterReversalFenceFact? fence,
        ICollection<EconomyWriterValidationError> errors)
    {
        if (fence is not null && fence.CapturedEpoch != fence.CurrentEpoch)
            Add(errors, EconomyWriterRejectionCode.StaleReversalEpoch,
                "The root reversal epoch changed after fragment selection.");
    }

    private static void ValidateLineage(
        IReadOnlyList<WriterLineageFact> lineage,
        ICollection<EconomyWriterValidationError> errors)
    {
        if (lineage.Any(fact => fact.InputUnits < 0 || fact.OutputUnits < 0 || fact.InputUnits != fact.OutputUnits))
            Add(errors, EconomyWriterRejectionCode.LineageNonConservation,
                "Lineage input and output units must conserve value per currency.");
    }

    private static void Add(
        ICollection<EconomyWriterValidationError> errors,
        EconomyWriterRejectionCode code,
        string message) => errors.Add(new EconomyWriterValidationError(code, message));
}
