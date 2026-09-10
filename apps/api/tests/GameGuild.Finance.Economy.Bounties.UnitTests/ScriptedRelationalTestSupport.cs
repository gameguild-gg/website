using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using GameGuild.Finance.Economy.Bounties.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

internal sealed class ScriptedBountiesContext : DbContext, IApplicationDbContext
{
    public ScriptedBountiesContext(ScriptedRelationalInterceptor interceptor)
        : base(new DbContextOptionsBuilder<ScriptedBountiesContext>()
            .UseSqlite("Data Source=:memory:")
            .AddInterceptors(interceptor)
            .Options)
    {
        Database.OpenConnection();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        new BountiesModelConfiguration().Configure(modelBuilder);

    Task<IDbContextTransaction> IApplicationDbContext.BeginTransactionAsync(
        CancellationToken cancellationToken) => Database.BeginTransactionAsync(cancellationToken);
}

internal sealed class ScriptedRelationalInterceptor : DbCommandInterceptor
{
    private readonly Queue<Func<DbCommand, DbDataReader>> _readers = [];
    private readonly Queue<Func<DbCommand, int>> _nonQueries = [];

    public List<string> Commands { get; } = [];

    public void EnqueueReader(DataTable table) =>
        _readers.Enqueue(command => ReorderForProjection(table, command.CommandText).CreateDataReader());

    public void EnqueueReaderException(Exception exception) =>
        _readers.Enqueue(_ => throw exception);

    public void EnqueueNonQuery(int result = 1) =>
        _nonQueries.Enqueue(_ => result);

    public void EnqueueNonQueryException(Exception exception) =>
        _nonQueries.Enqueue(_ => throw exception);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Commands.Add(command.CommandText);
        if (_readers.Count == 0)
            throw new InvalidOperationException("No scripted reader was provided for the relational command.");
        return InterceptionResult<DbDataReader>.SuppressWithResult(_readers.Dequeue()(command));
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Commands.Add(command.CommandText);
        if (_nonQueries.Count == 0)
            throw new InvalidOperationException("No scripted result was provided for the relational command.");
        return InterceptionResult<int>.SuppressWithResult(_nonQueries.Dequeue()(command));
    }

    private static DataTable ReorderForProjection(DataTable source, string commandText)
    {
        var outerSelect = commandText.Split("\nFROM", 2, StringSplitOptions.None)[0];
        var projectedColumns = Regex.Matches(outerSelect, "\\\"[^\\\"]+\\\"\\.\\\"(?<column>[^\\\"]+)\\\"")
            .Select(match => match.Groups["column"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (projectedColumns.Length == 0 ||
            projectedColumns.Any(column => !source.Columns.Contains(column)))
            return source;

        var reordered = new DataTable();
        foreach (var column in projectedColumns)
            reordered.Columns.Add(column, source.Columns[column]!.DataType);
        foreach (DataRow row in source.Rows)
        {
            var values = projectedColumns.Select(column => row[column]).ToArray();
            reordered.Rows.Add(values);
        }
        return reordered;
    }
}

internal sealed class TestDataTable
{
    private readonly DataTable _table = new();

    public TestDataTable(params (string Name, Type Type)[] columns)
    {
        foreach (var (name, type) in columns)
            _table.Columns.Add(name, type);
    }

    public TestDataTable AddRow(params object?[] values)
    {
        _table.Rows.Add(values.Select(value => value ?? DBNull.Value).ToArray());
        return this;
    }

    public DataTable Build() => _table;
}

internal sealed class NonRelationalApplicationContext : IApplicationDbContext
{
    public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class TestDbException(string message) : DbException(message);
