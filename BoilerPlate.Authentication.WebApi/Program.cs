using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Extensions;
using BoilerPlate.Authentication.WebApi.Filters;
using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.Edm;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddOData(options => options
        .Select()
        .Filter()
        .OrderBy()
        .Expand()
        .Count()
        .SetMaxTop(100)
        .AddRouteComponents("odata", ODataConfiguration.GetEdmModel()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Authentication API",
        Version = "v1",
        Description = "OAuth2 authentication API with JWT tokens using RS256 (RSA asymmetric encryption). " +
                      "Includes RESTful endpoints and OData query endpoints for all authentication entities."
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\". " +
                      "Obtain a token from the /oauth/token endpoint.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    // Enable XML comments for better documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Add operation filter to include authorization requirements in Swagger
    c.OperationFilter<AuthorizationOperationFilter>();
    
    // Include OData endpoints
    c.DocumentFilter<ODataDocumentFilter>();
});

// Add authentication database and services
builder.Services.AddAuthenticationServices(builder.Configuration);

// Add JWT authentication with RS256
builder.Services.AddJwtAuthentication(builder.Configuration);

// Add admin user initialization service
builder.Services.AddHostedService<AdminUserInitializationService>();

var app = builder.Build();

// Configure the HTTP request pipeline
// Enable Swagger in all environments (can be restricted to Development if needed)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Authentication API v1");
    c.RoutePrefix = "swagger"; // Swagger UI available at /swagger
    c.DisplayRequestDuration();
    c.EnableDeepLinking();
    c.EnableFilter();
    c.ShowExtensions();
    c.EnableValidator();
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
    c.DefaultModelsExpandDepth(-1); // Collapse models by default
    c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
});

app.UseHttpsRedirection();

// Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers (includes OData controllers)
app.MapControllers();

app.Run();
