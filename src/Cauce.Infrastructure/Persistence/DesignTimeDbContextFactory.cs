using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cauce.Infrastructure.Persistence;

/// <summary>
/// Factory de tiempo de diseño que permite a las herramientas de EF Core
/// (<c>dotnet ef migrations add</c>, <c>dotnet ef database update</c>) crear el
/// contexto sin levantar la aplicación completa. La cadena de conexión se lee de
/// la variable de entorno <c>CAUCE_DESIGN_TIME_CONNECTION</c> o, en su ausencia,
/// de un valor de desarrollo por defecto.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CauceDbContext>
{
    private const string DesignTimeConnectionEnvironmentVariable = "CAUCE_DESIGN_TIME_CONNECTION";

    private const string DevelopmentFallbackConnectionString =
        "Host=localhost;Port=5432;Database=cauce_dev;Username=cauce;Password=cauce_pg_dev_2026";

    /// <inheritdoc />
    public CauceDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(DesignTimeConnectionEnvironmentVariable)
            ?? DevelopmentFallbackConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<CauceDbContext>();
        optionsBuilder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new CauceDbContext(optionsBuilder.Options);
    }
}
