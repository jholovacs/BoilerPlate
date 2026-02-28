using BoilerPlate.Authentication.RadiusServer.Configuration;
using Flexinets.Radius;
using Flexinets.Radius.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Authentication.RadiusServer;

/// <summary>
///     RADIUS packet handler that validates Access-Request against BoilerPlate authentication services.
/// </summary>
public class AuthBackedRadiusPacketHandler : IPacketHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RadiusServerOptions _options;
    private readonly ILogger<AuthBackedRadiusPacketHandler> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AuthBackedRadiusPacketHandler" /> class
    /// </summary>
    public AuthBackedRadiusPacketHandler(
        IServiceScopeFactory scopeFactory,
        IOptions<RadiusServerOptions> options,
        ILogger<AuthBackedRadiusPacketHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public IRadiusPacket HandlePacket(IRadiusPacket packet)
    {
        if (packet.Code == PacketCode.AccountingRequest)
        {
            var acctStatusType = packet.GetAttribute<int?>("Acct-Status-Type");
            if (acctStatusType.HasValue)
            {
                return packet.CreateResponsePacket(PacketCode.AccountingResponse);
            }
        }

        if (packet.Code != PacketCode.AccessRequest)
        {
            _logger.LogWarning("Unsupported RADIUS packet code: {Code}", packet.Code);
            return packet.CreateResponsePacket(PacketCode.AccessReject);
        }

        var username = packet.GetAttribute<string>("User-Name");
        var password = packet.GetAttribute<string>("User-Password");

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogDebug("RADIUS Access-Request missing User-Name or User-Password");
            return packet.CreateResponsePacket(PacketCode.AccessReject);
        }

        var tenantId = _options.DefaultTenantId;
        var (parsedUsername, parsedTenantId) = RadiusUsernameParser.ParseUsername(username, tenantId);
        if (parsedTenantId.HasValue)
            tenantId = parsedTenantId;

        using var scope = _scopeFactory.CreateScope();
        var authProvider = scope.ServiceProvider.GetRequiredService<IRadiusAuthProvider>();

        var valid = authProvider.ValidateCredentialsAsync(parsedUsername, password, tenantId)
            .GetAwaiter().GetResult();

        if (valid)
        {
            var response = packet.CreateResponsePacket(PacketCode.AccessAccept);
            response.AddAttribute("Acct-Interim-Interval", 60);
            return response;
        }

        return packet.CreateResponsePacket(PacketCode.AccessReject);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
