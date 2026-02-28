using System.Net;
using BoilerPlate.Authentication.RadiusServer.Configuration;
using Flexinets.Net;
using Flexinets.Radius;
using Flexinets.Radius.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FlexinetsRadiusServer = Flexinets.Radius.RadiusServer;

namespace BoilerPlate.Authentication.RadiusServer;

/// <summary>
///     RADIUS server that authenticates against BoilerPlate services.
///     Wraps Flexinets.Radius.RadiusServer and delegates Access-Request to IRadiusAuthProvider.
/// </summary>
public class AuthBackedRadiusServer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuthBackedRadiusServer> _logger;
    private readonly RadiusServerOptions _options;
    private FlexinetsRadiusServer? _server;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AuthBackedRadiusServer" /> class
    /// </summary>
    public AuthBackedRadiusServer(
        IServiceProvider serviceProvider,
        IOptions<RadiusServerOptions> options,
        ILogger<AuthBackedRadiusServer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    ///     Starts the RADIUS server.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_server != null)
        {
            _logger.LogWarning("RADIUS server already started");
            return Task.CompletedTask;
        }

        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
        var packetParserLogger = loggerFactory.CreateLogger<RadiusPacketParser>();
        var radiusServerLogger = loggerFactory.CreateLogger<FlexinetsRadiusServer>();

        var dictionary = RadiusDictionary.Parse(DefaultDictionary.RadiusDictionary);
        var radiusPacketParser = new RadiusPacketParser(packetParserLogger, dictionary);
        var packetHandler = _serviceProvider.GetRequiredService<AuthBackedRadiusPacketHandler>();

        var repository = new PacketHandlerRepository();
        repository.AddPacketHandler(IPAddress.Any, packetHandler, _options.SharedSecret);

        var localEndpoint = new IPEndPoint(IPAddress.Any, _options.Port);
        var udpClientFactory = new UdpClientFactory();

        _server = new FlexinetsRadiusServer(
            udpClientFactory,
            localEndpoint,
            radiusPacketParser,
            RadiusServerType.Authentication,
            repository,
            radiusServerLogger);

        _server.Start();
        _logger.LogInformation("RADIUS server listening on port {Port}", _options.Port);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Stops the RADIUS server.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_server != null)
        {
            _server.Stop();
            _server.Dispose();
            _server = null;
            _logger.LogInformation("RADIUS server stopped");
        }

        return Task.CompletedTask;
    }
}
