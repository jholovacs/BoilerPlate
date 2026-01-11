namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     Request model for OData queries sent via POST to handle long query strings that exceed URL length limitations
///     The query string should be in the format: $filter=...&$select=...&$orderby=... etc.
/// </summary>
public class ODataQueryRequest
{
    /// <summary>
    ///     OData query string (e.g., "$filter=Name eq 'Test'&$select=Id,Name&$orderby=Name desc")
    ///     This allows sending complex queries that exceed URL length limitations via POST body
    /// </summary>
    public string Query { get; set; } = string.Empty;
}
