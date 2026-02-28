using BoilerPlate.Authentication.RadiusServer;
using BoilerPlate.Authentication.RadiusServer.Configuration;
using Flexinets.Radius.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BoilerPlate.Authentication.RadiusServer.Tests;

/// <summary>
///     Unit tests for AuthBackedRadiusPacketHandler.
/// </summary>
public class AuthBackedRadiusPacketHandlerTests
{
    /// <summary>
    ///     Creates a mock IRadiusPacket for Access-Request with User-Name and User-Password.
    /// </summary>
    private static Mock<IRadiusPacket> CreateAccessRequestPacket(string username, string password, Mock<IRadiusPacket>? responsePacket = null)
    {
        var packetMock = new Mock<IRadiusPacket>();
        packetMock.Setup(p => p.Code).Returns(PacketCode.AccessRequest);
        packetMock.Setup(p => p.GetAttribute<string>("User-Name")).Returns(username);
        packetMock.Setup(p => p.GetAttribute<string>("User-Password")).Returns(password);
        packetMock.Setup(p => p.GetAttribute<int?>("Acct-Status-Type")).Returns((int?)null);

        var response = responsePacket ?? new Mock<IRadiusPacket>();
        packetMock.Setup(p => p.CreateResponsePacket(PacketCode.AccessAccept)).Returns(response.Object);
        packetMock.Setup(p => p.CreateResponsePacket(PacketCode.AccessReject)).Returns(response.Object);
        packetMock.Setup(p => p.CreateResponsePacket(PacketCode.AccountingResponse)).Returns(response.Object);

        return packetMock;
    }

    /// <summary>
    ///     Creates a mock IRadiusPacket for AccountingRequest with Acct-Status-Type.
    /// </summary>
    private static Mock<IRadiusPacket> CreateAccountingRequestPacket(int? acctStatusType, Mock<IRadiusPacket>? responsePacket = null)
    {
        var packetMock = new Mock<IRadiusPacket>();
        packetMock.Setup(p => p.Code).Returns(PacketCode.AccountingRequest);
        packetMock.Setup(p => p.GetAttribute<int?>("Acct-Status-Type")).Returns(acctStatusType);

        var response = responsePacket ?? new Mock<IRadiusPacket>();
        packetMock.Setup(p => p.CreateResponsePacket(PacketCode.AccountingResponse)).Returns(response.Object);

        return packetMock;
    }

    /// <summary>
    ///     Creates a mock IRadiusPacket for an unsupported packet code.
    /// </summary>
    private static Mock<IRadiusPacket> CreateUnsupportedPacket(PacketCode code, Mock<IRadiusPacket>? responsePacket = null)
    {
        var packetMock = new Mock<IRadiusPacket>();
        packetMock.Setup(p => p.Code).Returns(code);

        var response = responsePacket ?? new Mock<IRadiusPacket>();
        packetMock.Setup(p => p.CreateResponsePacket(PacketCode.AccessReject)).Returns(response.Object);

        return packetMock;
    }

    /// <summary>
    ///     Test case: HandlePacket should return AccountingResponse for AccountingRequest with Acct-Status-Type.
    ///     Scenario: Packet is AccountingRequest and has Acct-Status-Type attribute. Handler returns AccountingResponse.
    /// </summary>
    [Fact]
    public void HandlePacket_WithAccountingRequestAndAcctStatusType_ShouldReturnAccountingResponse()
    {
        // Arrange
        var responsePacket = new Mock<IRadiusPacket>();
        var packet = CreateAccountingRequestPacket(1, responsePacket);
        var (handler, _) = CreateHandler();

        // Act
        var result = handler.HandlePacket(packet.Object);

        // Assert
        result.Should().Be(responsePacket.Object);
        packet.Verify(p => p.CreateResponsePacket(PacketCode.AccountingResponse), Times.Once);
    }

