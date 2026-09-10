using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Ledger;

public sealed record RegisteredPostingCapability(Guid Id, string Name, PostingTemplateKind TemplateKind);

public interface IRegisteredPostingCapabilityResolver
{
    Task<RegisteredPostingCapability> ResolveAsync(
        string capabilityName,
        PostingTemplateKind templateKind,
        CancellationToken cancellationToken = default);

    Task<RegisteredPostingAuthority> ResolveAuthorityAsync(
        string capabilityName,
        PostingTemplateKind templateKind,
        CapabilityAuthorizationReceipt receipt,
        CancellationToken cancellationToken = default);
}

public sealed class RegisteredPostingCapabilityUnavailableException : InvalidOperationException
{
    public RegisteredPostingCapabilityUnavailableException(string message)
        : base(message)
    {
    }

    public RegisteredPostingCapabilityUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
