using GameGuild.API;
using GameGuild.API.Database;
using GameGuild.API.Email;
using GameGuild.API.Integration;
using GameGuild.API.Setup;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Email;

var builder = WebApplication.CreateBuilder(args);
var productComposition = ApiProductComposition.Instance;
builder.Services.AddSingleton<IApiProductComposition>(productComposition);

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.MaxDepth = 128;
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

DataProtectionStartupConfiguration.Configure(builder, productComposition);

builder.AddAppSettings();
builder.AddEnvironmentVariables();
OperationalStartupConfiguration.ThrowIfInvalid(builder.Configuration, builder.Environment.EnvironmentName);
builder.AddStructuredLogging();
builder.AddOpenTelemetryObservability();

builder.Services.AddSingleton<DatabaseConnectivityProbe>();

builder.AddInfrastructureLayer();
builder.AddApplicationLayer();
builder.AddPresentationLayer();

builder.Services.Configure<EmailDeliveryOptions>(builder.Configuration.GetSection("EmailDelivery"));
builder.Services.Configure<SubscriptionNotificationLinkOptions>(
    builder.Configuration.GetSection("SubscriptionNotifications"));
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IMonthlyStatementMailSender, MonthlyStatementMailSenderAdapter>();
builder.Services.AddScoped<IMonthlyStatementDataProvider, MonthlyStatementDataProvider>();
builder.Services.AddScoped<IMonthlyStatementAttachmentBuilder, MonthlyStatementAttachmentBuilder>();
builder.Services.AddSingleton<IMonthlyStatementLinkBuilder, MonthlyStatementLinkBuilder>();

productComposition.ConfigureServices(builder);

var app = builder.Build();
var databaseInitialized = await DatabaseStartupInitializer.InitializeAsync(
    app,
    productComposition.SeedAsync).ConfigureAwait(false);

await ApiHostLifecycle.RunAsync(app, productComposition, databaseInitialized, args).ConfigureAwait(false);

namespace GameGuild.API
{
    public class Program { }
}
