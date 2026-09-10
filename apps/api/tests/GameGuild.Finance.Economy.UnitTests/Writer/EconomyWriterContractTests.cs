using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.UnitTests.Posting;
using GameGuild.Finance.Economy.Writer;

namespace GameGuild.Finance.Economy.UnitTests.Writer;

public sealed class EconomyWriterContractTests
{
    private static readonly DateTimeOffset ConfirmedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProcedureContractIsVersionedSecurityDefinerAndHasNoCallerSelectedSql()
    {
        var contract = EconomyWriterProcedureContract.V1;

        contract.Schema.Should().Be("economy_private");
        contract.Name.Should().Be("post_registered_posting_v1");
        contract.SecurityDefiner.Should().BeTrue();
        contract.OwnerRole.Should().Be(EconomyDatabaseRoles.ProcedureOwner);
        contract.ExecuteRole.Should().Be(EconomyDatabaseRoles.Writer);
        contract.PinnedSearchPath.Should().Be("pg_catalog,economy_private");
        contract.Inputs.Select(input => input.Name).Should().Equal(
            "p_capability_id", "p_actor_id", "p_tenant_id", "p_posting_id", "p_idempotency_key",
            "p_template_kind", "p_template_version", "p_authority", "p_policy_version", "p_reserve_version",
            "p_risk_decision_id", "p_risk_operation_fingerprint", "p_expected_counter_version",
            "p_source_stamp_id", "p_source_evidence_hash", "p_requested_at",
            "p_lines", "p_allocations", "p_root_ranges", "p_expected_reversal_epochs", "p_dispatch_snapshot_hash");
        contract.Inputs.Should().NotContain(input => input.Name.Contains("sql", StringComparison.OrdinalIgnoreCase));
        contract.Outputs.Select(output => output.Name).Should().Equal(
            "posting_id", "journal_sequence", "journal_hash", "duplicate");
        contract.CanonicalSignature.Should().Be(
            "economy_private.post_registered_posting_v1(uuid,uuid,uuid,uuid,text,integer,integer,integer,bigint,bigint,uuid,text,bigint,uuid,text,timestamptz,jsonb,jsonb,jsonb,jsonb,text)");
    }

    [Fact]
    public void DatabaseRolesSeparateMigrationRuntimeWriterAndProcedureOwnership()
    {
        EconomyDatabaseRoleCatalog.All.Select(role => role.Name).Should().BeEquivalentTo(
            EconomyDatabaseRoles.Migration,
            EconomyDatabaseRoles.Runtime,
            EconomyDatabaseRoles.Writer,
            EconomyDatabaseRoles.ProcedureOwner);

        EconomyDatabaseRoleCatalog.Find(EconomyDatabaseRoles.Migration)!.Privileges
            .Should().Contain([EconomyDatabasePrivilege.SchemaDdl, EconomyDatabasePrivilege.TableDml]);
        EconomyDatabaseRoleCatalog.Find(EconomyDatabaseRoles.Runtime)!.Privileges
            .Should().Equal(EconomyDatabasePrivilege.ReadProjection);
        EconomyDatabaseRoleCatalog.Find(EconomyDatabaseRoles.Writer)!.Privileges
            .Should().Equal(EconomyDatabasePrivilege.ExecuteRegisteredProcedure);
        EconomyDatabaseRoleCatalog.Find(EconomyDatabaseRoles.ProcedureOwner)!.CanLogin.Should().BeFalse();
        EconomyDatabaseRoleCatalog.Find("unknown").Should().BeNull();
        EconomyDatabaseRoleCatalog.All.Where(role => role.Name != EconomyDatabaseRoles.Migration)
            .SelectMany(role => role.Privileges)
            .Should().NotContain(EconomyDatabasePrivilege.TableDml);
    }

    [Fact]
    public void ValidConfirmedMintContractIsAccepted()
    {
        var result = EconomyWriterContractValidator.Validate(ValidRequest());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        FluentActions.Invoking(() => EconomyWriterContractValidator.EnsureValid(ValidRequest()))
            .Should().NotThrow();
    }

    [Fact]
    public void BalancedButUnauthorizedShapeIsRejected()
    {
        var request = ValidRequest();
        request = request with
        {
            Posting = request.Posting with
            {
                Lines =
                [
                    request.Posting.Lines[0] with { Account = EconomyAccountCode.PlatformHardTreasury },
                    request.Posting.Lines[1]
                ]
            }
        };

        AssertRejected(request, EconomyWriterRejectionCode.UnauthorizedTemplateShape);
    }

