using System.Security.Claims;
using BoilerPlate.Diagnostics.Database.Entities;
using Microsoft.Extensions.Logging;
using BoilerPlate.Diagnostics.EventLogs.MongoDb.Services;
using BoilerPlate.Diagnostics.WebApi.Controllers.OData;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BoilerPlate.Diagnostics.WebApi.Tests.Controllers.OData;

/// <summary>
///     Unit tests for <see cref="EventLogsODataController" />.
/// </summary>
public class EventLogsODataControllerTests
{
    private readonly Mock<IEventLogsRawQueryService> _rawQueryServiceMock;
    private readonly Mock<ILogger<EventLogsODataController>> _loggerMock;

    public EventLogsODataControllerTests()
    {
        _rawQueryServiceMock = new Mock<IEventLogsRawQueryService>();
        _loggerMock = new Mock<ILogger<EventLogsODataController>>();
    }

    /// <summary>
    ///     Scenario: Service Administrator calls Get with OData params.
    ///     Expected: Returns 200 OK with value and @odata.count; QueryAsync called with tenantId=null.
    /// </summary>
    [Fact]
    public async Task Get_AsServiceAdmin_ReturnsOkWithValueAndCount()
    {
        var entries = new List<EventLogEntry> { new() { Id = 1, Message = "Test" } };
        _rawQueryServiceMock
            .Setup(s => s.QueryAsync(null, null, null, true, 100, 0, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries, 50L));

        var controller = CreateController(serviceAdmin: true);
        SetupRequestQuery(controller, count: "true");

        var result = await controller.Get(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<Dictionary<string, object?>>().Subject;
        response["value"].Should().BeEquivalentTo(entries);
        response["@odata.count"].Should().Be(50L);
    }

    /// <summary>
    ///     Scenario: Tenant Administrator with tenant_id claim calls Get.
    ///     Expected: QueryAsync called with tenantId from claims.
    /// </summary>
    [Fact]
    public async Task Get_AsTenantAdmin_CallsQueryAsyncWithTenantId()
    {
        var tenantId = Guid.NewGuid();
        _rawQueryServiceMock
            .Setup(s => s.QueryAsync(tenantId, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<EventLogEntry>(), 0L));

        var controller = CreateController(serviceAdmin: false, tenantId: tenantId);
        SetupRequestQuery(controller);

        await controller.Get(CancellationToken.None);

        _rawQueryServiceMock.Verify(s => s.QueryAsync(tenantId, null, null, true, 100, 0, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Scenario: $filter=Level eq 'Error' is passed.
    ///     Expected: QueryAsync called with levelFilter="Error".
    /// </summary>
    [Fact]
    public async Task Get_WithLevelFilter_CallsQueryAsyncWithLevelFilter()
    {
        _rawQueryServiceMock
            .Setup(s => s.QueryAsync(It.IsAny<Guid?>(), "Error", It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<EventLogEntry>(), 0L));

        var controller = CreateController(serviceAdmin: true);
        SetupRequestQuery(controller, filter: "Level eq 'Error'");

        await controller.Get(CancellationToken.None);

        _rawQueryServiceMock.Verify(s => s.QueryAsync(null, "Error", null, true, 100, 0, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Scenario: Raw query service throws an exception.
    ///     Expected: Returns 500 with error details.
    /// </summary>
    [Fact]
    public async Task Get_WhenServiceThrows_Returns500WithErrorDetails()
    {
        _rawQueryServiceMock
            .Setup(s => s.QueryAsync(It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MongoDB unavailable"));

        var controller = CreateController(serviceAdmin: true);
        SetupRequestQuery(controller);

        var result = await controller.Get(CancellationToken.None);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
        statusResult.Value.Should().NotBeNull();
    }

    /// <summary>
    ///     Scenario: Get(key) with valid key and entry found.
    ///     Expected: Returns 200 OK with the event log entry.
    /// </summary>
    [Fact]
    public async Task GetById_WhenEntryExists_ReturnsOk()
    {
        var entry = new EventLogEntry { Id = 12345, Message = "Test log" };
        _rawQueryServiceMock
            .Setup(s => s.GetByIdAsync(12345, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var controller = CreateController(serviceAdmin: true);

        var result = await controller.Get(12345, CancellationToken.None);

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
            .Setup(s => s.GetByIdAsync(It.IsAny<long>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventLogEntry?)null);

        var controller = CreateController(serviceAdmin: true);

        var result = await controller.Get(99999, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    private EventLogsODataController CreateController(bool serviceAdmin, Guid? tenantId = null)
    {
        var claims = new List<Claim>();
        if (serviceAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Service Administrator"));
        else if (tenantId.HasValue)
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));

        return new EventLogsODataController(_rawQueryServiceMock.Object, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };
    }

    private static void SetupRequestQuery(
        EventLogsODataController controller,
        string? filter = null,
        string? orderBy = null,
        string? top = null,
        string? skip = null,
        string? count = null)
    {
        var pairs = new List<string>();
        if (filter != null) pairs.Add($"$filter={Uri.EscapeDataString(filter)}");
        if (orderBy != null) pairs.Add($"$orderby={Uri.EscapeDataString(orderBy)}");
        if (top != null) pairs.Add($"$top={top}");
        if (skip != null) pairs.Add($"$skip={skip}");
        if (count != null) pairs.Add($"$count={count}");
        var queryString = pairs.Count > 0 ? "?" + string.Join("&", pairs) : "";
        controller.HttpContext!.Request.QueryString = new QueryString(queryString);
    }
}
