using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using CowetaConnect.API.Middleware;
using CowetaConnect.Application;
using CowetaConnect.Infrastructure;
using CowetaConnect.Infrastructure.Health;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// Bootstrap logger — captures startup errors before full Serilog is configured.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Key Vault (production only, via Managed Identity) ────────────────────
    if (!builder.Environment.IsDevelopment())
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri("https://cowetaconnect-kv.vault.azure.net/"),
            new DefaultAzureCredential());
    }

    // ── Serilog ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, lc) =>
    {
        lc.ReadFrom.Configuration(ctx.Configuration)
          .Enrich.FromLogContext()
          .Enrich.WithProperty("Application", "CowetaConnect.API")
          .WriteTo.Console();

        // App Service auto-injects APPLICATIONINSIGHTS_CONNECTION_STRING when linked.
        var aiConnectionString = ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
                                 ?? ctx.Configuration["ApplicationInsights:ConnectionString"];
        if (!string.IsNullOrEmpty(aiConnectionString))
            lc.WriteTo.ApplicationInsights(aiConnectionString, TelemetryConverter.Traces);
    });

    // ── JWT RS256 signing key ─────────────────────────────────────────────────
    RsaSecurityKey signingKey;
    if (builder.Environment.IsDevelopment())
    {
        // Ephemeral key: tokens don't survive process restarts in dev — acceptable.
        var rsa = RSA.Create(2048);
        signingKey = new RsaSecurityKey(rsa) { KeyId = "dev-ephemeral" };
    }
    else
    {
        // Production: private key PEM stored in Key Vault as "Jwt--PrivateKeyPem".
        var rsa = RSA.Create();
        rsa.ImportFromPem(builder.Configuration["Jwt:PrivateKeyPem"]);
        signingKey = new RsaSecurityKey(rsa)
        {
            KeyId = builder.Configuration["Jwt:KeyId"]
        };
    }

    // Expose signing key so AuthController can sign tokens.
    builder.Services.AddSingleton(signingKey);

    // ── Controllers ───────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // ── API Versioning ────────────────────────────────────────────────────────
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // ── CORS ──────────────────────────────────────────────────────────────────
    // App:SpaOrigin is set via Key Vault / App Service config in production.
    // The Vue deploy bakes VITE_API_BASE_URL at build time, so the origin is fixed.
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("VueApp", policy =>
        {
            var spaOrigin = builder.Configuration["App:SpaOrigin"]
                            ?? "https://cowetaconnect.com";

            var origins = new List<string> { spaOrigin };

            if (!spaOrigin.Contains("www."))
                origins.Add(spaOrigin.Replace("://", "://www."));

            if (builder.Environment.IsDevelopment())
                origins.Add("http://localhost:5173");

            policy.WithOrigins([.. origins])
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // Required for refresh-token httpOnly cookie.
        });
    });

    // ── Authentication (JWT Bearer / RS256) ───────────────────────────────────
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ClockSkew = TimeSpan.Zero // Strict expiry — access token is only 15 min.
            };
        });

    // ── Authorization ─────────────────────────────────────────────────────────
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireOwner", p => p.RequireRole("Owner", "Admin"));
        options.AddPolicy("RequireAdmin", p => p.RequireRole("Admin"));
    });

    // ── Rate Limiting ─────────────────────────────────────────────────────────
    builder.Services.AddRateLimiter(options =>
    {
        // Global: 60 req/min (unauth) or 300 req/min (auth), per SECURITY.md §4.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            if (ctx.User.Identity?.IsAuthenticated == true)
            {
                return RateLimitPartition.GetSlidingWindowLimiter(
                    ctx.User.Identity.Name ?? "authenticated",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6
                    });
            }

            return RateLimitPartition.GetSlidingWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6
                });
        });

        // Named policy — auth endpoints: 10 req/min per IP.
        options.AddSlidingWindowLimiter("auth-endpoints", o =>
        {
            o.PermitLimit = 10;
            o.Window = TimeSpan.FromMinutes(1);
            o.SegmentsPerWindow = 6;
            o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            o.QueueLimit = 0;
        });

        // Named policy — file uploads: 5 req/min per user.
        options.AddSlidingWindowLimiter("file-upload", o =>
        {
            o.PermitLimit = 5;
            o.Window = TimeSpan.FromMinutes(1);
            o.SegmentsPerWindow = 6;
            o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            o.QueueLimit = 0;
        });

        // Named policy — admin endpoints: 200 req/min per user.
        options.AddSlidingWindowLimiter("admin-endpoints", o =>
        {
            o.PermitLimit = 200;
            o.Window = TimeSpan.FromMinutes(1);
            o.SegmentsPerWindow = 6;
            o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            o.QueueLimit = 0;
        });

        // Named policy — standard authenticated endpoints: 300 req/min.
        options.AddSlidingWindowLimiter("authenticated", o =>
        {
            o.PermitLimit = 300;
            o.Window = TimeSpan.FromMinutes(1);
            o.SegmentsPerWindow = 6;
            o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            o.QueueLimit = 0;
        });

        // Named policy — anonymous endpoints: 60 req/min.
        options.AddSlidingWindowLimiter("anonymous", o =>
        {
            o.PermitLimit = 60;
            o.Window = TimeSpan.FromMinutes(1);
            o.SegmentsPerWindow = 6;
            o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            o.QueueLimit = 0;
        });

        options.OnRejected = async (ctx, token) =>
        {
            ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await ctx.HttpContext.Response.WriteAsync(
                "Too many requests. Please retry later.", token);
        };
    });

    // ── Application + Infrastructure ──────────────────────────────────────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── Hangfire (storage config lives here because Hangfire.AspNetCore +
    //   Hangfire.PostgreSql are both API project references) ──────────────────
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(c =>
            c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Hangfire"))));

    builder.Services.AddHangfireServer();

    // ── Health Checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("Default")!,
            name: "database",
            tags: ["db", "ready"])
        .AddRedis(
            builder.Configuration.GetConnectionString("Redis")!,
            name: "redis",
            tags: ["cache", "ready"])
        .AddCheck<ElasticsearchHealthCheck>(
            "elasticsearch",
            tags: ["search", "ready"]);

    // ─────────────────────────────────────────────────────────────────────────
    var app = builder.Build();
    // ─────────────────────────────────────────────────────────────────────────

    // Exception handler must be FIRST — wraps the entire pipeline.
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();
    app.UseCors("VueApp");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapHealthChecks("/health");

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireAdminAuthorizationFilter()]
    });

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