    [Fact]
    public void AbsentReusedAndMutatedSourceStampsAreRejected()
    {
        var valid = ValidRequest();

        AssertRejected(valid with { Source = null }, EconomyWriterRejectionCode.AbsentSourceStamp);
        AssertRejected(valid with { Source = valid.Source! with { Exists = false } }, EconomyWriterRejectionCode.AbsentSourceStamp);
        AssertRejected(valid with { Source = valid.Source! with { AlreadyMinted = true } }, EconomyWriterRejectionCode.ReusedSourceStamp);
        AssertRejected(valid with { Source = valid.Source! with { SubmittedHash = new string('b', 64) } }, EconomyWriterRejectionCode.MutatedSourceStamp);
    }

    [Fact]
    public void UnknownTemplateVersionFailsClosedWithoutApplyingAProviderSourceRule()
    {
        var valid = ValidRequest();
        var request = valid with
        {
            Posting = valid.Posting with
            {
                Template = new PostingTemplate(
                    PostingTemplateKind.ConfirmedTopUpMint,
                    PostingTemplate.CurrentVersion + 1)
            },
            Source = null
        };

        var result = EconomyWriterContractValidator.Validate(request);

        result.Errors.Should().Contain(error => error.Code == EconomyWriterRejectionCode.UnauthorizedTemplateShape);
        result.Errors.Should().NotContain(error => error.Code == EconomyWriterRejectionCode.AbsentSourceStamp);
    }

    [Fact]
    public void UnconfirmedMintProviderOverCreditAndForgedTimeAreRejected()
    {
        var valid = ValidRequest();

        AssertRejected(
            valid with { Source = valid.Source! with { State = SourceConfirmationState.Observed } },
            EconomyWriterRejectionCode.UnconfirmedExternalMint);
        AssertRejected(
            valid with { Source = valid.Source! with { ProviderPreviouslyCreditedUnits = 950 } },
            EconomyWriterRejectionCode.ProviderOverCredit);
        AssertRejected(
            valid with { Source = valid.Source! with { SubmittedConfirmedAt = ConfirmedAt.AddSeconds(1) } },
            EconomyWriterRejectionCode.ForgedConfirmationTime);
    }

    [Fact]
    public void EarlyMaturityAndOverAllocationAreRejected()
    {
        var valid = ValidRequest();

        AssertRejected(
            valid with { Maturity = valid.Maturity! with { MaturesAt = ConfirmedAt.AddDays(119) } },
            EconomyWriterRejectionCode.EarlyMaturity);
        AssertRejected(
            valid with { Maturity = valid.Maturity! with { MaturesAt = ConfirmedAt.AddDays(121) } },
            EconomyWriterRejectionCode.EarlyMaturity);
        AssertRejected(
            valid with { Allocations = [new WriterAllocationFact(101, 100)] },
            EconomyWriterRejectionCode.OverAllocation);
        AssertRejected(
            valid with { Allocations = [new WriterAllocationFact(0, 100)] },
            EconomyWriterRejectionCode.OverAllocation);
        AssertRejected(
            valid with { Allocations = [new WriterAllocationFact(1, -1)] },
            EconomyWriterRejectionCode.OverAllocation);
    }

    [Fact]
    public void OverlapStaleFenceAndLineageNonConservationAreRejected()
    {
        var valid = ValidRequest();
        var root = valid.OutputRanges[0].Root;

        AssertRejected(
            valid with
            {
                OutputRanges =
                [
                    new RootTraceRange(root, 0, 70, 2),
                    new RootTraceRange(root, 60, 40, 2)
                ]
            },
            EconomyWriterRejectionCode.OverlappingRootRange);
        AssertRejected(
            valid with { ReversalFence = new WriterReversalFenceFact(1, 2) },
            EconomyWriterRejectionCode.StaleReversalEpoch);
        AssertRejected(
            valid with { Lineage = [new WriterLineageFact(CurrencyCode.HardCoin, 100, 99)] },
            EconomyWriterRejectionCode.LineageNonConservation);
    }

