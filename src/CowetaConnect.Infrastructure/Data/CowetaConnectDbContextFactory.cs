// src/CowetaConnect.Infrastructure/Data/CowetaConnectDbContextFactory.cs
// Used only by EF Core CLI tools (dotnet ef migrations add, etc.)
// Not registered in DI — the real DbContext comes from ServiceCollectionExtensions.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NetTopologySuite;

namespace CowetaConnect.Infrastructure.Data;

public class CowetaConnectDbContextFactory : IDesignTimeDbContextFactory<CowetaConnectDbContext>
{
    public CowetaConnectDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CowetaConnectDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=cowetaconnect;Username=postgres;Password=localdev",
            npgsql => npgsql
                .UseNetTopologySuite()
                .MigrationsAssembly(typeof(CowetaConnectDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention();

        return new CowetaConnectDbContext(optionsBuilder.Options);
    }
}
