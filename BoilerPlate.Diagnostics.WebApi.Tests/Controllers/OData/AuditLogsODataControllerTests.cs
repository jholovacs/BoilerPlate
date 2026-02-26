using System.Security.Claims;
using BoilerPlate.Diagnostics.AuditLogs.MongoDb.Services;
using Microsoft.Extensions.Logging;
using BoilerPlate.Diagnostics.Database.Entities;
using BoilerPlate.Diagnostics.WebApi.Controllers.OData;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BoilerPlate.Diagnostics.WebApi.Tests.Controllers.OData;

/// <summary>
///     Unit tests for <see cref="AuditLogsODataController" />.
/// </summary>
public class AuditLogsODataControllerTests
{
    private readonly Mock<IAuditLogsRawQueryService> _rawQueryServiceMock;
    private readonly Mock<ILogger<AuditLogsODataController>> _loggerMock;

    public AuditLogsODataControllerTests()
    {
        _rawQueryServiceMock = new Mock<IAuditLogsRawQueryService>();
        _loggerMock = new Mock<ILogger<AuditLogsODataController>>();
    }

    /// <summary>
    ///     Scenario: Service Administrator calls Get with $orderby=EventTimestamp desc, $top=50, $skip=0, $count=true.
    ///     Expected: Returns 200 OK with value array and @odata.count; QueryAsync called with tenantId=null, orderByDesc=true.
    /// </summary>
    [Fact]
    public async Task Get_AsServiceAdmin_WithODataParams_ReturnsOkWithValueAndCount()
    {
        var entries = new List<AuditLogEntry> { new() { Id = "1", EventType = "Test" } };
        _rawQueryServiceMock
            .Setup(s => s.QueryAsync(null, true, 50, 0, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries, 100L));

        var controller = CreateController(serviceAdmin: true);
        SetupRequestQuery(controller, orderBy: "EventTimestamp desc", top: "50", skip: "0", count: "true");

        var result = await controller.Get(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<Dictionary<string, object?>>().Subject;
        response["value"].Should().BeEquivalentTo(entries);
        response["@odata.count"].Should().Be(100L);
        _rawQueryServiceMock.Verify(s => s.QueryAsync(null, true, 50, 0, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Scenario: Tenant Administrator calls Get; tenant ID is in claims.
    ///     Expected: QueryAsync called with tenantId from claims.
    /// </summary>
    [Fact]
    public async Task Get_AsTenantAdmin_CallsQueryAsyncWithTenantId()
    {
        var tenantId = Guid.NewGuid();
        _rawQueryServiceMock
            .Setup(s => s.QueryAsync(tenantId, It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogEntry>(), 0L));

        var controller = CreateController(serviceAdmin: false, tenantId: tenantId);
        SetupRequestQuery(controller);

        await controller.Get(CancellationToken.None);

        _rawQueryServiceMock.Verify(s => s.QueryAsync(tenantId, true, 100, 0, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Scenario: $orderby=EventTimestamp asc is passed.
    ///     Expected: QueryAsync called with orderByDesc=false.
    /// </summary>
    [Fact]
    public async Task Get_WithOrderByAsc_CallsQueryAsyncWithOrderByDescFalse()
    {
        _rawQueryServiceMock
            .Setup(s => s.QueryAsync(It.IsAny<Guid?>(), false, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogEntry>(), 0L));

        var controller = CreateController(serviceAdmin: true);
        SetupRequestQuery(controller, orderBy: "EventTimestamp asc");

        await controller.Get(CancellationToken.None);

        _rawQueryServiceMock.Verify(s => s.QueryAsync(null, false, 100, 0, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Scenario: $top=1000 is passed (exceeds MaxTop 500).
    ///     Expected: QueryAsync called with top=500.
    /// </summary>
    [Fact]
    public async Task Get_WithTopExceedingMax_CapsAtMaxTop()
    {
        _rawQueryServiceMock
            .Setup(s => s.QueryAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), 500, It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogEntry>(), 0L));

        var controller = CreateController(serviceAdmin: true);
        SetupRequestQuery(controller, top: "1000");

        await controller.Get(CancellationToken.None);

        _rawQueryServiceMock.Verify(s => s.QueryAsync(null, true, 500, 0, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Scenario: Raw query service throws an exception.
    ///     Expected: Returns 500 with error details in response body.
    /// </summary>
    [Fact]
    public async Task Get_WhenServiceThrows_Returns500WithErrorDetails()
    {
        _rawQueryServiceMock
            .Setup(s => s.QueryAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MongoDB connection failed"));

        var controller = CreateController(serviceAdmin: true);
        SetupRequestQuery(controller);

        var result = await controller.Get(CancellationToken.None);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
        var body = statusResult.Value;
        body.Should().NotBeNull();
        body!.GetType().GetProperty("error")!.GetValue(body).Should().Be("MongoDB connection failed");
    }

    /// <summary>
    ///     Scenario: Get(key) with valid key and entry found.
    ///     Expected: Returns 200 OK with the audit log entry.
    /// </summary>
    [Fact]
    public async Task GetById_WhenEntryExists_ReturnsOk()
    {
        var entry = new AuditLogEntry { Id = "507f1f77bcf86cd799439011", EventType = "UserCreated" };
        _rawQueryServiceMock
            .Setup(s => s.GetByIdAsync("507f1f77bcf86cd799439011", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var controller = CreateController(serviceAdmin: true);

        var result = await controller.Get("507f1f77bcf86cd799439011", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(entry);
    }

    /// <summary>
    ///     Scenario: Get(key) when entry is not found.
    ///     Expected: Returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task GetById_WhenEntryNotFound_ReturnsNotFound()
    {
        _rawQueryServiceMock
            .Setup(s => s.GetByIdAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLogEntry?)null);

        var controller = CreateController(serviceAdmin: true);

        var result = await controller.Get("nonexistent", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    /// <summary>
    ///     Scenario: Tenant Admin calls Get(key); entry belongs to another tenant.
    ///     Expected: GetByIdAsync called with tenantId; returns 404 when service returns null.
    /// </summary>
    [Fact]
    public async Task GetById_AsTenantAdmin_CallsGetByIdAsyncWithTenantId()
    {
        var tenantId = Guid.NewGuid();
        _rawQueryServiceMock
            .Setup(s => s.GetByIdAsync("key1", tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLogEntry?)null);

        var controller = CreateController(serviceAdmin: false, tenantId: tenantId);

        var result = await controller.Get("key1", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _rawQueryServiceMock.Verify(s => s.GetByIdAsync("key1", tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private AuditLogsODataController CreateController(bool serviceAdmin, Guid? tenantId = null)
    {
        var claims = new List<Claim>();
        if (serviceAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Service Administrator"));
        else if (tenantId.HasValue)
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));

        return new AuditLogsODataController(_rawQueryServiceMock.Object, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(
                        new System.Security.Claims.ClaimsIdentity(claims, "Test"))
                }
            }
        };
    }

    private static void SetupRequestQuery(
        AuditLogsODataController controller,
        string? orderBy = null,
        string? top = null,
        string? skip = null,
        string? count = null)
    {
        var pairs = new List<string>();
        if (orderBy != null) pairs.Add($"$orderby={Uri.EscapeDataString(orderBy)}");
        if (top != null) pairs.Add($"$top={top}");
        if (skip != null) pairs.Add($"$skip={skip}");
        if (count != null) pairs.Add($"$count={count}");
        var queryString = pairs.Count > 0 ? "?" + string.Join("&", pairs) : "";
        controller.HttpContext!.Request.QueryString = new QueryString(queryString);
    }
}