    [Fact]
    public void AdjacentRootIntervalsRemainValidAndNonOverlapping()
    {
        var valid = ValidRequest();
        var root = valid.OutputRanges[0].Root;
        var request = valid with
        {
            OutputRanges =
            [
                new RootTraceRange(root, 0, 60, 2),
                new RootTraceRange(root, 60, 40, 2)
            ]
        };

        EconomyWriterContractValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ProviderAndLineageArithmeticFailClosedAtNumericBoundaries()
    {
        var valid = ValidRequest();

        AssertRejected(
            valid with
            {
                Source = valid.Source! with
                {
                    ProviderAuthoritativeUnits = long.MaxValue,
                    ProviderPreviouslyCreditedUnits = long.MaxValue
                },
                Allocations = [new WriterAllocationFact(long.MaxValue, long.MaxValue)]
            },
            EconomyWriterRejectionCode.ProviderOverCredit);
        AssertRejected(
            valid with { Source = valid.Source! with { ProviderAuthoritativeUnits = -1 } },
            EconomyWriterRejectionCode.ProviderOverCredit);
        AssertRejected(
            valid with { Source = valid.Source! with { ProviderPreviouslyCreditedUnits = -1 } },
            EconomyWriterRejectionCode.ProviderOverCredit);
        AssertRejected(
            valid with { Lineage = [new WriterLineageFact(CurrencyCode.HardCoin, -1, -1)] },
            EconomyWriterRejectionCode.LineageNonConservation);
        AssertRejected(
            valid with { Lineage = [new WriterLineageFact(CurrencyCode.HardCoin, 1, -1)] },
            EconomyWriterRejectionCode.LineageNonConservation);
    }

    [Fact]
    public void ValidatorRejectsNullAndEnsureValidReturnsAllDetectedErrors()
    {
        FluentActions.Invoking(() => EconomyWriterContractValidator.Validate(null!))
            .Should().Throw<ArgumentNullException>();
        var request = ValidRequest() with
        {
            Source = null,
            Allocations = [new WriterAllocationFact(2, 1)],
            ReversalFence = new WriterReversalFenceFact(1, 2)
        };

        var action = () => EconomyWriterContractValidator.EnsureValid(request);
        action.Should().Throw<EconomyWriterContractException>()
            .Which.Errors.Select(error => error.Code)
            .Should().Contain(
            [
                EconomyWriterRejectionCode.AbsentSourceStamp,
                EconomyWriterRejectionCode.OverAllocation,
                EconomyWriterRejectionCode.StaleReversalEpoch
            ]);
    }

    [Fact]
    public void NonProviderPostingDoesNotRequireAProviderSourceOrMaturity()
    {
        var posting = PostingFixture.Valid(PostingTemplateKind.Spend);
        var request = new EconomyWriterValidationRequest(
            posting,
            null,
            [new WriterAllocationFact(10, 10)],
            [],
            [],
            [new WriterLineageFact(CurrencyCode.HardCoin, 10, 10)],
            null,
            null);

        request.InputRanges.Should().BeEmpty();
        EconomyWriterContractValidator.Validate(request).IsValid.Should().BeTrue();
    }

    private static void AssertRejected(EconomyWriterValidationRequest request, EconomyWriterRejectionCode code)
    {
        EconomyWriterContractValidator.Validate(request).Errors.Should().Contain(error => error.Code == code);
    }

    private static EconomyWriterValidationRequest ValidRequest()
    {
        var posting = PostingFixture.Valid(PostingTemplateKind.ConfirmedTopUpMint);
        var root = posting.Source!.Id;
        var hash = posting.Source.EvidenceHash;
        return new EconomyWriterValidationRequest(
            posting,
            new WriterSourceFact(
                true,
                false,
                hash,
                hash,
                SourceConfirmationState.Confirmed,
                ConfirmedAt,
                ConfirmedAt,
                1_000,
                0),
            [new WriterAllocationFact(100, 100)],
            [new RootTraceRange(root, 0, 100, 2)],
            [new RootTraceRange(root, 0, 100, 2)],
            [new WriterLineageFact(CurrencyCode.HardCoin, 100, 100)],
            new WriterReversalFenceFact(2, 2),
            new WriterMaturityFact(
                CurrencyCode.HardCoin,
                ProvenanceKind.EarnedHard,
                ConfirmedAt,
                ConfirmedAt.AddDays(EconomyWriterContractValidator.EarnedHardMaturityDays)));
    }
}
