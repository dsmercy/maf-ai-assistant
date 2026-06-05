using AssistantApi.Application.Agents;
using AssistantApi.Application.Configuration;
using AssistantApi.Application.Services;
using AssistantApi.Application.Validators;
using AssistantApi.HealthChecks;
using AssistantApi.Infrastructure;
using AssistantApi.Middleware;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// Options
builder.Services.Configure<AssistantOptions>(builder.Configuration.GetSection(AssistantOptions.SectionName));

// Infrastructure (EF, Ollama, Qdrant)
builder.Services.AddInfrastructure(builder.Configuration);

// Agents — order matters: leaf agents first, orchestrator last
builder.Services.AddScoped<RepositoryAgent>();
builder.Services.AddScoped<InstructionAgent>();
builder.Services.AddScoped<CodingAgent>();
builder.Services.AddScoped<OrchestratorAgent>();
builder.Services.AddScoped<ChatService>();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<ChatRequestValidator>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<OllamaHealthCheck>("ollama", tags: ["ready"])
    .AddCheck<QdrantHealthCheck>("qdrant", tags: ["ready"])
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

// JWT — Open WebUI issues tokens; we validate them on all non-health endpoints
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "replace_with_32_char_secret_minimum";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // dev only — enable in production
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,  // Open WebUI doesn't set a fixed issuer
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "application/json";
                // OpenAI-format error so Open WebUI can parse it correctly
                return ctx.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    error = new
                    {
                        message = "Missing bearer authentication in header",
                        type = "invalid_request_error",
                        param = (string?)null,
                        code = (string?)null
                    }
                }));
            },
            OnMessageReceived = ctx =>
            {
                // Accept static API key (WEBUI_SECRET_KEY) as a valid bearer token for /v1/* routes
                // This allows Open WebUI to call our API without needing a full JWT
                var apiKey = builder.Configuration["Jwt:Secret"];
                var token = ctx.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(token) && token == apiKey && ctx.Request.Path.StartsWithSegments("/v1"))
                {
                    // Synthesise a simple identity so the request passes auth
                    var claims = new[] { new System.Security.Claims.Claim("sub", "open-webui") };
                    var identity = new System.Security.Claims.ClaimsIdentity(claims, "ApiKey");
                    ctx.Principal = new System.Security.Claims.ClaimsPrincipal(identity);
                    ctx.Success();
                }
                return Task.CompletedTask;
            },
            // Extract user identity from Open WebUI JWT claims
            OnTokenValidated = ctx =>
            {
                var email = ctx.Principal?.FindFirst("email")?.Value
                         ?? ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                         ?? ctx.Principal?.FindFirst("sub")?.Value
                         ?? "anonymous";
                ctx.HttpContext.Items["UserId"] = email;
                return Task.CompletedTask;
            }
        };
    });

// TODO: Re-enable auth enforcement for production (Phase 5)
// builder.Services.AddAuthorization(options =>
// {
//     options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
//         .RequireAuthenticatedUser()
//         .Build();
// });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-apply EF migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AssistantApi.Infrastructure.Persistence.AssistantDbContext>();
    db.Database.EnsureCreated();
}

app.UseMiddleware<ValidationExceptionMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Liveness — always returns 200, no auth required
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();

// Readiness — checks all downstream services
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(result));
    }
}).AllowAnonymous();

app.MapControllers();

app.Run();
