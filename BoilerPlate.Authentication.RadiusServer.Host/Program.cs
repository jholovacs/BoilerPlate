using BoilerPlate.Authentication.Database.PostgreSql.Extensions;
using BoilerPlate.Authentication.RadiusServer;
using BoilerPlate.Authentication.RadiusServer.Configuration;
using BoilerPlate.Authentication.RadiusServer.Host;
using BoilerPlate.Authentication.Services.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddAuthenticationDatabasePostgreSql(context.Configuration);
        services.AddAuthenticationServices();

        services.Configure<RadiusServerOptions>(context.Configuration.GetSection(RadiusServerOptions.SectionName));

        services.AddScoped<IRadiusAuthProvider, AuthServiceRadiusProvider>();
        services.AddSingleton<AuthBackedRadiusPacketHandler>();
        services.AddSingleton<AuthBackedRadiusServer>();
        services.AddHostedService<RadiusServerHostedService>();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(
            context.HostingEnvironment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);
    })
    .Build();

await host.RunAsync();
