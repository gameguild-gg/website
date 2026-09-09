using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GameGuild.API.Database;

namespace GameGuild.API.UnitTests.Database;

public sealed class ApplicationDbContextCommonTests
{
    [Fact]
    public async Task Model_PreservesDatabaseDataProtectionKeysAlongsideDurableTransport()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);

        Assert.IsAssignableFrom<IDataProtectionKeyContext>(context);
        Assert.NotNull(context.Model.FindEntityType(typeof(DataProtectionKey)));
        Assert.NotNull(context.Model.FindEntityType("GameGuild.API.Eventing.OutboxMessage"));
        Assert.NotNull(context.Model.FindEntityType("GameGuild.API.Eventing.InboxReceipt"));
    }
}
