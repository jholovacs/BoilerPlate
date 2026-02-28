using BoilerPlate.Authentication.Database.PostgreSql.Extensions;
using BoilerPlate.Authentication.LdapServer;
using BoilerPlate.Authentication.LdapServer.Configuration;
using BoilerPlate.Authentication.LdapServer.Host;
using BoilerPlate.Authentication.Services.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddAuthenticationDatabasePostgreSql(context.Configuration);
        services.AddAuthenticationServices();

        services.Configure<LdapServerOptions>(context.Configuration.GetSection(LdapServerOptions.SectionName));

        services.AddScoped<ILdapDirectoryProvider, AuthServiceLdapDirectoryProvider>();
        services.AddSingleton<AuthBackedLdapServer>();
        services.AddHostedService<LdapServerHostedService>();
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
