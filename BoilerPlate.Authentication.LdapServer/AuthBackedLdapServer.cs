using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using BoilerPlate.Authentication.LdapServer.Configuration;
using Flexinets.Ldap.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Authentication.LdapServer;

/// <summary>
///     LDAP server that authenticates against BoilerPlate services and exposes directory data.
///     Supports LDAP (port 389) and LDAPS (port 636 with TLS).
/// </summary>
public class AuthBackedLdapServer
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuthBackedLdapServer> _logger;
    private readonly LdapServerOptions _options;
    private readonly ConcurrentDictionary<TcpClient, bool> _connections = new();
    private TcpListener? _plainListener;
    private TcpListener? _secureListener;
    private CancellationTokenSource? _cts;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AuthBackedLdapServer" /> class
    /// </summary>
    public AuthBackedLdapServer(
        IServiceScopeFactory scopeFactory,
        IOptions<LdapServerOptions> options,
        ILogger<AuthBackedLdapServer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    ///     Starts the LDAP server(s).
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (_options.Port > 0)
        {
            _plainListener = new TcpListener(IPAddress.Any, _options.Port);
            _plainListener.Start();
            _ = AcceptLoopAsync(_plainListener, useTls: false, _cts.Token);
            _logger.LogInformation("LDAP server listening on port {Port}", _options.Port);
        }

        if (_options.SecurePort > 0 && !string.IsNullOrEmpty(_options.CertificatePath) && File.Exists(_options.CertificatePath))
        {
            _secureListener = new TcpListener(IPAddress.Any, _options.SecurePort);
            _secureListener.Start();
            _ = AcceptLoopAsync(_secureListener, useTls: true, _cts.Token);
            _logger.LogInformation("LDAPS server listening on port {Port}", _options.SecurePort);
        }
        else if (_options.SecurePort > 0)
        {
            _logger.LogWarning("LDAPS disabled: CertificatePath not configured or file not found. Set LdapServer:CertificatePath for secure LDAP.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Stops the LDAP server(s).
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        _plainListener?.Stop();
        _secureListener?.Stop();
        foreach (var client in _connections.Keys.ToList())
        {
            try { client.Close(); } catch { /* ignore */ }
        }
        _connections.Clear();
        _logger.LogInformation("LDAP server stopped");
        return Task.CompletedTask;
    }

    private async Task AcceptLoopAsync(TcpListener listener, bool useTls, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && listener.Server.IsBound)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _connections[client] = true;
                _ = Task.Run(() => HandleClientAsync(client, useTls, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting LDAP connection");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, bool useTls, CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("LDAP connection from {RemoteEndPoint}", client.Client.RemoteEndPoint);

            Stream stream = client.GetStream();
            if (useTls)
            {
                var sslStream = await CreateSslStreamAsync(stream);
                if (sslStream == null)
                {
                    _logger.LogWarning("Failed to establish TLS for LDAPS connection");
                    client.Close();
                    return;
                }
                stream = sslStream;
            }

            var isBound = false;
            while (!ct.IsCancellationRequested && LdapPacket.TryParsePacket(stream, out var requestPacket) && requestPacket != null)
            {
                var messageId = requestPacket.ChildAttributes.Count > 0
                    ? requestPacket.ChildAttributes[0].GetValue<int>()
                    : 0;

                if (requestPacket.ChildAttributes.Any(o => o.LdapOperation == LdapOperation.UnbindRequest))
                {
                    break;
                }

                if (requestPacket.ChildAttributes.Any(o => o.LdapOperation == LdapOperation.BindRequest))
                {
                    isBound = await HandleBindRequestAsync(stream, requestPacket, messageId, ct);
                }
                else if (isBound && requestPacket.ChildAttributes.Any(o => o.LdapOperation == LdapOperation.SearchRequest))
                {
                    await HandleSearchRequestAsync(stream, requestPacket, messageId, ct);
                }
                else if (isBound)
                {
                    _logger.LogDebug("Unsupported LDAP operation, ignoring");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LDAP connection closed");
        }
        finally
        {
            _connections.TryRemove(client, out _);
            try { client.Close(); } catch { /* ignore */ }
        }
    }

    private async Task<bool> HandleBindRequestAsync(Stream stream, LdapPacket requestPacket, int messageId, CancellationToken ct)
    {
        var bindRequest = requestPacket.ChildAttributes.FirstOrDefault(o => o.LdapOperation == LdapOperation.BindRequest);
        if (bindRequest == null || bindRequest.ChildAttributes.Count < 3)
        {
            WriteBindResponse(stream, messageId, LdapResult.protocolError);
            return false;
        }

        var dn = bindRequest.ChildAttributes[1].GetValue<string>();
        var password = bindRequest.ChildAttributes[2].GetValue<string>();

        var (username, tenantId) = DnParser.ParseBindDn(dn ?? "", _options.DefaultTenantId);

        if (string.IsNullOrEmpty(username))
        {
            WriteBindResponse(stream, messageId, LdapResult.invalidCredentials);
            return false;
        }

        using var scope = _scopeFactory.CreateScope();
        var directoryProvider = scope.ServiceProvider.GetRequiredService<ILdapDirectoryProvider>();
        var valid = await directoryProvider.ValidateCredentialsAsync(username, password ?? "", tenantId, ct);
        var result = valid ? LdapResult.success : LdapResult.invalidCredentials;
        WriteBindResponse(stream, messageId, result);
        return valid;
    }

    private void WriteBindResponse(Stream stream, int messageId, LdapResult result)
    {
        var responsePacket = new LdapPacket(messageId);
        responsePacket.ChildAttributes.Add(new LdapResultAttribute(LdapOperation.BindResponse, result));
        var bytes = responsePacket.GetBytes();
        stream.Write(bytes, 0, bytes.Length);
    }

    private async Task HandleSearchRequestAsync(Stream stream, LdapPacket requestPacket, int messageId, CancellationToken ct)
    {
        var searchRequest = requestPacket.ChildAttributes.FirstOrDefault(o => o.LdapOperation == LdapOperation.SearchRequest);
        if (searchRequest == null || searchRequest.ChildAttributes.Count < 7)
        {
            WriteSearchDone(stream, messageId, LdapResult.protocolError);
            return;
        }

        var baseObject = searchRequest.ChildAttributes[0].GetValue<string>() ?? "";
        var filter = searchRequest.ChildAttributes.Count > 6 ? searchRequest.ChildAttributes[6] : null;

        var tenantId = ExtractTenantFromBaseDn(baseObject);
        if (!tenantId.HasValue && _options.DefaultTenantId.HasValue)
            tenantId = _options.DefaultTenantId;

        if (!tenantId.HasValue)
        {
            WriteSearchDone(stream, messageId, LdapResult.noSuchObject);
            return;
        }

        IReadOnlyList<LdapDirectoryEntry> entries;
        string? filterAttr = null;
        string? filterValue = null;

        if (filter != null && filter.ChildAttributes.Count >= 2 && (LdapFilterChoice?)filter.ContextType == LdapFilterChoice.equalityMatch)
        {
            filterAttr = filter.ChildAttributes[0].GetValue<string>();
            filterValue = filter.ChildAttributes[1].GetValue<string>();
        }
        else if (filter != null && (LdapFilterChoice?)filter.ContextType == LdapFilterChoice.present)
        {
            filterAttr = filter.ChildAttributes.Count > 0 ? filter.ChildAttributes[0].GetValue<string>() : filter.GetValue<string>();
            filterValue = "*";
        }

        using var scope = _scopeFactory.CreateScope();
        var directoryProvider = scope.ServiceProvider.GetRequiredService<ILdapDirectoryProvider>();

        if (!string.IsNullOrEmpty(filterAttr) && !string.IsNullOrEmpty(filterValue))
            entries = await directoryProvider.SearchAsync(tenantId.Value, filterAttr, filterValue, ct);
        else
            entries = await directoryProvider.GetAllUsersAsync(tenantId.Value, ct);

        foreach (var entry in entries)
        {
            WriteSearchResultEntry(stream, messageId, entry);
        }

        WriteSearchDone(stream, messageId, LdapResult.success);
    }

    private static Guid? ExtractTenantFromBaseDn(string baseDn)
    {
        if (string.IsNullOrWhiteSpace(baseDn)) return null;
        var parts = baseDn.Split(',');
        foreach (var part in parts)
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            var attr = part[..eq].Trim().ToLowerInvariant();
            var value = part[(eq + 1)..].Trim();
            if (attr == "ou" && Guid.TryParse(value, out var tid))
                return tid;
        }
        return null;
    }

    private void WriteSearchResultEntry(Stream stream, int messageId, LdapDirectoryEntry entry)
    {
        var searchResultEntry = new LdapAttribute(LdapOperation.SearchResultEntry);
        searchResultEntry.ChildAttributes.Add(new LdapAttribute(UniversalDataType.OctetString, entry.DistinguishedName));

        var attrsSequence = new LdapAttribute(UniversalDataType.Sequence);
        AddAttribute(attrsSequence, "cn", entry.Cn);
        AddAttribute(attrsSequence, "uid", entry.Uid);
        AddAttribute(attrsSequence, "sAMAccountName", entry.SamAccountName);
        AddAttribute(attrsSequence, "mail", entry.Mail);
        AddAttribute(attrsSequence, "displayName", entry.DisplayName);
        AddAttribute(attrsSequence, "givenName", entry.GivenName);
        AddAttribute(attrsSequence, "sn", entry.Sn);
        if (entry.ObjectClass.Count > 0)
            attrsSequence.ChildAttributes.Add(new LdapPartialAttribute("objectClass", entry.ObjectClass));
        if (entry.MemberOf.Count > 0)
            attrsSequence.ChildAttributes.Add(new LdapPartialAttribute("memberOf", entry.MemberOf));

        searchResultEntry.ChildAttributes.Add(attrsSequence);

        var responsePacket = new LdapPacket(messageId);
        responsePacket.ChildAttributes.Add(searchResultEntry);
        var bytes = responsePacket.GetBytes();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void AddAttribute(LdapAttribute parent, string name, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        parent.ChildAttributes.Add(new LdapPartialAttribute(name, value));
    }

    private void WriteSearchDone(Stream stream, int messageId, LdapResult result)
    {
        var responsePacket = new LdapPacket(messageId);
        responsePacket.ChildAttributes.Add(new LdapResultAttribute(LdapOperation.SearchResultDone, result));
        var bytes = responsePacket.GetBytes();
        stream.Write(bytes, 0, bytes.Length);
    }

    private async Task<SslStream?> CreateSslStreamAsync(Stream innerStream)
    {
        if (string.IsNullOrEmpty(_options.CertificatePath) || !File.Exists(_options.CertificatePath))
        {
            _logger.LogWarning("LDAPS certificate not found at {Path}", _options.CertificatePath);
            return null;
        }

        X509Certificate2 certificate;
        try
        {
            certificate = !string.IsNullOrEmpty(_options.CertificatePassword)
                ? new X509Certificate2(_options.CertificatePath, _options.CertificatePassword)
                : new X509Certificate2(_options.CertificatePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load LDAPS certificate");
            return null;
        }

        var sslStream = new SslStream(innerStream, leaveInnerStreamOpen: false);
        await sslStream.AuthenticateAsServerAsync(certificate, clientCertificateRequired: false, checkCertificateRevocation: false);
        return sslStream;
    }
}
