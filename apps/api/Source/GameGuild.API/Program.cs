using GameGuild;
using GameGuild.API;
using GameGuild.API.Database;
using GameGuild.API.Email;
using GameGuild.API.Geolocation;
using GameGuild.API.Integration;
using GameGuild.API.Push;
using GameGuild.API.Setup;
using GameGuild.API.Sms;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Email;
using GameGuild.Push;
using GameGuild.Sms;

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
builder.Services.Configure<PaymentDunningOptions>(
    builder.Configuration.GetSection(PaymentDunningOptions.SectionName));
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<SesEmailSender>();

// SMS delivery port (issue #244): Twilio REST adapter when SmsDelivery:Provider=Twilio and
// Enabled; otherwise the no-op default declines every send so notification rows fail closed
// (retry → deadletter) instead of pretending to deliver.
builder.Services.Configure<SmsDeliveryOptions>(builder.Configuration.GetSection(SmsDeliveryOptions.SectionName));
builder.Services.AddHttpClient(TwilioSmsSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
if (builder.Configuration.GetValue<bool>($"{SmsDeliveryOptions.SectionName}:Enabled")
    && string.Equals(builder.Configuration[$"{SmsDeliveryOptions.SectionName}:Provider"], "Twilio", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ISmsSender, TwilioSmsSender>();
}
else
{
    builder.Services.AddSingleton<ISmsSender>(static _ => NoOpSmsSender.Instance);
}

// Mobile push delivery port (issue #259): FCM HTTP v1 adapter when PushDelivery:Provider=Fcm
// and Enabled; otherwise the no-op default declines every send (fail closed).
builder.Services.Configure<PushDeliveryOptions>(builder.Configuration.GetSection(PushDeliveryOptions.SectionName));
builder.Services.AddHttpClient(FcmPushSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
if (builder.Configuration.GetValue<bool>($"{PushDeliveryOptions.SectionName}:Enabled")
    && string.Equals(builder.Configuration[$"{PushDeliveryOptions.SectionName}:Provider"], "Fcm", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IPushSender, FcmPushSender>();
}
else
{
    builder.Services.AddSingleton<IPushSender>(static _ => NoOpPushSender.Instance);
}

// Geolocation (issues #242/#278): offline MaxMind GeoLite2 adapter when a database path is
// configured; otherwise the module-level no-op default keeps location enrichment Unknown.
// Registered after the modules so this registration is the effective one.
builder.Services.Configure<MaxMindGeolocationOptions>(builder.Configuration.GetSection(MaxMindGeolocationOptions.SectionName));
if (!string.IsNullOrWhiteSpace(builder.Configuration[$"{MaxMindGeolocationOptions.SectionName}:DatabasePath"]))
{
    builder.Services.AddSingleton<IGeolocationProvider, MaxMindGeolocationProvider>();
}

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
