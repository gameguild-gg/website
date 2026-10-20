namespace GameGuild.Identity.Authentication;

/// <summary>Safe failure metadata: never includes an endpoint, RPC message/data or inner transport exception.</summary>
public sealed class EvmCertificateRpcException(
    EvmCertificateRpcFailure failure,
    int? httpStatusCode = null,
    int? rpcErrorCode = null) : Exception($"EVM RPC request failed ({failure}).")
{
    public EvmCertificateRpcFailure Failure { get; } = failure;
    public int? HttpStatusCode { get; } = httpStatusCode;
    public int? RpcErrorCode { get; } = rpcErrorCode;
}

public enum EvmCertificateRpcFailure
{
    HttpFailure,
    RpcFailure,
    InvalidResponse,
    ResponseTooLarge,
    TransportFailure,
    Timeout
}
