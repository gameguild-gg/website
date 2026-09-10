using Xunit;

// Keep Docker Desktop load bounded. Gate-provided databases are isolated per test,
// and lifecycle operations are synchronized in EconomyPostgreSqlTestDatabase, so
// PostgreSQL-backed cases can still run concurrently.
[assembly: CollectionBehavior(MaxParallelThreads = 3)]