    /// <summary>
    ///     Test case: HandlePacket should return AccessReject for unsupported packet codes.
    ///     Scenario: Packet is not AccessRequest or AccountingRequest. Handler returns AccessReject.
    /// </summary>
    [Theory]
    [InlineData(PacketCode.AccessAccept)]
    [InlineData(PacketCode.AccessReject)]
    [InlineData(PacketCode.AccountingResponse)]
    public void HandlePacket_WithUnsupportedPacketCode_ShouldReturnAccessReject(PacketCode code)
    {
        // Arrange
        var responsePacket = new Mock<IRadiusPacket>();
        var packet = CreateUnsupportedPacket(code, responsePacket);
        var (handler, _) = CreateHandler();

        // Act
        var result = handler.HandlePacket(packet.Object);

        // Assert
        result.Should().Be(responsePacket.Object);
        packet.Verify(p => p.CreateResponsePacket(PacketCode.AccessReject), Times.Once);
    }

    /// <summary>
    ///     Test case: HandlePacket should return AccessReject when User-Name is empty.
    ///     Scenario: AccessRequest has empty or whitespace User-Name. Handler returns AccessReject without calling auth.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HandlePacket_WithEmptyUsername_ShouldReturnAccessReject(string? username)
    {
        // Arrange
        var responsePacket = new Mock<IRadiusPacket>();
        var packet = CreateAccessRequestPacket(username ?? "", "password", responsePacket);
        var (handler, authProviderMock) = CreateHandler();

        // Act
        var result = handler.HandlePacket(packet.Object);

        // Assert
        result.Should().Be(responsePacket.Object);
        packet.Verify(p => p.CreateResponsePacket(PacketCode.AccessReject), Times.Once);
        authProviderMock.Verify(
            x => x.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     Test case: HandlePacket should return AccessReject when User-Password is empty.
    ///     Scenario: AccessRequest has empty or whitespace User-Password. Handler returns AccessReject without calling auth.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HandlePacket_WithEmptyPassword_ShouldReturnAccessReject(string? password)
    {
        // Arrange
        var responsePacket = new Mock<IRadiusPacket>();
        var packet = CreateAccessRequestPacket("john", password ?? "", responsePacket);
        var (handler, authProviderMock) = CreateHandler();

        // Act
        var result = handler.HandlePacket(packet.Object);

        // Assert
        result.Should().Be(responsePacket.Object);
        packet.Verify(p => p.CreateResponsePacket(PacketCode.AccessReject), Times.Once);
        authProviderMock.Verify(
            x => x.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     Test case: HandlePacket should return AccessAccept when credentials are valid.
    ///     Scenario: IRadiusAuthProvider.ValidateCredentialsAsync returns true. Handler returns AccessAccept with
    ///     Acct-Interim-Interval attribute.
    /// </summary>
    [Fact]
    public void HandlePacket_WhenAuthSucceeds_ShouldReturnAccessAcceptWithAcctInterimInterval()
    {
        // Arrange
        var responsePacket = new Mock<IRadiusPacket>();
        var packet = CreateAccessRequestPacket("john", "password", responsePacket);
        var (handler, authProviderMock) = CreateHandler(tenantId: Guid.NewGuid());
        authProviderMock.Setup(x => x.ValidateCredentialsAsync("john", "password", It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = handler.HandlePacket(packet.Object);

        // Assert
        result.Should().Be(responsePacket.Object);
        packet.Verify(p => p.CreateResponsePacket(PacketCode.AccessAccept), Times.Once);
        responsePacket.Verify(p => p.AddAttribute("Acct-Interim-Interval", 60), Times.Once);
    }

    /// <summary>
    ///     Test case: HandlePacket should return AccessReject when credentials are invalid.
    ///     Scenario: IRadiusAuthProvider.ValidateCredentialsAsync returns false. Handler returns AccessReject.
    /// </summary>
    [Fact]
    public void HandlePacket_WhenAuthFails_ShouldReturnAccessReject()
    {
        // Arrange
        var responsePacket = new Mock<IRadiusPacket>();
        var packet = CreateAccessRequestPacket("john", "wrong", responsePacket);
        var (handler, authProviderMock) = CreateHandler(tenantId: Guid.NewGuid());
        authProviderMock.Setup(x => x.ValidateCredentialsAsync("john", "wrong", It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = handler.HandlePacket(packet.Object);

        // Assert
        result.Should().Be(responsePacket.Object);
        packet.Verify(p => p.CreateResponsePacket(PacketCode.AccessReject), Times.Once);
    }

    /// <summary>
    ///     Test case: HandlePacket should use DefaultTenantId when username has no realm.
    ///     Scenario: Username is "john" with no @tenant. Options have DefaultTenantId. Auth is called with that tenant.
    /// </summary>
    [Fact]
    public void HandlePacket_WithPlainUsername_ShouldUseDefaultTenantId()
    {
        // Arrange
        var defaultTenantId = Guid.NewGuid();
        var responsePacket = new Mock<IRadiusPacket>();
        var packet = CreateAccessRequestPacket("john", "password", responsePacket);
        var (handler, authProviderMock) = CreateHandler(tenantId: defaultTenantId);
        authProviderMock.Setup(x => x.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        handler.HandlePacket(packet.Object);

        // Assert
        authProviderMock.Verify(
            x => x.ValidateCredentialsAsync("john", "password", defaultTenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     Test case: HandlePacket should call auth with null tenant when DefaultTenantId is null and no realm in username.
    ///     Scenario: Username is "john" with no realm. Options have no DefaultTenantId. Auth receives null tenant and
    ///     returns false, so handler returns AccessReject.
    /// </summary>
    [Fact]
    public void HandlePacket_WithNullDefaultTenantAndNoRealm_ShouldCallAuthWithNullTenant()
    {
        // Arrange - no DefaultTenantId
        var responsePacket = new Mock<IRadiusPacket>();
        var packet = CreateAccessRequestPacket("john", "password", responsePacket);
        var (handler, authProviderMock) = CreateHandler(tenantId: null);
        authProviderMock.Setup(x => x.ValidateCredentialsAsync("john", "password", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = handler.HandlePacket(packet.Object);

        // Assert
        result.Should().Be(responsePacket.Object);
        authProviderMock.Verify(
            x => x.ValidateCredentialsAsync("john", "password", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     Test case: HandlePacket should parse tenant from username realm format.
    ///     Scenario: Username is "john@tenant-guid". Auth is called with parsed username and tenant.
    /// </summary>
    [Fact]
    public void HandlePacket_WithRealmFormat_ShouldParseTenantAndUsername()
    {
        // Arrange
        var tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var defaultTenantId = Guid.NewGuid();
        var responsePacket = new Mock<IRadiusPacket>();
        var packet = CreateAccessRequestPacket($"john@{tenantId}", "password", responsePacket);
        var (handler, authProviderMock) = CreateHandler(tenantId: defaultTenantId);
        authProviderMock.Setup(x => x.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        handler.HandlePacket(packet.Object);

        // Assert
        authProviderMock.Verify(
            x => x.ValidateCredentialsAsync("john", "password", tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static (AuthBackedRadiusPacketHandler Handler, Mock<IRadiusAuthProvider> AuthProviderMock) CreateHandler(
        Guid? tenantId = null)
    {
        var authProviderMock = new Mock<IRadiusAuthProvider>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IRadiusAuthProvider))).Returns(authProviderMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(It.Is<Type>(t => t == typeof(IRadiusAuthProvider))))
            .Returns(authProviderMock.Object);

        var serviceScopeMock = new Mock<IServiceScope>();
        serviceScopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(serviceScopeMock.Object);

        var options = new RadiusServerOptions { DefaultTenantId = tenantId };
        var loggerMock = new Mock<ILogger<AuthBackedRadiusPacketHandler>>();

        var handler = new AuthBackedRadiusPacketHandler(
            scopeFactoryMock.Object,
            Options.Create(options),
            loggerMock.Object);

        return (handler, authProviderMock);
    }
}
