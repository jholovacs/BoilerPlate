namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
/// Request model for updating a role
/// </summary>
public class UpdateRoleRequest
{
    /// <summary>
    /// Role name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Role description
    /// </summary>
    public string? Description { get; set; }
}
