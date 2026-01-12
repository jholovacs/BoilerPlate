namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     Request model for OData queries sent via POST to handle long query strings that exceed URL length limitations
///     The query string should be in the format: <c>$filter</c>=...&amp;<c>$select</c>=...&amp;<c>$orderby</c>=... etc.
/// </summary>
public class ODataQueryRequest
{
    /// <summary>
    ///     OData query string (e.g., <c>$filter</c>=Name eq 'Test'&amp;<c>$select</c>=Id,Name&amp;<c>$orderby</c>=Name desc")
    ///     This allows sending complex queries that exceed URL length limitations via POST body
    /// </summary>
    public string Query { get; set; } = string.Empty;
}
