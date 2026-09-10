namespace GameGuild.Finance.Economy.Writer;

public sealed record EconomyProcedureParameter(string Name, string PostgreSqlType);

public sealed record EconomyWriterProcedure(
    string Schema,
    string Name,
    bool SecurityDefiner,
    string OwnerRole,
    string ExecuteRole,
    string PinnedSearchPath,
    IReadOnlyList<EconomyProcedureParameter> Inputs,
    IReadOnlyList<EconomyProcedureParameter> Outputs)
{
    public string CanonicalSignature => $"{Schema}.{Name}({string.Join(',', Inputs.Select(input => input.PostgreSqlType))})";
}

public static class EconomyWriterProcedureContract
{
    public static EconomyWriterProcedure V1 { get; } = new(
        "economy_private",
        "post_registered_posting_v1",
        true,
        EconomyDatabaseRoles.ProcedureOwner,
        EconomyDatabaseRoles.Writer,
        "pg_catalog,economy_private",
        [
            Input("p_capability_id", "uuid"),
            Input("p_actor_id", "uuid"),
            Input("p_tenant_id", "uuid"),
            Input("p_posting_id", "uuid"),
            Input("p_idempotency_key", "text"),
            Input("p_template_kind", "integer"),
            Input("p_template_version", "integer"),
            Input("p_authority", "integer"),
            Input("p_policy_version", "bigint"),
            Input("p_reserve_version", "bigint"),
            Input("p_risk_decision_id", "uuid"),
            Input("p_risk_operation_fingerprint", "text"),
            Input("p_expected_counter_version", "bigint"),
            Input("p_source_stamp_id", "uuid"),
            Input("p_source_evidence_hash", "text"),
            Input("p_requested_at", "timestamptz"),
            Input("p_lines", "jsonb"),
            Input("p_allocations", "jsonb"),
            Input("p_root_ranges", "jsonb"),
            Input("p_expected_reversal_epochs", "jsonb"),
            Input("p_dispatch_snapshot_hash", "text")
        ],
        [
            Output("posting_id", "uuid"),
            Output("journal_sequence", "bigint"),
            Output("journal_hash", "text"),
            Output("duplicate", "boolean")
        ]);

    private static EconomyProcedureParameter Input(string name, string type) => new(name, type);
    private static EconomyProcedureParameter Output(string name, string type) => new(name, type);
}
