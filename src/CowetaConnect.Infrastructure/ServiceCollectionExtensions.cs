using Azure.Identity;
using Azure.Storage.Blobs;
using CowetaConnect.Infrastructure.Data;
using CowetaConnect.Infrastructure.Health;
using Elastic.Clients.Elasticsearch;
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

        return services;
    }
}
