using System.Text;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OData.Edm;

namespace BoilerPlate.Authentication.WebApi.Tests.Helpers;

/// <summary>
///     Unit tests for ODataQueryHelper, including malformed input and injection-like patterns.
/// </summary>
public class ODataQueryHelperTests
{
    private static readonly IEdmModel EdmModel = ODataConfiguration.GetEdmModel();

    #region ReadQueryStringFromBodyAsync Tests

    /// <summary>
    ///     Test case: ReadQueryStringFromBodyAsync should return null when body is empty.
    ///     Scenario: Request has text/plain content type but empty body. Returns null.
    /// </summary>
    [Fact]
    public async Task ReadQueryStringFromBodyAsync_WithEmptyBody_ShouldReturnNull()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.ContentType = "text/plain";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(""));

        // Act
        var result = await ODataQueryHelper.ReadQueryStringFromBodyAsync(context.Request);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: ReadQueryStringFromBodyAsync should return query string from text/plain body.
    ///     Scenario: Request has text/plain body with OData query. Returns the query string.
    /// </summary>
    [Fact]
    public async Task ReadQueryStringFromBodyAsync_WithTextPlain_ShouldReturnBodyContent()
    {
        // Arrange
        var query = "$filter=Name eq 'Test'&$top=10";
        var context = new DefaultHttpContext();
        context.Request.ContentType = "text/plain";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(query));

        // Act
        var result = await ODataQueryHelper.ReadQueryStringFromBodyAsync(context.Request);

        // Assert
        result.Should().Be(query);
    }

    /// <summary>
    ///     Test case: ReadQueryStringFromBodyAsync should return query from JSON body.
    ///     Scenario: Request has application/json with {"Query": "..."}. Returns the query value.
    /// </summary>
    [Fact]
    public async Task ReadQueryStringFromBodyAsync_WithValidJson_ShouldReturnQueryValue()
    {
        // Arrange
        var query = "$filter=Name eq 'Test'";
        var json = $"{{\"Query\":\"{query.Replace("\"", "\\\"")}\"}}";
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var result = await ODataQueryHelper.ReadQueryStringFromBodyAsync(context.Request);

        // Assert
        result.Should().Be(query);
    }

    /// <summary>
    ///     Test case: ReadQueryStringFromBodyAsync should throw when JSON is malformed.
    ///     Scenario: Request has application/json but body is invalid JSON. JsonSerializer.Deserialize throws.
    /// </summary>
    [Fact]
    public async Task ReadQueryStringFromBodyAsync_WithMalformedJson_ShouldThrow()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{ invalid json }"));

        // Act
        var act = async () => await ODataQueryHelper.ReadQueryStringFromBodyAsync(context.Request);

        // Assert
        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    /// <summary>
    ///     Test case: ReadQueryStringFromBodyAsync should handle JSON with injection-like content in query.
    ///     Scenario: Query string contains characters that could be used for injection. The helper passes through
    ///     the raw string; validation happens in ApplyQueryFromBody. Returns the string as-is.
    /// </summary>
    [Fact]
    public async Task ReadQueryStringFromBodyAsync_WithInjectionLikeContentInJson_ShouldReturnAsIs()
    {
        // Arrange - OData boolean logic abuse pattern
        var query = "$filter=Name eq 'admin' or true eq true";
        var json = $"{{\"Query\":\"{query.Replace("\"", "\\\"")}\"}}";
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var result = await ODataQueryHelper.ReadQueryStringFromBodyAsync(context.Request);

        // Assert - ReadQueryStringFromBodyAsync does not validate; it passes through
        result.Should().Be(query);
    }

    /// <summary>
    ///     Test case: ReadQueryStringFromBodyAsync should return null for unsupported content type.
    ///     Scenario: Request has content type that is neither text/plain nor application/json. Returns null.
    /// </summary>
    [Fact]
    public async Task ReadQueryStringFromBodyAsync_WithUnsupportedContentType_ShouldReturnNull()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/xml";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("<query>$filter=test</query>"));

        // Act
        var result = await ODataQueryHelper.ReadQueryStringFromBodyAsync(context.Request);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: ReadQueryStringFromBodyAsync should handle JSON with null query property.
    ///     Scenario: JSON is {"Query": null}. Returns null.
    /// </summary>
    [Fact]
    public async Task ReadQueryStringFromBodyAsync_WithJsonNullQuery_ShouldReturnNull()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"Query\":null}"));

        // Act
        var result = await ODataQueryHelper.ReadQueryStringFromBodyAsync(context.Request);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ApplyQueryFromBody Tests

    /// <summary>
    ///     Test case: ApplyQueryFromBody should return query unchanged when query string is null or empty.
    ///     Scenario: queryStringFromBody is null. Returns the original queryable.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyQueryFromBody_WithNullOrEmptyQuery_ShouldReturnQueryUnchanged(string? queryString)
    {
        // Arrange
        var data = new List<Tenant> { new() { Id = Guid.NewGuid(), Name = "Test" } };
        var query = data.AsQueryable();
        var context = CreateHttpContext();

        // Act
        var result = ODataQueryHelper.ApplyQueryFromBody(query, queryString!, context, EdmModel, "Tenants");

        // Assert
        result.Should().BeSameAs(query);
        result.Should().HaveCount(1);
    }

    /// <summary>
    ///     Test case: ApplyQueryFromBody should throw when entity set is not found.
    ///     Scenario: entitySetName does not exist in EDM model. Throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void ApplyQueryFromBody_WithInvalidEntitySetName_ShouldThrow()
    {
        // Arrange
        var data = new List<Tenant>().AsQueryable();
        var context = CreateHttpContext();

        // Act
        var act = () => ODataQueryHelper.ApplyQueryFromBody(data, "$top=5", context, EdmModel, "NonExistentSet");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*NonExistentSet*");
    }

    /// <summary>
    ///     Test case: ApplyQueryFromBody should throw when $filter has malformed syntax.
    ///     Scenario: $filter has invalid OData expression syntax. OData parser throws; wrapped in InvalidOperationException.
    /// </summary>
    [Fact]
    public void ApplyQueryFromBody_WithMalformedFilter_ShouldThrow()
    {
        // Arrange
        var data = new List<Tenant>().AsQueryable();
        var context = CreateHttpContext();

        // Act
        var act = () => ODataQueryHelper.ApplyQueryFromBody(data, "$filter=Name eq 'unclosed", context, EdmModel, "Tenants");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Failed to parse*");
    }

    /// <summary>
    ///     Test case: ApplyQueryFromBody should throw when $filter references non-existent property.
    ///     Scenario: $filter uses a property not in the entity type. OData validation fails.
    /// </summary>
    [Fact]
    public void ApplyQueryFromBody_WithInvalidPropertyInFilter_ShouldThrow()
    {
        // Arrange
        var data = new List<Tenant>().AsQueryable();
        var context = CreateHttpContext();

        // Act
        var act = () => ODataQueryHelper.ApplyQueryFromBody(data, "$filter=NonExistentProperty eq 'x'", context, EdmModel, "Tenants");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Failed to parse*");
    }

    /// <summary>
    ///     Test case: ApplyQueryFromBody should apply valid $filter and return filtered results.
    ///     Scenario: Valid $filter=Name eq 'Alpha'. Returns only matching entities.
    /// </summary>
    [Fact]
    public void ApplyQueryFromBody_WithValidFilter_ShouldApplyFilter()
    {
        // Arrange
        var data = new List<Tenant>
        {
            new() { Id = Guid.NewGuid(), Name = "Alpha" },
            new() { Id = Guid.NewGuid(), Name = "Beta" }
        };
        var query = data.AsQueryable();
        var context = CreateHttpContext();

        // Act
        var result = ODataQueryHelper.ApplyQueryFromBody(query, "$filter=Name eq 'Alpha'", context, EdmModel, "Tenants");

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Alpha");
    }

    /// <summary>
    ///     Test case: ApplyQueryFromBody should handle injection-like boolean logic in $filter.
    ///     Scenario: $filter attempts "or true eq true" to return all records. OData parses it; for in-memory
    ///     queryable the filter is applied. The expression "true eq true" evaluates to true, so "X or true" matches all.
    ///     Expected: All items returned (the injection pattern works at OData level - this tests that we don't crash).
    /// </summary>
    [Fact]
    public void ApplyQueryFromBody_WithInjectionLikeBooleanOr_ShouldNotThrow()
    {
        // Arrange - OData uses "or" and "eq" for boolean logic
        var data = new List<Tenant>
        {
            new() { Id = Guid.NewGuid(), Name = "Admin" },
            new() { Id = Guid.NewGuid(), Name = "User" }
        };
        var query = data.AsQueryable();
        var context = CreateHttpContext();

        // Act - "Name eq 'x' or true eq true" is valid OData; the "or true" part matches everything
        var result = ODataQueryHelper.ApplyQueryFromBody(query, "$filter=Name eq 'x' or true eq true", context, EdmModel, "Tenants");

        // Assert - Should not throw; OData parses and applies. Result: both items (or true matches all)
        result.Should().HaveCount(2);
    }

    /// <summary>
    ///     Test case: ApplyQueryFromBody should handle $filter with escaped single quotes in string.
    ///     Scenario: $filter contains doubled single quote for escaping. OData handles it.
    /// </summary>
    [Fact]
    public void ApplyQueryFromBody_WithEscapedQuotesInFilter_ShouldApplyCorrectly()
    {
        // Arrange
        var data = new List<Tenant>
        {
            new() { Id = Guid.NewGuid(), Name = "O'Brien" }
        };
        var query = data.AsQueryable();
        var context = CreateHttpContext();

        // Act - OData escapes single quote by doubling: 'O''Brien'
        var result = ODataQueryHelper.ApplyQueryFromBody(query, "$filter=Name eq 'O''Brien'", context, EdmModel, "Tenants");

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("O'Brien");
    }

    /// <summary>
    ///     Test case: ApplyQueryFromBody should respect $top limit.
    ///     Scenario: Valid $top=1. Returns at most 1 item.
    /// </summary>
    [Fact]
    public void ApplyQueryFromBody_WithTop_ShouldLimitResults()
    {
        // Arrange
        var data = new List<Tenant>
        {
            new() { Id = Guid.NewGuid(), Name = "A" },
            new() { Id = Guid.NewGuid(), Name = "B" }
        };
        var query = data.AsQueryable();
        var context = CreateHttpContext();

        // Act
        var result = ODataQueryHelper.ApplyQueryFromBody(query, "$top=1", context, EdmModel, "Tenants");

        // Assert
        result.Should().HaveCount(1);
    }

    /// <summary>
    ///     Test case: ApplyQueryFromBody should throw when IHttpRequestFeature is not available.
    ///     Scenario: HttpContext has no IHttpRequestFeature. Throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void ApplyQueryFromBody_WithMissingHttpRequestFeature_ShouldThrow()
    {
        // Arrange - create context without the feature
        var context = new DefaultHttpContext();
        var features = new FeatureCollection();
        context.Initialize(features);
        var data = new List<Tenant>().AsQueryable();

        // Act
        var act = () => ODataQueryHelper.ApplyQueryFromBody(data, "$top=5", context, EdmModel, "Tenants");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IHttpRequestFeature*");
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        // DefaultHttpContext includes IHttpRequestFeature; ensure it exists
        var feature = context.Features.Get<IHttpRequestFeature>();
        if (feature == null)
        {
            context.Features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        }
        return context;
    }

    #endregion
}
