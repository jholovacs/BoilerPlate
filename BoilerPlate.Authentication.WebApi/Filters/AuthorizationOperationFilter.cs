using BoilerPlate.Authentication.WebApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;

namespace BoilerPlate.Authentication.WebApi.Filters;

/// <summary>
/// Swagger operation filter to add authorization requirements to the API documentation
/// </summary>
public class AuthorizationOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var authAttributes = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>()
            .Union(context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>() ?? Enumerable.Empty<AuthorizeAttribute>())
            .ToList();

        var allowAnonymousAttributes = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AllowAnonymousAttribute>()
            .Union(context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>() ?? Enumerable.Empty<AllowAnonymousAttribute>())
            .ToList();

        // If there's an AllowAnonymous attribute, don't add security requirements
        if (allowAnonymousAttributes.Any())
        {
            return;
        }

        // If there are authorization attributes, add security requirements
        if (authAttributes.Any())
        {
            var roles = authAttributes
                .Where(a => !string.IsNullOrEmpty(a.Roles))
                .SelectMany(a => a.Roles!.Split(','))
                .Select(r => r.Trim())
                .Distinct()
                .ToList();

            var policies = authAttributes
                .Where(a => !string.IsNullOrEmpty(a.Policy))
                .Select(a => a.Policy!)
                .Distinct()
                .ToList();

            // Map policy names to human-readable role descriptions
            var policyRoleMap = new Dictionary<string, string>
            {
                { AuthorizationPolicies.ServiceAdministrator, "Service Administrator" },
                { AuthorizationPolicies.TenantAdministrator, "Tenant Administrator" },
                { AuthorizationPolicies.UserAdministrator, "User Administrator" },
                { AuthorizationPolicies.UserManagement, "Service Administrator, Tenant Administrator, or User Administrator" },
                { AuthorizationPolicies.RoleManagement, "Service Administrator or Tenant Administrator" },
                { AuthorizationPolicies.ODataAccess, "Service Administrator or Tenant Administrator" }
            };

            // Build description for required permissions
            var permissionDescription = new List<string>();
            if (roles.Any())
            {
                permissionDescription.Add($"Required Roles: {string.Join(", ", roles)}");
            }
            if (policies.Any())
            {
                var policyDescriptions = policies.Select(p => 
                    policyRoleMap.TryGetValue(p, out var description) 
                        ? $"{p} ({description})" 
                        : p).ToList();
                permissionDescription.Add($"Required Policies: {string.Join(", ", policyDescriptions)}");
                
                // Extract roles from policies for Swagger security requirements
                foreach (var policy in policies)
                {
                    if (policy == AuthorizationPolicies.ServiceAdministrator)
                    {
                        roles.Add("Service Administrator");
                    }
                    else if (policy == AuthorizationPolicies.TenantAdministrator)
                    {
                        roles.Add("Tenant Administrator");
                    }
                    else if (policy == AuthorizationPolicies.UserAdministrator)
                    {
                        roles.Add("User Administrator");
                    }
                    else if (policy == AuthorizationPolicies.UserManagement)
                    {
                        roles.Add("Service Administrator");
                        roles.Add("Tenant Administrator");
                        roles.Add("User Administrator");
                    }
                    else if (policy == AuthorizationPolicies.RoleManagement || policy == AuthorizationPolicies.ODataAccess)
                    {
                        roles.Add("Service Administrator");
                        roles.Add("Tenant Administrator");
                    }
                }
                
                // Remove duplicates
                roles = roles.Distinct().ToList();
            }

            if (permissionDescription.Any())
            {
                // Add to operation description
                if (!string.IsNullOrEmpty(operation.Description))
                {
                    operation.Description += "\n\n**Authorization:** " + string.Join(" | ", permissionDescription);
                }
                else
                {
                    operation.Description = "**Authorization:** " + string.Join(" | ", permissionDescription);
                }

                // Add security requirement with roles
                operation.Security = new List<OpenApiSecurityRequirement>
                {
                    new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            roles.Any() ? roles : new List<string>()
                        }
                    }
                };
            }
            else
            {
                // Authorization required but no specific roles/policies
                if (!string.IsNullOrEmpty(operation.Description))
                {
                    operation.Description += "\n\n**Authorization:** Authentication required";
                }
                else
                {
                    operation.Description = "**Authorization:** Authentication required";
                }

                operation.Security = new List<OpenApiSecurityRequirement>
                {
                    new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            new List<string>()
                        }
                    }
                };
            }
        }
    }
}
