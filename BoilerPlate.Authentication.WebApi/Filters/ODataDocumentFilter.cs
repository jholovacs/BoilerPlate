using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;

namespace BoilerPlate.Authentication.WebApi.Filters;

/// <summary>
/// Swagger document filter to include OData endpoints in the API documentation
/// </summary>
public class ODataDocumentFilter : IDocumentFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // OData endpoints are already included via controllers
        // This filter can be used to add additional OData-specific documentation if needed
        
        // Add OData query parameter documentation to relevant paths
        foreach (var path in swaggerDoc.Paths)
        {
            if (path.Key.Contains("/odata/", StringComparison.OrdinalIgnoreCase))
            {
                // Add OData query parameters to GET operations
                foreach (var operation in path.Value.Operations.Where(o => o.Key == Microsoft.OpenApi.Models.OperationType.Get))
                {
                    var op = operation.Value;
                    
                    // Add OData query parameters if not already present
                    if (op.Parameters == null)
                    {
                        op.Parameters = new List<OpenApiParameter>();
                    }

                    // Add common OData query parameters
                    var odataParams = new[]
                    {
                        new OpenApiParameter
                        {
                            Name = "$select",
                            In = ParameterLocation.Query,
                            Description = "Select specific properties to return",
                            Required = false,
                            Schema = new OpenApiSchema { Type = "string" }
                        },
                        new OpenApiParameter
                        {
                            Name = "$filter",
                            In = ParameterLocation.Query,
                            Description = "Filter results using OData filter syntax",
                            Required = false,
                            Schema = new OpenApiSchema { Type = "string" }
                        },
                        new OpenApiParameter
                        {
                            Name = "$orderby",
                            In = ParameterLocation.Query,
                            Description = "Order results by one or more properties",
                            Required = false,
                            Schema = new OpenApiSchema { Type = "string" }
                        },
                        new OpenApiParameter
                        {
                            Name = "$top",
                            In = ParameterLocation.Query,
                            Description = "Limit the number of results returned",
                            Required = false,
                            Schema = new OpenApiSchema { Type = "integer", Format = "int32" }
                        },
                        new OpenApiParameter
                        {
                            Name = "$skip",
                            In = ParameterLocation.Query,
                            Description = "Skip a number of results",
                            Required = false,
                            Schema = new OpenApiSchema { Type = "integer", Format = "int32" }
                        },
                        new OpenApiParameter
                        {
                            Name = "$expand",
                            In = ParameterLocation.Query,
                            Description = "Expand related entities",
                            Required = false,
                            Schema = new OpenApiSchema { Type = "string" }
                        },
                        new OpenApiParameter
                        {
                            Name = "$count",
                            In = ParameterLocation.Query,
                            Description = "Include count of results",
                            Required = false,
                            Schema = new OpenApiSchema { Type = "boolean" }
                        }
                    };

                    // Only add if not already present
                    foreach (var param in odataParams)
                    {
                        if (!op.Parameters.Any(p => p.Name == param.Name))
                        {
                            op.Parameters.Add(param);
                        }
                    }

                    // Update description to mention OData support
                    if (!string.IsNullOrEmpty(op.Description))
                    {
                        op.Description += "\n\n**OData Query Support:** This endpoint supports OData query options ($select, $filter, $orderby, $top, $skip, $expand, $count).";
                    }
                    else
                    {
                        op.Description = "**OData Query Support:** This endpoint supports OData query options ($select, $filter, $orderby, $top, $skip, $expand, $count).";
                    }
                }
            }
        }
    }
}
