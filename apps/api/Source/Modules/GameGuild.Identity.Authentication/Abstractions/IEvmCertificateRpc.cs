using System.Text.Json;

namespace GameGuild.Identity.Authentication;

/// <summary>Typed JSON-RPC boundary; callers never supply arbitrary method names.</summary>
public interface IEvmCertificateRpc
{
    Task<JsonElement> RequestAsync(EvmCertificateRpcMethod method, object?[] parameters, CancellationToken cancellationToken = default);
}

public enum EvmCertificateRpcMethod
{
    ChainId,
    GetCode,
    Call,
    GetTransactionByHash,
    GetTransactionReceipt,
    GetBlockByNumber
}
