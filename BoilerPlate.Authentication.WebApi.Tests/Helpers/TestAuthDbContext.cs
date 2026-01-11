using BoilerPlate.Authentication.Database;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Tests.Helpers;

/// <summary>
///     Concrete test implementation of BaseAuthDbContext for in-memory testing
/// </summary>
internal class TestAuthDbContext : BaseAuthDbContext
{
    /// <summary>
    ///     Constructor that accepts BaseAuthDbContext options
    /// </summary>
    public TestAuthDbContext(DbContextOptions<BaseAuthDbContext> options)
        : base(options)
    {
    }
}