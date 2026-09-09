using FluentAssertions;
using FluentValidation;
using GameGuild.Finance.Economy.Commands;
using GameGuild.Finance.Economy.Funding;

namespace GameGuild.Finance.Economy.UnitTests.Commands;

public sealed class HardToSoftConversionCommandTests
{
    [Fact]
    public async Task Handler_ForwardsTheExactSelfServiceRequestToTheWorkflow()
    {
        var receipt = new SelfServiceHardToSoftConversionReceipt(
            Guid.Parse("92000000-0000-0000-0000-000000000002"),
            null,
            17,
            "journal-hash",
            false);
        var workflow = new CapturingWorkflow(receipt);
        var handler = new ConvertMyHardToSoftCommandHandler(workflow);
        var command = new ConvertMyHardToSoftCommand(
            new ConvertMyHardToSoftRequest(100, "conversion-key"));

        var result = await handler.Handle(command, CancellationToken.None);

        workflow.Request.Should().Be(new SelfServiceHardToSoftConversionRequest(100, "conversion-key"));
        result.Should().Be(receipt);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(100, false)]
    public void Validator_RejectsInvalidPrincipal(long principalUnits, bool shouldFail)
    {
        var command = new ConvertMyHardToSoftCommand(
            new ConvertMyHardToSoftRequest(principalUnits, "conversion-key"));

        var result = new ConvertMyHardToSoftCommandValidator().Validate(command);

        result.IsValid.Should().Be(!shouldFail);
    }

    [Fact]
    public void Validator_RequiresAnIdempotencyKey()
    {
        var command = new ConvertMyHardToSoftCommand(
            new ConvertMyHardToSoftRequest(100, string.Empty));

        var result = new ConvertMyHardToSoftCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain("Request.IdempotencyKey");
    }

    private sealed class CapturingWorkflow(SelfServiceHardToSoftConversionReceipt receipt) : IHardToSoftConversionWorkflow
    {
        public SelfServiceHardToSoftConversionRequest? Request { get; private set; }

        public Task<SelfServiceHardToSoftConversionReceipt> ConvertAsync(
            SelfServiceHardToSoftConversionRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(receipt);
        }
    }
}
