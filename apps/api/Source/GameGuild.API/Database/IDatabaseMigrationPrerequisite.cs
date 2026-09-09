using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.Database;

internal interface IDatabaseMigrationPrerequisite
{
    Task PrepareAsync(DbContext db, CancellationToken cancellationToken);
}
