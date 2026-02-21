using Azure.Identity;
using Azure.Storage.Blobs;
using CowetaConnect.Application.Auth.Interfaces;
using CowetaConnect.Infrastructure.Data;
using CowetaConnect.Infrastructure.Health;
using CowetaConnect.Infrastructure.Identity;
using CowetaConnect.Infrastructure.Services;
using Elastic.Clients.Elasticsearch;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CowetaConnect.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core + PostgreSQL + PostGIS
        services.AddDbContext<CowetaConnectDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql
                    .UseNetTopologySuite()
                    .MigrationsAssembly(typeof(CowetaConnectDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention());

        // ASP.NET Core Identity — lean setup (no cookie auth, just UserManager + stores).
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            // Password policy — matches SECURITY.md requirements.
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;

            // Disable Identity's built-in lockout — we handle it in Redis per-IP.
            options.Lockout.MaxFailedAccessAttempts = int.MaxValue;

            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<CowetaConnectDbContext>();

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));

        // Elasticsearch
        services.AddSingleton(_ =>
        {
            var settings = new ElasticsearchClientSettings(
                new Uri(configuration["Elasticsearch:Url"]!));
            return new ElasticsearchClient(settings);
        });

        // Azure Blob Storage (Managed Identity in production, connection string in dev)
        var blobUri = configuration["AzureStorage:BlobServiceUri"];
        if (!string.IsNullOrEmpty(blobUri))
        {
            services.AddSingleton(_ =>
                new BlobServiceClient(new Uri(blobUri), new DefaultAzureCredential()));
        }

        // Elasticsearch health check
        services.AddSingleton<ElasticsearchHealthCheck>();

        // Auth services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthUserService, AuthUserService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        return services;
    }
}
