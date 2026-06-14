using AssistantApi.Application.Agents;
using AssistantApi.Application.Configuration;
using AssistantApi.Application.Services;
using AssistantApi.Application.Validators;
using AssistantApi.HealthChecks;
using AssistantApi.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace AssistantApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationOptions(
        this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        services.Configure<AssistantOptions>(configuration.GetSection(AssistantOptions.SectionName));

        // Resolve relative ingestion paths against the content root so "data/uploads"
        // becomes "<project-dir>/data/uploads" locally while Docker absolute paths are left unchanged.
        services.PostConfigure<AssistantApi.Infrastructure.Ingestion.IngestionOptions>(opts =>
        {
            if (!Path.IsPathRooted(opts.UploadPath))
                opts.UploadPath = Path.GetFullPath(Path.Combine(contentRootPath, opts.UploadPath));
            if (!Path.IsPathRooted(opts.RepositoryCachePath))
                opts.RepositoryCachePath = Path.GetFullPath(Path.Combine(contentRootPath, opts.RepositoryCachePath));
        });

        return services;
    }

    public static IServiceCollection AddValidationFromAssembly(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<ChatRequestValidator>();
        return services;
    }

    public static IServiceCollection AddAgents(this IServiceCollection services)
    {
        services.AddScoped<RepositoryAgent>();
        services.AddScoped<InstructionAgent>();
        services.AddScoped<CodingAgent>();
        services.AddScoped<OrchestratorAgent>();
        services.AddScoped<ChatService>();
        services.AddScoped<ToolCallService>();
        return services;
    }

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<OllamaHealthCheck>("ollama",    tags: ["ready"])
            .AddCheck<QdrantHealthCheck>("qdrant",    tags: ["ready"])
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);
        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSecret = configuration["Jwt:Secret"] ?? "replace_with_32_char_secret_minimum";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ValidateIssuer           = false,
                    ValidateAudience         = false,
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.FromMinutes(5)
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = ctx =>
                    {
                        ctx.HandleResponse();
                        ctx.Response.StatusCode  = 401;
                        ctx.Response.ContentType = "application/json";
                        return ctx.Response.WriteAsync(JsonSerializer.Serialize(new
                        {
                            error = new
                            {
                                message = "Missing bearer authentication in header",
                                type    = "invalid_request_error",
                                param   = (string?)null,
                                code    = (string?)null
                            }
                        }));
                    },
                    OnMessageReceived = ctx =>
                    {
                        // Accept the static API key as a bearer token on /v1/* routes
                        // so Continue.dev and Open WebUI can call the API without a full JWT.
                        var apiKey = configuration["Jwt:Secret"];
                        var token  = ctx.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
                        if (!string.IsNullOrEmpty(token) && token == apiKey
                            && ctx.Request.Path.StartsWithSegments("/v1"))
                        {
                            ctx.Principal = new ClaimsPrincipal(
                                new ClaimsIdentity([new Claim("sub", "api-key-client")], "ApiKey"));
                            ctx.Success();
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = ctx =>
                    {
                        var email = ctx.Principal?.FindFirst("email")?.Value
                                 ?? ctx.Principal?.FindFirst(ClaimTypes.Email)?.Value
                                 ?? ctx.Principal?.FindFirst("sub")?.Value
                                 ?? "anonymous";
                        ctx.HttpContext.Items["UserId"] = email;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("chat", opt =>
            {
                opt.Window               = TimeSpan.FromMinutes(1);
                opt.PermitLimit          = 20;
                opt.QueueLimit           = 0;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });

            options.AddFixedWindowLimiter("ingestion", opt =>
            {
                opt.Window               = TimeSpan.FromMinutes(1);
                opt.PermitLimit          = 10;
                opt.QueueLimit           = 0;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
        });

        return services;
    }

    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("assistant-api"))
                .AddAspNetCoreInstrumentation(opts => opts.RecordException = true)
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("assistant-api"))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        return services;
    }
}
