namespace GameGuild.Finance.Economy.Writer;

public static class EconomyDatabaseRoles
{
    public const string Migration = "gameguild_economy_migration";
    public const string Runtime = "gameguild_economy_runtime";
    public const string Writer = "gameguild_economy_writer";
    public const string ProcedureOwner = "gameguild_economy_procedure_owner";
}

public enum EconomyDatabasePrivilege
{
    SchemaDdl = 1,
    TableDml = 2,
    ReadProjection = 3,
    ExecuteRegisteredProcedure = 4
}

public sealed record EconomyDatabaseRole(
    string Name,
    bool CanLogin,
    IReadOnlyList<EconomyDatabasePrivilege> Privileges);

public static class EconomyDatabaseRoleCatalog
{
    private static readonly IReadOnlyList<EconomyDatabaseRole> Roles =
    [
        new(EconomyDatabaseRoles.Migration, true,
            [EconomyDatabasePrivilege.SchemaDdl, EconomyDatabasePrivilege.TableDml]),
        new(EconomyDatabaseRoles.Runtime, true,
            [EconomyDatabasePrivilege.ReadProjection]),
        new(EconomyDatabaseRoles.Writer, false,
            [EconomyDatabasePrivilege.ExecuteRegisteredProcedure]),
        new(EconomyDatabaseRoles.ProcedureOwner, false, [])
    ];

    public static IReadOnlyList<EconomyDatabaseRole> All => Roles;

    public static EconomyDatabaseRole? Find(string name) =>
        Roles.SingleOrDefault(role => string.Equals(role.Name, name, StringComparison.Ordinal));
}
