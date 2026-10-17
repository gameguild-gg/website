using FluentAssertions;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

internal static class EmailLogPrivacyAssertions
{
    public static void RequirePrivateValuesAbsent<T>(TestLogger<T> logger, params string[] privateValues)
    {
        logger.Entries.Should().NotBeEmpty();
        foreach (var entry in logger.Entries)
        {
            entry.Exception.Should().BeNull("exception objects may contain addresses, codes or tokens");
            entry.Properties.Should().NotContain(pair => pair.Key == "Email" || pair.Key == "Code" || pair.Key == "Token");
            foreach (var privateValue in privateValues)
            {
                entry.Message.Should().NotContain(privateValue);
                entry.Properties.Should().OnlyContain(pair => pair.Value == null || !pair.Value.ToString()!.Contains(privateValue));
            }
        }
    }

    public static InvalidOperationException PrivateFailure(string carrier, string privateValue)
    {
        var failure = carrier switch
        {
            "message" => new InvalidOperationException(privateValue),
            "inner" => new InvalidOperationException("Delivery failure", new Exception(privateValue)),
            "data" => new InvalidOperationException("Delivery failure"),
            _ => throw new ArgumentOutOfRangeException(nameof(carrier))
        };
        if (carrier == "data")
        {
            failure.Data["private"] = privateValue;
        }

        return failure;
    }
}
