using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tds.Api.Data;

/// <summary>Lets <c>dotnet ef migrations add</c> run without configuration. Never connects.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time")
            .UseSnakeCaseNamingConvention()
            .Options);
}
