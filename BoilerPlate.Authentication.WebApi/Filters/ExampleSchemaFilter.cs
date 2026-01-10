using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.WebApi.Controllers;
using BoilerPlate.Authentication.WebApi.Models;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BoilerPlate.Authentication.WebApi.Filters;

/// <summary>
/// Swagger schema filter to add realistic examples to request/response models
/// </summary>
public class ExampleSchemaFilter : ISchemaFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Example != null)
        {
            return; // Example already set
        }

        var type = context.Type;

        // Handle arrays/enumerables first - set examples on the array schema
        if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();
            if (genericType == typeof(IEnumerable<>) || 
                genericType == typeof(List<>) ||
                genericType == typeof(ICollection<>) ||
                genericType == typeof(IList<>))
            {
                var elementType = type.GetGenericArguments()[0];
                if (elementType == typeof(TenantDto))
                {
                    schema.Example = new OpenApiArray
                    {
                        new OpenApiObject
                        {
                            ["id"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                            ["name"] = new OpenApiString("Acme Corporation"),
                            ["description"] = new OpenApiString("Main enterprise tenant for Acme Corporation"),
                            ["isActive"] = new OpenApiBoolean(true),
                            ["createdAt"] = new OpenApiString("2025-01-15T10:30:00Z"),
                            ["updatedAt"] = new OpenApiString("2025-01-16T14:20:00Z")
                        },
                        new OpenApiObject
                        {
                            ["id"] = new OpenApiString("6ba7b810-9dad-11d1-80b4-00c04fd430c8"),
                            ["name"] = new OpenApiString("TechStart Solutions"),
                            ["description"] = new OpenApiString("Technology startup tenant"),
                            ["isActive"] = new OpenApiBoolean(true),
                            ["createdAt"] = new OpenApiString("2025-01-14T09:00:00Z"),
                            ["updatedAt"] = new OpenApiString("2025-01-14T09:00:00Z")
                        }
                    };
                    return;
                }
                else if (elementType == typeof(UserDto))
                {
                    schema.Example = new OpenApiArray
                    {
                        new OpenApiObject
                        {
                            ["id"] = new OpenApiString("7c9e6679-7425-40de-944b-e07fc1f90ae7"),
                            ["tenantId"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                            ["userName"] = new OpenApiString("jdoe"),
                            ["email"] = new OpenApiString("john.doe@acme.com"),
                            ["firstName"] = new OpenApiString("John"),
                            ["lastName"] = new OpenApiString("Doe"),
                            ["phoneNumber"] = new OpenApiString("+1-555-0123"),
                            ["emailConfirmed"] = new OpenApiBoolean(true),
                            ["phoneNumberConfirmed"] = new OpenApiBoolean(false),
                            ["isActive"] = new OpenApiBoolean(true),
                            ["createdAt"] = new OpenApiString("2025-01-10T08:15:00Z"),
                            ["updatedAt"] = new OpenApiString("2025-01-12T16:45:00Z"),
                            ["roles"] = new OpenApiArray
                            {
                                new OpenApiString("User Administrator")
                            }
                        },
                        new OpenApiObject
                        {
                            ["id"] = new OpenApiString("8d8e7788-8536-51ef-a551-f18gd2g01bf8"),
                            ["tenantId"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                            ["userName"] = new OpenApiString("jsmith"),
                            ["email"] = new OpenApiString("jane.smith@acme.com"),
                            ["firstName"] = new OpenApiString("Jane"),
                            ["lastName"] = new OpenApiString("Smith"),
                            ["phoneNumber"] = new OpenApiString("+1-555-0456"),
                            ["emailConfirmed"] = new OpenApiBoolean(true),
                            ["phoneNumberConfirmed"] = new OpenApiBoolean(true),
                            ["isActive"] = new OpenApiBoolean(true),
                            ["createdAt"] = new OpenApiString("2025-01-11T11:20:00Z"),
                            ["updatedAt"] = new OpenApiString("2025-01-13T13:30:00Z"),
                            ["roles"] = new OpenApiArray
                            {
                                new OpenApiString("Employee"),
                                new OpenApiString("Project Manager")
                            }
                        }
                    };
                    return;
                }
                else if (elementType == typeof(RoleDto))
                {
                    schema.Example = new OpenApiArray
                    {
                        new OpenApiObject
                        {
                            ["id"] = new OpenApiString("6ba7b810-9dad-11d1-80b4-00c04fd430c8"),
                            ["tenantId"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                            ["name"] = new OpenApiString("Project Manager"),
                            ["normalizedName"] = new OpenApiString("PROJECT MANAGER")
                        },
                        new OpenApiObject
                        {
                            ["id"] = new OpenApiString("7cb8c921-0ebe-22e2-91c5-11d15ge541d9"),
                            ["tenantId"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                            ["name"] = new OpenApiString("Team Lead"),
                            ["normalizedName"] = new OpenApiString("TEAM LEAD")
                        }
                    };
                    return;
                }
                else if (elementType == typeof(string))
                {
                    // Handle string arrays (like role lists)
                    if (type == typeof(IEnumerable<string>) || type == typeof(List<string>) || type == typeof(ICollection<string>))
                    {
                        schema.Example = new OpenApiArray
                        {
                            new OpenApiString("Project Manager"),
                            new OpenApiString("Team Lead")
                        };
                        return;
                    }
                }
            }
        }

        // Create tenant request examples
        if (type == typeof(CreateTenantRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["name"] = new OpenApiString("Acme Corporation"),
                ["description"] = new OpenApiString("Main enterprise tenant for Acme Corporation and its subsidiaries")
            };
        }
        else if (type == typeof(UpdateTenantRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["name"] = new OpenApiString("Acme Corporation - Updated"),
                ["description"] = new OpenApiString("Updated description for Acme Corporation tenant"),
                ["isActive"] = new OpenApiBoolean(true)
            };
        }
        else if (type == typeof(TenantDto))
        {
            schema.Example = new OpenApiObject
            {
                ["id"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                ["name"] = new OpenApiString("Acme Corporation"),
                ["description"] = new OpenApiString("Main enterprise tenant for Acme Corporation and its subsidiaries"),
                ["isActive"] = new OpenApiBoolean(true),
                ["createdAt"] = new OpenApiString("2025-01-15T10:30:00Z"),
                ["updatedAt"] = new OpenApiString("2025-01-16T14:20:00Z")
            };
        }

        // User request/response examples
        else if (type == typeof(RegisterRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["tenantId"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                ["email"] = new OpenApiString("john.doe@acme.com"),
                ["userName"] = new OpenApiString("jdoe"),
                ["password"] = new OpenApiString("SecurePass123!@#"),
                ["confirmPassword"] = new OpenApiString("SecurePass123!@#"),
                ["firstName"] = new OpenApiString("John"),
                ["lastName"] = new OpenApiString("Doe"),
                ["phoneNumber"] = new OpenApiString("+1-555-0123")
            };
        }
        else if (type == typeof(UpdateUserRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["firstName"] = new OpenApiString("John"),
                ["lastName"] = new OpenApiString("Doe"),
                ["email"] = new OpenApiString("john.doe@acme.com"),
                ["phoneNumber"] = new OpenApiString("+1-555-0123"),
                ["isActive"] = new OpenApiBoolean(true)
            };
        }
        else if (type == typeof(UserDto))
        {
            schema.Example = new OpenApiObject
            {
                ["id"] = new OpenApiString("7c9e6679-7425-40de-944b-e07fc1f90ae7"),
                ["tenantId"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                ["userName"] = new OpenApiString("jdoe"),
                ["email"] = new OpenApiString("john.doe@acme.com"),
                ["firstName"] = new OpenApiString("John"),
                ["lastName"] = new OpenApiString("Doe"),
                ["phoneNumber"] = new OpenApiString("+1-555-0123"),
                ["emailConfirmed"] = new OpenApiBoolean(true),
                ["phoneNumberConfirmed"] = new OpenApiBoolean(false),
                ["isActive"] = new OpenApiBoolean(true),
                ["createdAt"] = new OpenApiString("2025-01-10T08:15:00Z"),
                ["updatedAt"] = new OpenApiString("2025-01-12T16:45:00Z"),
                ["roles"] = new OpenApiArray
                {
                    new OpenApiString("User Administrator"),
                    new OpenApiString("Employee")
                }
            };
        }

        // Role request/response examples
        else if (type == typeof(CreateRoleRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["tenantId"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                ["name"] = new OpenApiString("Project Manager"),
                ["description"] = new OpenApiString("Manages projects and coordinates team activities within the tenant")
            };
        }
        else if (type == typeof(UpdateRoleRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["name"] = new OpenApiString("Senior Project Manager"),
                ["description"] = new OpenApiString("Manages complex projects and provides strategic guidance to project teams")
            };
        }
        else if (type == typeof(RoleDto))
        {
            schema.Example = new OpenApiObject
            {
                ["id"] = new OpenApiString("6ba7b810-9dad-11d1-80b4-00c04fd430c8"),
                ["tenantId"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                ["name"] = new OpenApiString("Project Manager"),
                ["normalizedName"] = new OpenApiString("PROJECT MANAGER")
            };
        }

        // Authentication request/response examples
        else if (type == typeof(LoginRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["tenantId"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                ["userNameOrEmail"] = new OpenApiString("jdoe@acme.com"),
                ["password"] = new OpenApiString("SecurePass123!@#"),
                ["rememberMe"] = new OpenApiBoolean(false)
            };
        }
        else if (type == typeof(OAuthTokenRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["grantType"] = new OpenApiString("password"),
                ["username"] = new OpenApiString("jdoe@acme.com"),
                ["password"] = new OpenApiString("SecurePass123!@#"),
                ["tenantId"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                ["scope"] = new OpenApiString("api.read api.write")
            };
        }
        else if (type == typeof(ChangePasswordRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["tenantId"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                ["currentPassword"] = new OpenApiString("OldPassword123!@#"),
                ["newPassword"] = new OpenApiString("NewSecurePass456!@#"),
                ["confirmNewPassword"] = new OpenApiString("NewSecurePass456!@#")
            };
        }
        else if (type == typeof(AuthResult))
        {
            schema.Example = new OpenApiObject
            {
                ["succeeded"] = new OpenApiBoolean(true),
                ["token"] = new OpenApiString("eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI3YzllNjY3OS03NDI1LTQwZGUtOTQ0Yi1lMDdmYzFmOTBhZTciLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiamRvZSIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL2VtYWlsYWRkcmVzcyI6ImpvaG4uZG9lQGFjbWUuY29tIiwiZXhwIjoxNzA1MzUyMDAwLCJpc3MiOiJCb2lsZXJQbGF0ZUF1dGhlbnRpY2F0aW9uIiwiYXVkIjoiQm9pbGVyUGxhdGVBUEkifQ.example-signature"),
                ["errors"] = new OpenApiArray(),
                ["user"] = new OpenApiObject
                {
                    ["id"] = new OpenApiString("7c9e6679-7425-40de-944b-e07fc1f90ae7"),
                    ["tenantId"] = new OpenApiString("550e8400-e29b-41d4-a716-446655440000"),
                    ["userName"] = new OpenApiString("jdoe"),
                    ["email"] = new OpenApiString("john.doe@acme.com"),
                    ["firstName"] = new OpenApiString("John"),
                    ["lastName"] = new OpenApiString("Doe"),
                    ["phoneNumber"] = new OpenApiString("+1-555-0123"),
                    ["emailConfirmed"] = new OpenApiBoolean(true),
                    ["phoneNumberConfirmed"] = new OpenApiBoolean(false),
                    ["isActive"] = new OpenApiBoolean(true),
                    ["createdAt"] = new OpenApiString("2025-01-10T08:15:00Z"),
                    ["updatedAt"] = new OpenApiString("2025-01-12T16:45:00Z"),
                    ["roles"] = new OpenApiArray
                    {
                        new OpenApiString("User Administrator")
                    }
                }
            };
        }
        else if (type == typeof(OAuthTokenResponse))
        {
            schema.Example = new OpenApiObject
            {
                ["accessToken"] = new OpenApiString("eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI3YzllNjY3OS03NDI1LTQwZGUtOTQ0Yi1lMDdmYzFmOTBhZTciLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiamRvZSIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL2VtYWlsYWRkcmVzcyI6ImpvaG4uZG9lQGFjbWUuY29tIiwiZXhwIjoxNzA1MzUyMDAwLCJpc3MiOiJCb2lsZXJQbGF0ZUF1dGhlbnRpY2F0aW9uIiwiYXVkIjoiQm9pbGVyUGxhdGVBUEkifQ.example-signature"),
                ["tokenType"] = new OpenApiString("Bearer"),
                ["expiresIn"] = new OpenApiInteger(3600),
                ["refreshToken"] = new OpenApiString("refresh-token-example-123456789abcdef"),
                ["scope"] = new OpenApiString("api.read api.write")
            };
        }
        else if (type == typeof(OAuthRefreshTokenRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["grantType"] = new OpenApiString("refresh_token"),
                ["refreshToken"] = new OpenApiString("refresh-token-example-123456789abcdef"),
                ["scope"] = new OpenApiString("api.read api.write")
            };
        }

        // Controller-specific request examples
        else if (type == typeof(AssignRolesRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["roles"] = new OpenApiArray
                {
                    new OpenApiString("Project Manager"),
                    new OpenApiString("Team Lead")
                }
            };
        }
        else if (type == typeof(RemoveRolesRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["roles"] = new OpenApiArray
                {
                    new OpenApiString("Temporary Access"),
                    new OpenApiString("Guest")
                }
            };
        }
    }
}
